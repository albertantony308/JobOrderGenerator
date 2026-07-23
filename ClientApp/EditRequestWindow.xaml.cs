using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ClientApp.Data;
using ClientApp.Models;
using ClientApp.Services;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ClientApp
{
    public partial class EditRequestWindow : Window
    {
        private int _memoId;
        public bool WasUpdated { get; private set; }
        public bool RequestNavigationToBranding { get; set; } = false;
        private List<string> _currentImagePaths = new List<string>();
        private string _memoNumber = "";
        private bool _isRepeatedDevice;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var element = Keyboard.FocusedElement as UIElement;
                if (element is TextBox textBox)
                {
                    if (textBox.AcceptsReturn && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
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

        public EditRequestWindow(int memoId)
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            _memoId = memoId;

            // Populate country code dropdowns
            var countries = CountryCodeHelper.GetCountries();
            cmbCountryCode.ItemsSource = countries;

            LoadMemo();

            this.MouseLeftButtonDown += (s, e) => this.DragMove();

            // Wire up automatic focus scrolling
            txtCustomerName.GotFocus += FormElement_GotFocus;
            cmbCountryCode.GotFocus += FormElement_GotFocus;
            txtPhoneNumber.GotFocus += FormElement_GotFocus;
            txtCustomerAddress.GotFocus += FormElement_GotFocus;
            txtTechnician.GotFocus += FormElement_GotFocus;
            txtBrand.GotFocus += FormElement_GotFocus;
            txtDeviceName.GotFocus += FormElement_GotFocus;
            txtDeviceModel.GotFocus += FormElement_GotFocus;
            txtSerialNumber.GotFocus += FormElement_GotFocus;
            txtAccessories.GotFocus += FormElement_GotFocus;
            txtEstCost.GotFocus += FormElement_GotFocus;
            txtComplaint.GotFocus += FormElement_GotFocus;
            txtDiagnostics.GotFocus += FormElement_GotFocus;
            cmbStatus.GotFocus += FormElement_GotFocus;
            toggleRepeatedDevice.GotFocus += FormElement_GotFocus;
            btnAddImage.GotFocus += FormElement_GotFocus;
        }

        private void cmbStatus_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                FormScrollViewer?.RaiseEvent(eventArg);
            }
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

        private void LoadMemo()
        {
            using (var db = new LocalDbContext())
            {
                var memo = db.ServiceMemos.Find(_memoId);
                if (memo != null)
                {
                    _memoNumber = memo.MemoNumber;
                    txtMemoNumber.Text = "Order ID: " + memo.MemoNumber;
                    txtCustomerName.Text = memo.CustomerName;
                    txtCustomerAddress.Text = memo.CustomerAddress;
                    txtTechnician.Text = memo.TechnicianName;
                    txtDeviceName.Text = memo.DeviceName;
                    txtBrand.Text = memo.Brand;
                    txtDeviceModel.Text = memo.DeviceModel;
                    txtSerialNumber.Text = memo.SerialNumber;
                    txtAccessories.Text = memo.Accessories;
                    txtEstCost.Text = memo.EstimatedCost.ToString();
                    txtComplaint.Text = memo.IssueDescription;
                    txtDiagnostics.Text = memo.Diagnostics;

                    // Parse Phone Number
                    string defaultCode = SettingsManager.Default.DefaultCountryCode ?? "+1";
                    var phone1Parsed = CountryCodeHelper.ParsePhoneNumber(memo.PhoneNumber, defaultCode);
                    var matchedCountry1 = cmbCountryCode.Items.Cast<CountryInfo>()
                        .FirstOrDefault(c => c.Code == phone1Parsed.countryCode);
                    if (matchedCountry1 != null)
                        cmbCountryCode.SelectedItem = matchedCountry1;
                    else
                    {
                        var defaultCountry = cmbCountryCode.Items.Cast<CountryInfo>()
                        .FirstOrDefault(c => c.Code == defaultCode);
                        if (defaultCountry != null)
                            cmbCountryCode.SelectedItem = defaultCountry;
                        else if (cmbCountryCode.Items.Count > 0)
                            cmbCountryCode.SelectedIndex = 0;
                    }
                    txtPhoneNumber.Text = phone1Parsed.localNumber;

                    if (!string.IsNullOrEmpty(memo.ImagePath))
                    {
                        _currentImagePaths = memo.ImagePath.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                    UpdateImageUI();
                    
                    foreach (System.Windows.Controls.ComboBoxItem item in cmbStatus.Items)
                    {
                        if (item.Content.ToString() == memo.Status)
                        {
                            cmbStatus.SelectedItem = item;
                            break;
                        }
                    }

                    // Show repeated device banner if applicable
                    _isRepeatedDevice = memo.IsRepeatedDevice;
                    if (_isRepeatedDevice)
                    {
                        RepeatedDeviceBanner.Visibility = Visibility.Visible;
                        RepeatedDeviceToggleBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        RepeatedDeviceBanner.Visibility = Visibility.Collapsed;
                        RepeatedDeviceToggleBar.Visibility = Visibility.Visible;
                        toggleRepeatedDevice.IsChecked = false;
                    }
                }
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImagePaths.Count >= 5)
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
                    if (_currentImagePaths.Count < 5 && !_currentImagePaths.Contains(file))
                    {
                        _currentImagePaths.Add(file);
                    }
                }
                UpdateImageUI();
            }
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
            {
                _currentImagePaths.Remove(path);
                UpdateImageUI();
            }
        }

        private void UpdateImageUI()
        {
            ImagesList.ItemsSource = null;
            ImagesList.ItemsSource = _currentImagePaths.ToList();
            txtImageCount.Text = $"{_currentImagePaths.Count} / 5 images selected";
            btnAddImage.Visibility = _currentImagePaths.Count >= 5 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void EditRepeatedDevice_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This job order is marked as a repeated device. Would you like to disable this status?", 
                "Repeated Device Status", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isRepeatedDevice = false;
                RepeatedDeviceBanner.Visibility = Visibility.Collapsed;
                RepeatedDeviceToggleBar.Visibility = Visibility.Visible;
                toggleRepeatedDevice.IsChecked = false;
                
                // Save immediately so the change persists even without clicking "Save Changes"
                using (var db = new LocalDbContext())
                {
                    var memo = db.ServiceMemos.Find(_memoId);
                    if (memo != null)
                    {
                        memo.IsRepeatedDevice = false;
                        memo.UpdatedAt = DateTime.Now;
                        db.SaveChanges();
                        WasUpdated = true;
                    }
                }
            }
        }

        private void ToggleRepeatedDevice_Checked(object sender, RoutedEventArgs e)
        {
            _isRepeatedDevice = true;
        }

        private void ToggleRepeatedDevice_Unchecked(object sender, RoutedEventArgs e)
        {
            _isRepeatedDevice = false;
        }

        private void PreviewPrint_Click(object sender, RoutedEventArgs e)
        {
            // We save changes before printing to ensure the preview has latest data
            Save_Click(sender, e);
            
            using (var db = new LocalDbContext())
            {
                var memoToPrint = db.ServiceMemos.Find(_memoId);
                if (memoToPrint != null)
                {
                    var preview = new PrintPreviewWindow(memoToPrint);
                    preview.Owner = this;
                    preview.ShowDialog();
                    
                    if (preview.RequestNavigationToBranding)
                    {
                        this.RequestNavigationToBranding = true;
                        this.Close();
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text) || 
                string.IsNullOrWhiteSpace(txtDeviceName.Text) ||
                string.IsNullOrWhiteSpace(txtComplaint.Text))
            {
                MessageBox.Show("Please fill out Customer Name, Device Name, and Complaint.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCountry = cmbCountryCode.SelectedItem as CountryInfo;
            string countryCode = selectedCountry?.Code ?? "+1";
            string phoneNum = txtPhoneNumber.Text.Trim();

            if (string.IsNullOrWhiteSpace(phoneNum))
            {
                MessageBox.Show("Please enter a Phone Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!CountryCodeHelper.IsPhoneNumberValid(phoneNum))
            {
                MessageBox.Show("Please enter a valid Phone Number (7 to 15 digits).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save default country code to settings
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
                return;
            }

            using (var db = new LocalDbContext())
            {
                var memo = db.ServiceMemos.Find(_memoId);
                if (memo != null)
                {
                    memo.CustomerName = txtCustomerName.Text.Trim();
                    memo.PhoneNumber = fullPhone1;
                    memo.CustomerAddress = txtCustomerAddress.Text.Trim();
                    memo.Phone1 = fullPhone1; // Phone 1 (Compulsory)
                    memo.TechnicianName = txtTechnician.Text.Trim();
                    memo.DeviceName = txtDeviceName.Text.Trim();
                    memo.Brand = txtBrand.Text.Trim();
                    memo.DeviceModel = txtDeviceModel.Text.Trim();
                    memo.SerialNumber = txtSerialNumber.Text.Trim();
                    memo.Accessories = txtAccessories.Text.Trim();
                    memo.IssueDescription = txtComplaint.Text.Trim();
                    memo.Diagnostics = txtDiagnostics.Text.Trim();
                    memo.EstimatedCost = estCost;
                    memo.Status = ((System.Windows.Controls.ComboBoxItem?)cmbStatus.SelectedItem)?.Content?.ToString() ?? "Pending";
                    memo.IsRepeatedDevice = _isRepeatedDevice;
                    memo.UpdatedAt = DateTime.Now;

                    // Handle image updates
                    List<string> localPaths = new List<string>();
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "Images");
                    Directory.CreateDirectory(dir);

                    for (int i = 0; i < _currentImagePaths.Count; i++)
                    {
                        string path = _currentImagePaths[i];
                        if (path.StartsWith(dir))
                        {
                            // Already in local storage, keep as is
                            localPaths.Add(path);
                        }
                        else
                        {
                            // New image, copy it
                            try
                            {
                                string ext = Path.GetExtension(path);
                                string localPath = Path.Combine(dir, $"{_memoNumber}_{i}_{DateTime.Now.Ticks}{ext}");
                                File.Copy(path, localPath, true);
                                localPaths.Add(localPath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error updating image: " + ex.Message);
                            }
                        }
                    }
                    memo.ImagePath = string.Join("|", localPaths);

                    db.SaveChanges();
                    WasUpdated = true;

                    // P2P LAN Broadcast
                    if (SettingsManager.Default.SyncMode != "InternetOnly")
                    {
                        _ = LanSyncService.BroadcastMemoSavedAsync(memo);
                    }
                }
            }

            if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
            {
                _ = CloudSyncService.SyncWithCloudAsync();
            }

            if (sender is System.Windows.Controls.Button && ((System.Windows.Controls.Button)sender).Content.ToString() == "Save Changes")
            {
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
