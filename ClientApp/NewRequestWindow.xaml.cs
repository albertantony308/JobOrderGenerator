using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ClientApp.Data;
using ClientApp.Models;
using ClientApp.Services;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ClientApp
{
    public partial class NewRequestWindow : Window
    {
        public bool WasCreated { get; private set; }
        public bool RequestNavigationToBranding { get; private set; } = false;
        private string _generatedMemoNum = "";
        private List<string> _selectedImagePaths = new List<string>();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var element = Keyboard.FocusedElement as UIElement;
                if (element is TextBox textBox)
                {
                    // If it's a multi-line textbox, we might want Enter to work as a newline
                    // but the user said "after printing data on each Text box if I press enter it should move to the next text box"
                    // However, for multi-line ones, let's keep default behavior if they want newlines.
                    // Actually, let's just move focus for all unless it's explicitly handled.
                    if (textBox.AcceptsReturn && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // Allow Shift+Enter for newlines in multi-line boxes if needed, 
                        // but default Enter will move focus as requested.
                        return;
                    }

                    textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down)
            {
                var element = Keyboard.FocusedElement as UIElement;
                if (element is TextBox tb && tb.AcceptsReturn)
                {
                    try
                    {
                        int caretIndex = tb.CaretIndex;
                        int lineIndex = tb.GetLineIndexFromCharacterIndex(caretIndex);
                        int lineCount = tb.LineCount;
                        if (lineIndex < lineCount - 1)
                        {
                            return; // Let standard behavior handle line down
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (element is ComboBox cb && cb.IsDropDownOpen)
                {
                    return;
                }

                if (element is Control)
                {
                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                var element = Keyboard.FocusedElement as UIElement;
                if (element is TextBox tb && tb.AcceptsReturn)
                {
                    try
                    {
                        int caretIndex = tb.CaretIndex;
                        int lineIndex = tb.GetLineIndexFromCharacterIndex(caretIndex);
                        if (lineIndex > 0)
                        {
                            return; // Let standard behavior handle line up
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (element is ComboBox cb && cb.IsDropDownOpen)
                {
                    return;
                }

                if (element is Control)
                {
                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                    e.Handled = true;
                }
            }
        }

        public NewRequestWindow()
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            txtMemoNumber.Text = "Order ID: Calculating...";

            this.Loaded += (s, e) =>
            {
                // Populate country code dropdowns
                var countries = Services.CountryCodeHelper.GetCountries();
                
                cmbCountryCode.ItemsSource = countries;

                // Select default
                string defaultCode = SettingsManager.Default.DefaultCountryCode ?? "+1";
                
                var defaultCountry1 = cmbCountryCode.Items.Cast<Services.CountryInfo>()
                    .FirstOrDefault(c => c.Code == defaultCode);
                if (defaultCountry1 != null)
                    cmbCountryCode.SelectedItem = defaultCountry1;
                else if (cmbCountryCode.Items.Count > 0)
                    cmbCountryCode.SelectedIndex = 0;

                _generatedMemoNum = GetNextOrderId();
                txtMemoNumber.Text = "Order ID: " + _generatedMemoNum;
                
                // Broadcast active draft to LAN peers to avoid clashes
                if (SettingsManager.Default.SyncMode != "InternetOnly")
                {
                    LanSyncService.ActiveDraftMemoNumber = _generatedMemoNum;
                }
            };

            this.Closed += (s, e) =>
            {
                // Reset active draft when window closes
                if (SettingsManager.Default.SyncMode != "InternetOnly")
                {
                    LanSyncService.ActiveDraftMemoNumber = string.Empty;
                }
            };

            this.MouseLeftButtonDown += (s, e) => this.DragMove();
            UpdateImageUI();

            // Wire up automatic focus scrolling
            txtCustomerName.GotFocus += FormElement_GotFocus;
            cmbCountryCode.GotFocus += FormElement_GotFocus;
            txtPhoneNumber.GotFocus += FormElement_GotFocus;
            txtTechnician.GotFocus += FormElement_GotFocus;
            txtBrand.GotFocus += FormElement_GotFocus;
            txtDeviceName.GotFocus += FormElement_GotFocus;
            txtDeviceModel.GotFocus += FormElement_GotFocus;
            txtSerialNumber.GotFocus += FormElement_GotFocus;
            txtAccessories.GotFocus += FormElement_GotFocus;
            txtEstCost.GotFocus += FormElement_GotFocus;
            txtComplaint.GotFocus += FormElement_GotFocus;
            txtDiagnostics.GotFocus += FormElement_GotFocus;
            toggleRepeatedDevice.GotFocus += FormElement_GotFocus;
            btnAddImage.GotFocus += FormElement_GotFocus;
        }

        private void FormElement_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && FormScrollViewer != null)
            {
                element.BringIntoView();

                // Scroll the ScrollViewer to position the element near the upper-middle of viewport
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var transform = element.TransformToAncestor(FormScrollViewer);
                        var elementOffset = transform.Transform(new Point(0, 0));
                        
                        double currentVerticalOffset = FormScrollViewer.VerticalOffset;
                        // Scroll down slightly more (e.g. keeping a 100px gap from top) so subsequent fields are visible
                        double newOffset = currentVerticalOffset + elementOffset.Y - 100;
                        
                        if (newOffset > 0 && newOffset <= FormScrollViewer.ScrollableHeight)
                        {
                            FormScrollViewer.ScrollToVerticalOffset(newOffset);
                        }
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private string GetNextOrderId()
        {
            string prefix = LicenseManager.GetDeviceOrderPrefix();
            using (var db = new LocalDbContext())
            {
                int max = 999;
                var ownMemos = db.ServiceMemos
                    .Where(m => m.MemoNumber.StartsWith(prefix))
                    .Select(m => m.MemoNumber)
                    .ToList();

                foreach (var mn in ownMemos)
                {
                    if (string.IsNullOrEmpty(mn)) continue;
                    var numMatch = Regex.Match(mn, @"\d+");
                    if (numMatch.Success && int.TryParse(numMatch.Value, out int v))
                        max = Math.Max(max, v);
                }
                return $"{prefix}{(max + 1):D4}";
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedImagePaths.Count >= 5)
            {
                MessageBox.Show("Maximum 5 images allowed.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ofd = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Select Product Image",
                Multiselect = true
            };

            if (ofd.ShowDialog() == true)
            {
                foreach (var file in ofd.FileNames)
                {
                    if (_selectedImagePaths.Count < 5 && !_selectedImagePaths.Contains(file))
                    {
                        _selectedImagePaths.Add(file);
                    }
                }
                UpdateImageUI();
            }
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
            {
                _selectedImagePaths.Remove(path);
                UpdateImageUI();
            }
        }

        private void UpdateImageUI()
        {
            ImagesList.ItemsSource = null;
            ImagesList.ItemsSource = _selectedImagePaths;
            txtImageCount.Text = $"{_selectedImagePaths.Count} / 5 images selected";
            btnAddImage.Visibility = _selectedImagePaths.Count >= 5 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PreviewPrint_Click(object sender, RoutedEventArgs e)
        {
            var memo = CreateMemoFromInput();
            if (memo == null) return;

            var preview = new PrintPreviewWindow(memo);
            preview.Owner = this;
            preview.ShowDialog();
            
            if (preview.RequestNavigationToBranding)
            {
                this.RequestNavigationToBranding = true;
                this.Close();
                return;
            }
            
            SaveMemo(memo);
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var memo = CreateMemoFromInput();
            if (memo != null)
            {
                SaveMemo(memo);
            }
        }

        private ServiceMemo? CreateMemoFromInput()
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text) || 
                string.IsNullOrWhiteSpace(txtDeviceName.Text) ||
                string.IsNullOrWhiteSpace(txtComplaint.Text))
            {
                MessageBox.Show("Please fill out Customer Name, Device Name, and Complaint.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var selectedCountry = cmbCountryCode.SelectedItem as Services.CountryInfo;
            string countryCode = selectedCountry?.Code ?? "+1";
            string phoneNum = txtPhoneNumber.Text.Trim();

            if (string.IsNullOrWhiteSpace(phoneNum))
            {
                MessageBox.Show("Please enter a Phone Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (!Services.CountryCodeHelper.IsPhoneNumberValid(phoneNum))
            {
                MessageBox.Show("Please enter a valid Phone Number (7 to 15 digits).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Persist the selected country code in settings as the default for next time
            if (SettingsManager.Default.DefaultCountryCode != countryCode)
            {
                SettingsManager.Default.DefaultCountryCode = countryCode;
                SettingsManager.Save();
            }

            string fullPhone1 = $"{countryCode} {phoneNum}";

            decimal estCost = 0;
            if (!string.IsNullOrWhiteSpace(txtEstCost.Text) && !decimal.TryParse(txtEstCost.Text, out estCost))
            {
                MessageBox.Show("Please enter a valid number for Estimated Cost.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            List<string> localPaths = new List<string>();
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "Images");
            Directory.CreateDirectory(dir);

            for (int i = 0; i < _selectedImagePaths.Count; i++)
            {
                try
                {
                    string ext = Path.GetExtension(_selectedImagePaths[i]);
                    string localPath = Path.Combine(dir, $"{_generatedMemoNum}_{i}{ext}");
                    File.Copy(_selectedImagePaths[i], localPath, true);
                    localPaths.Add(localPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving image: " + ex.Message);
                }
            }

            return new ServiceMemo
            {
                MemoNumber = _generatedMemoNum,
                CustomerName = txtCustomerName.Text.Trim(),
                PhoneNumber = fullPhone1,
                CustomerAddress = txtCustomerAddress.Text.Trim(),
                Phone1 = fullPhone1, // Compulsory
                TechnicianName = txtTechnician.Text.Trim(),
                DeviceName = txtDeviceName.Text.Trim(),
                Brand = txtBrand.Text.Trim(),
                DeviceModel = txtDeviceModel.Text.Trim(),
                SerialNumber = txtSerialNumber.Text.Trim(),
                Accessories = txtAccessories.Text.Trim(),
                IssueDescription = txtComplaint.Text.Trim(),
                Diagnostics = txtDiagnostics.Text.Trim(),
                EstimatedCost = estCost,
                Status = "Pending",
                IsRepeatedDevice = toggleRepeatedDevice.IsChecked == true,
                CreatedAt = DateTime.Now,
                UpdatedAt = NetworkTimeService.GetUtcNow(),
                ImagePath = string.Join("|", localPaths)
            };
        }

        private void SaveMemo(ServiceMemo memo)
        {
            using (var db = new LocalDbContext())
            {
                db.ServiceMemos.Add(memo);
                db.SaveChanges();
            }

            // P2P LAN Broadcast
            if (SettingsManager.Default.SyncMode != "InternetOnly")
            {
                _ = LanSyncService.BroadcastMemoSavedAsync(memo);
            }

            if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
            {
                _ = CloudSyncService.SyncWithCloudAsync();
            }

            WasCreated = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
