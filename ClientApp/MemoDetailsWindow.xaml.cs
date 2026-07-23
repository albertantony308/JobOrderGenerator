using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClientApp.Data;
using ClientApp.Models;
using ClientApp.Services;
using Microsoft.EntityFrameworkCore;

namespace ClientApp
{
    public partial class MemoDetailsWindow : Window
    {
        private int _memoId;
        private ServiceMemo? _memo;
        private List<string> _images = new List<string>();
        private int _currentImageIndex = 0;
        public bool NeedsRefresh { get; private set; } = false;
        public bool NeedsBrandingNavigation { get; private set; } = false;

        private System.Collections.ObjectModel.ObservableCollection<CostItem> _costItems = new System.Collections.ObjectModel.ObservableCollection<CostItem>();
        private bool _isCostItemsDirty = false;
        private bool _isLoadingCosts = false;

        public MemoDetailsWindow(int memoId)
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            _memoId = memoId;
            
            dgCostItems.ItemsSource = _costItems;
            _costItems.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CostItem item in e.NewItems)
                        item.PropertyChanged += CostItem_PropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (CostItem item in e.OldItems)
                        item.PropertyChanged -= CostItem_PropertyChanged;
                }
                if (!_isLoadingCosts)
                {
                    _isCostItemsDirty = true;
                }
                RecalculateTotal();
            };

            LoadMemo();

            CloudSyncService.SyncCompleted += OnCloudSyncCompleted;
            this.Closed += (s, e) => {
                CloudSyncService.SyncCompleted -= OnCloudSyncCompleted;
            };
        }

        private void OnCloudSyncCompleted()
        {
            this.Dispatcher.Invoke(() =>
            {
                LoadMemo();
            });
        }

        private void LoadMemo()
        {
            using (var db = new LocalDbContext())
            {
                _memo = db.ServiceMemos.FirstOrDefault(m => m.Id == _memoId);
                if (_memo == null) { Close(); return; }
                txtMemoNumber.Text = _memo.MemoNumber;
                txtDate.Text = _memo.CreatedAt.ToString("MMMM dd, yyyy HH:mm");
                txtCustomerName.Text = _memo.CustomerName;
                txtPhoneNumber.Text = _memo.PhoneNumber;
                txtCustomerAddress.Text = string.IsNullOrEmpty(_memo.CustomerAddress) ? "No address provided." : _memo.CustomerAddress;
                
                txtPhone2.Text = string.IsNullOrEmpty(_memo.Phone2) ? "" : "Phone 2: " + _memo.Phone2;
                txtTechnician.Text = string.IsNullOrEmpty(_memo.TechnicianName) ? "Unassigned" : _memo.TechnicianName;
                AlternativeContactsPanel.Visibility = string.IsNullOrEmpty(_memo.Phone2) ? Visibility.Collapsed : Visibility.Visible;
                TechnicianPanel.Visibility = string.IsNullOrEmpty(_memo.TechnicianName) ? Visibility.Collapsed : Visibility.Visible;

                txtDeviceName.Text = _memo.DeviceName;
                txtBrand.Text = string.IsNullOrEmpty(_memo.Brand) ? "N/A" : _memo.Brand;
                txtDeviceModel.Text = string.IsNullOrEmpty(_memo.DeviceModel) ? "N/A" : _memo.DeviceModel;
                txtSerialNumber.Text = string.IsNullOrEmpty(_memo.SerialNumber) ? "N/A" : _memo.SerialNumber;
                txtAccessories.Text = string.IsNullOrEmpty(_memo.Accessories) ? "No accessories listed." : _memo.Accessories;

                txtIssue.Text = _memo.IssueDescription;
                txtDiagnostics.Text = string.IsNullOrEmpty(_memo.Diagnostics) ? "No diagnostics recorded yet." : _memo.Diagnostics;
                if (!txtOrderUpdates.IsFocused)
                {
                    txtOrderUpdates.Text = _memo.OrderUpdates ?? "";
                }
                _isLoadingCosts = true;
                if (!_isCostItemsDirty)
                {
                    _costItems.Clear();
                    if (!string.IsNullOrEmpty(_memo.ItemizedCosts))
                    {
                        try
                        {
                            var items = System.Text.Json.JsonSerializer.Deserialize<List<CostItem>>(_memo.ItemizedCosts);
                            if (items != null)
                            {
                                foreach (var item in items)
                                {
                                    _costItems.Add(item);
                                }
                            }
                        }
                        catch { }
                    }

                    if (_costItems.Count == 0 && _memo.EstimatedCost > 0)
                    {
                        _costItems.Add(new CostItem { Description = "Repair Work / Service Charge", Cost = _memo.EstimatedCost });
                    }
                }
                RecalculateTotal();
                _isLoadingCosts = false;

                // Return date
                SetReturnDateDisplay(_memo.ReturnDate);

                // Clean up corrupted status
                if (!string.IsNullOrEmpty(_memo.Status) && _memo.Status.StartsWith("System.Windows.Controls.ComboBoxItem: "))
                {
                    _memo.Status = _memo.Status.Replace("System.Windows.Controls.ComboBoxItem: ", "");
                    db.SaveChanges();
                }

                // Set Status (Handling strings instead of ComboBoxItems)
                if (!cmbStatus.IsFocused && !cmbStatus.IsDropDownOpen)
                {
                    cmbStatus.SelectedItem = _memo.Status;
                }

                // Load Images
                if (!string.IsNullOrEmpty(_memo.ImagePath))
                {
                    _images = _memo.ImagePath.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (_images.Count > 0)
                    {
                        sectionPhotos.Visibility = Visibility.Visible;
                        UpdateImageDisplay();
                    }
                    else
                    {
                        sectionPhotos.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    sectionPhotos.Visibility = Visibility.Collapsed;
                }

                // Show repeated device info badge
                if (_memo.IsRepeatedDevice)
                {
                    RepeatedDeviceBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    RepeatedDeviceBadge.Visibility = Visibility.Collapsed;
                }

                // WhatsApp Section Initialization
                UpdateWhatsAppSection(_memo.Status);
            }
        }

        private void UpdateImageDisplay()
        {
            if (_images.Count > 0)
            {
                string path = _images[_currentImageIndex];
                try { imgProduct.Source = new BitmapImage(new Uri(path)); } catch { }
                txtImageCounter.Text = $"{_currentImageIndex + 1} of {_images.Count}";
                btnPrev.IsEnabled = _currentImageIndex > 0;
                btnNext.IsEnabled = _currentImageIndex < _images.Count - 1;
            }
        }

        private void SaveReturnDate(DateTime? date)
        {
            if (_memo == null) return;
            try
            {
                using (var db = new LocalDbContext())
                {
                    var m = db.ServiceMemos.Find(_memoId);
                    if (m != null)
                    {
                        m.ReturnDate = date;
                        var nowUtc = NetworkTimeService.GetUtcNow();
                        m.UpdatedAt = nowUtc > m.UpdatedAt ? nowUtc : m.UpdatedAt.AddSeconds(1);
                        db.Entry(m).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        db.SaveChanges();
                        _memo.ReturnDate = date;
                        _memo.UpdatedAt = m.UpdatedAt;
                        NeedsRefresh = true;
                    }
                }
                SetReturnDateDisplay(date);
                if (SettingsManager.Default.IsCloudSyncEnabled)
                    _ = CloudSyncService.SyncWithCloudAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReturnDate save error: {ex.Message}");
            }
        }

        private void SetReturnDateDisplay(DateTime? date)
        {
            var normalBackground = System.Windows.Media.Brushes.Transparent;
            var normalForeground = FindResource("OnSurfaceBrush") as System.Windows.Media.Brush;
            var selectedBackground = FindResource("PrimaryBrush") as System.Windows.Media.Brush;
            var selectedForeground = FindResource("OnPrimaryBrush") as System.Windows.Media.Brush;

            btnToday.Background = normalBackground;
            btnToday.Foreground = normalForeground;
            btnToggleCalendar.Background = normalBackground;
            btnToggleCalendar.Foreground = normalForeground;

            if (date.HasValue)
            {
                txtReturnDateDisplay.Text = date.Value.ToString("dd MMM yyyy");
                txtReturnDateDisplay.FontWeight = FontWeights.Bold;
                txtReturnDateDisplay.Opacity = 1.0;
                calReturnDate.SelectedDate = date.Value;

                DateTime targetDate = date.Value.Date;
                if (targetDate == DateTime.Today.Date)
                {
                    btnToday.Background = selectedBackground;
                    btnToday.Foreground = selectedForeground;
                }
                else
                {
                    btnToggleCalendar.Background = selectedBackground;
                    btnToggleCalendar.Foreground = selectedForeground;
                }
            }
            else
            {
                txtReturnDateDisplay.Text = "Not Enrolled";
                txtReturnDateDisplay.FontWeight = FontWeights.SemiBold;
                txtReturnDateDisplay.Opacity = 0.55;
                calReturnDate.SelectedDate = null;
            }
        }

        private void QuickDate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                int offset = int.Parse(btn.Tag.ToString() ?? "0");
                DateTime selected = DateTime.Today.AddDays(offset);
                SaveReturnDate(selected);
                if (popCalendar != null)
                {
                    popCalendar.IsOpen = false;
                }
            }
        }

        private void CalendarDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (calReturnDate.SelectedDate.HasValue)
            {
                SaveReturnDate(calReturnDate.SelectedDate.Value);
                if (popCalendar != null)
                {
                    popCalendar.IsOpen = false;
                }
            }
        }

        private void ClearReturnDate_Click(object sender, RoutedEventArgs e)
        {
            SaveReturnDate(null);
            if (popCalendar != null)
            {
                popCalendar.IsOpen = false;
            }
        }

        private void btnToggleCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (popCalendar != null)
            {
                popCalendar.IsOpen = !popCalendar.IsOpen;
            }
        }



        private void Status_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_memo == null || cmbStatus.SelectedItem == null) return;
            
            string newStatus = cmbStatus.SelectedItem.ToString() ?? "Pending";
            
            if (_memo.Status != newStatus)
            {
                using (var db = new LocalDbContext())
                {
                    var m = db.ServiceMemos.Find(_memoId);
                    if (m != null)
                    {
                        m.Status = newStatus;
                        var nowUtc = NetworkTimeService.GetUtcNow();
                        m.UpdatedAt = nowUtc > m.UpdatedAt ? nowUtc : m.UpdatedAt.AddSeconds(1);
                        db.Entry(m).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        db.SaveChanges();
                        _memo.Status = newStatus;
                        _memo.UpdatedAt = m.UpdatedAt;
                        NeedsRefresh = true;
                    }
                }
                UpdateWhatsAppSection(newStatus);
                if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
                {
                    _ = CloudSyncService.SyncWithCloudAsync();
                }
            }
        }

        private void ToggleWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            if (whatsappMessagePanel.Visibility == Visibility.Visible)
            {
                whatsappMessagePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                whatsappMessagePanel.Visibility = Visibility.Visible;
            }
        }

        private void UpdateWhatsAppSection(string status)
        {
            if (status == "Completed" && _memo != null)
            {
                whatsappSection.Visibility = Visibility.Visible;
                if (!txtWhatsAppMessage.IsFocused)
                {
                    if (string.IsNullOrEmpty(txtWhatsAppMessage.Text) || !txtWhatsAppMessage.Text.Contains(_memo.CustomerName))
                    {
                        txtWhatsAppMessage.Text = $"Hi {_memo.CustomerName}, your {_memo.DeviceName} is now ready. Please come and collect it.";
                    }
                }
            }
            else
            {
                whatsappSection.Visibility = Visibility.Collapsed;
                if (whatsappMessagePanel != null)
                {
                    whatsappMessagePanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SendWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            if (_memo == null) return;
            
            string message = txtWhatsAppMessage.Text;
            string phone = _memo.PhoneNumber;

            // Clean phone number: keep only digits
            string cleanPhone = new string(phone.Where(char.IsDigit).ToArray());
            
            // If it doesn't start with a country code and is likely a local number, 
            // you might want to add a default country code, but for now we'll use what's provided.
            // WhatsApp Web expects the number with country code but no +.
            
            string url = $"https://web.whatsapp.com/send?phone={cleanPhone}&text={Uri.EscapeDataString(message)}";
            
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open WhatsApp: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex > 0) { _currentImageIndex--; UpdateImageDisplay(); }
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex < _images.Count - 1) { _currentImageIndex++; UpdateImageDisplay(); }
        }

        private void Image_Click(object sender, MouseButtonEventArgs e)
        {
            if (imgProduct.Source != null)
            {
                imgFull.Source = imgProduct.Source;
                imgPopup.Visibility = Visibility.Visible;
            }
        }

        private void ClosePopup_Click(object sender, MouseButtonEventArgs e) => imgPopup.Visibility = Visibility.Collapsed;
        private void ClosePopup_Click(object sender, RoutedEventArgs e) => imgPopup.Visibility = Visibility.Collapsed;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void RefreshDetails_Click(object sender, RoutedEventArgs e)
        {
            // 1. Reload local details from SQLite DB
            LoadMemo();

            // 2. Perform real-time cloud sync in the background if enabled
            if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
            {
                try
                {
                    await CloudSyncService.SyncWithCloudAsync();
                    // 3. Reload again on UI thread to pull in any synced changes
                    LoadMemo();
                    NeedsRefresh = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Details real-time refresh exception: {ex.Message}");
                }
            }

            MessageBox.Show("Details refreshed successfully.", "Refreshed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EditRequestWindow(_memoId);
            editWindow.Owner = this;
            editWindow.ShowDialog();
            if (editWindow.RequestNavigationToBranding)
            {
                this.NeedsBrandingNavigation = true;
                this.Close();
                return;
            }
            if (editWindow.WasUpdated) { LoadMemo(); NeedsRefresh = true; }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_memo == null) return;
            var printWindow = new PrintPreviewWindow(_memo);
            printWindow.Owner = this;
            printWindow.ShowDialog();
            if (printWindow.RequestNavigationToBranding)
            {
                this.NeedsBrandingNavigation = true;
                this.Close();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isCostItemsDirty || (txtOrderUpdates != null && txtOrderUpdates.Text.Trim() != (_memo?.OrderUpdates ?? "").Trim()))
            {
                SaveOrderUpdatesAndCosts(false);
            }
            base.OnClosing(e);
        }

        private void txtOrderUpdates_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                UpdateOrder_Click(sender, e);
            }
        }

        private void UpdateOrder_Click(object sender, RoutedEventArgs e)
        {
            SaveOrderUpdatesAndCosts(true);
        }

        private bool SaveOrderUpdatesAndCosts(bool showMessage = false)
        {
            if (_memo == null) return false;
            try
            {
                try
                {
                    dgCostItems.CommitEdit(DataGridEditingUnit.Cell, true);
                    dgCostItems.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch { }

                string updatesText = txtOrderUpdates.Text.Trim();
                var costItemsList = _costItems.ToList();
                string costJson = System.Text.Json.JsonSerializer.Serialize(costItemsList);
                decimal totalCost = costItemsList.Sum(i => i.Cost);

                using (var db = new LocalDbContext())
                {
                    var m = db.ServiceMemos.Find(_memoId);
                    if (m != null)
                    {
                        m.OrderUpdates = updatesText;
                        m.ItemizedCosts = costJson;
                        m.EstimatedCost = totalCost;
                        var nowUtc = NetworkTimeService.GetUtcNow();
                        m.UpdatedAt = nowUtc > m.UpdatedAt ? nowUtc : m.UpdatedAt.AddSeconds(1);
                        db.Entry(m).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        db.SaveChanges();
                        _isCostItemsDirty = false;
                        
                        _memo.OrderUpdates = updatesText;
                        _memo.ItemizedCosts = costJson;
                        _memo.EstimatedCost = totalCost;
                        _memo.UpdatedAt = m.UpdatedAt;
                        NeedsRefresh = true;
                        
                        if (showMessage)
                        {
                            MessageBox.Show("Order updates and costs saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
                {
                    _ = CloudSyncService.SyncWithCloudAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                if (showMessage)
                {
                    MessageBox.Show("Error saving order updates: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return false;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to permanently delete this job order? This action cannot be undone.", 
                "Delete Job Order", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ServiceMemo memoToDelete;
                    using (var db = new LocalDbContext())
                    {
                        memoToDelete = db.ServiceMemos.Find(_memoId);
                        if (memoToDelete != null)
                        {
                            memoToDelete.Status = "Deleted";
                            memoToDelete.UpdatedAt = NetworkTimeService.GetUtcNow();
                            db.Entry(memoToDelete).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            db.ServiceMemos.Update(memoToDelete);
                            db.SaveChanges();
                        }
                    }
                    
                    NeedsRefresh = true;
                    
                    if (memoToDelete != null)
                    {
                        // 1. Broadcast delete tombstone to other LAN devices
                        _ = Task.Run(() => LanSyncService.BroadcastMemoSavedAsync(memoToDelete));

                        // 2. Sync tombstone to Supabase Cloud
                        if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly" && SupabaseClientManager.IsConfigured)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await CloudSyncService.SyncWithCloudAsync();
                                }
                                catch { }
                            });
                        }
                    }
                    
                    MessageBox.Show("Job order deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting job order: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddCostItem_Click(object sender, RoutedEventArgs e)
        {
            _costItems.Add(new CostItem { Description = "New Item", Cost = 0.00m });
        }

        private void RemoveCostItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgCostItems.SelectedItem is CostItem selected)
            {
                _costItems.Remove(selected);
            }
        }

        private void CostItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isLoadingCosts)
            {
                _isCostItemsDirty = true;
            }
            if (e.PropertyName == nameof(CostItem.Cost) || e.PropertyName == nameof(CostItem.Description))
            {
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            decimal total = _costItems.Sum(i => i.Cost);
            txtTotalCost.Text = $"Rs. {total:N2}";
        }
    }
}
