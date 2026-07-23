using System.Windows;
using ClientApp.Services;

namespace ClientApp
{
    public partial class CloudSettingsWindow : Window
    {
        public CloudSettingsWindow()
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            _ = LoadSettingsAsync();
        }

        private async System.Threading.Tasks.Task LoadSettingsAsync()
        {
            // If subscription key is empty but we have an active license, auto-import it!
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
            {
                var licenseManager = new LicenseManager();
                if (await licenseManager.IsLicenseValidAsync())
                {
                    var info = await licenseManager.GetCurrentLicenseInfoAsync();
                    if (!string.IsNullOrEmpty(info.key))
                    {
                        var (valid, email) = await licenseManager.VerifySubscriptionAsync(info.key);
                        if (valid)
                        {
                            SettingsManager.Default.SubscriptionKey = info.key;
                            SettingsManager.Default.CloudUserEmail = email;
                            SettingsManager.Default.IsCloudSyncEnabled = true;
                            
                            bool hasCloud = info.cloudSyncEnabled;
                            if (hasCloud)
                            {
                                SettingsManager.Default.IsCloudSyncEnabled = true;
                                SettingsManager.Default.SyncMode = "Hybrid";
                            }
                            else
                            {
                                SettingsManager.Default.IsCloudSyncEnabled = false;
                                SettingsManager.Default.SyncMode = "LocalOnly";
                            }

                            SettingsManager.Save();
                            
                            // Initialize Supabase client
                            SupabaseClientManager.Initialize();
                        }
                    }
                }
            }

            txtSubscriptionKey.Text = SettingsManager.Default.SubscriptionKey;
            chkEnableSync.IsChecked = SettingsManager.Default.IsCloudSyncEnabled;
            chkSyncImages.IsChecked = SettingsManager.Default.SyncImagesEnabled;
            
            if (SettingsManager.Default.IsCloudSyncEnabled && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
            {
                await ShowConfigAsync(SettingsManager.Default.CloudUserEmail);
            }
            else
            {
                ShowIntro();
            }
        }

        private void ShowIntro()
        {
            IntroSection.Visibility = Visibility.Visible;
            ActivationSection.Visibility = Visibility.Collapsed;
            ConfigSection.Visibility = Visibility.Collapsed;
        }

        private void ShowActivation()
        {
            IntroSection.Visibility = Visibility.Collapsed;
            ActivationSection.Visibility = Visibility.Visible;
            ConfigSection.Visibility = Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task ShowConfigAsync(string email)
        {
            IntroSection.Visibility = Visibility.Collapsed;
            ActivationSection.Visibility = Visibility.Collapsed;
            ConfigSection.Visibility = Visibility.Visible;

            if (borderConfigCloudOffline != null)
            {
                borderConfigCloudOffline.Visibility = CloudSyncService.IsCloudOffline ? Visibility.Visible : Visibility.Collapsed;
            }

            lblActiveUser.Text = email;

            try
            {
                var licenseManager = new LicenseManager();
                var (key, expiresAt, activeDevices, maxDevices, cloudSyncEnabled, cloudStorageLimitGb, cloudStorageUsedMb, planName) = await licenseManager.GetCurrentLicenseInfoAsync();

                lblActivePlan.Text = $"{planName} Subscription";
                lblBadgeText.Text = planName.ToUpper();

                // Storage used math (converting MB to GB)
                double usedGb = cloudStorageUsedMb / 1024.0;
                double totalGb = cloudStorageLimitGb;
                
                // Safe default storage capacity
                if (totalGb <= 0) totalGb = 5.0;

                double percentUsed = (usedGb / totalGb) * 100.0;
                if (percentUsed > 100.0) percentUsed = 100.0;
                if (percentUsed < 0.0) percentUsed = 0.0;

                if (totalGb < 1.0)
                {
                    lblStorageUsedText.Text = $"{cloudStorageUsedMb:F2} MB of {totalGb * 1024.0:F0} MB used";
                }
                else
                {
                    lblStorageUsedText.Text = $"{usedGb:F2} GB of {totalGb:F1} GB used";
                }
                pbStorageUsed.Value = percentUsed;
                
                double remainingPercent = 100.0 - percentUsed;
                lblStorageRemainingPercent.Text = $"{remainingPercent:F0}% Left";

                // Device seats
                string maxDevStr = maxDevices == -1 ? "Unlimited" : maxDevices.ToString();
                lblDeviceSeats.Text = $"{activeDevices} of {maxDevStr}";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowConfigAsync Exception: {ex.Message}");
                // Dynamic UI fallbacks
                lblActivePlan.Text = "Standard Cloud Sync";
                lblBadgeText.Text = "ACTIVE";
                lblStorageUsedText.Text = "0.00 GB of 5.0 GB used";
                pbStorageUsed.Value = 0;
                lblStorageRemainingPercent.Text = "100% Left";
                lblDeviceSeats.Text = "1 of 3";
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void ActivateCloud_Click(object sender, RoutedEventArgs e)
        {
            var licenseManager = new LicenseManager();
            var info = await licenseManager.GetCurrentLicenseInfoAsync();
            string key = info.key ?? "";
            
            try
            {
                string url = $"https://servicememomanager.com/cloud-activate?key={System.Uri.EscapeDataString(key)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Could not open activation portal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartActivation_Click(object sender, RoutedEventArgs e)
        {
            ShowActivation();
        }

        private void BackToIntro_Click(object sender, RoutedEventArgs e)
        {
            ShowIntro();
        }

        private async void Verify_Click(object sender, RoutedEventArgs e)
        {
            var key = txtSubscriptionKey.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Please enter a subscription key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var currentKey = SettingsManager.Default.SubscriptionKey;
            bool isSwitching = !string.IsNullOrEmpty(currentKey) && currentKey != key;

            if (isSwitching)
            {
                var confirmWin = new ConfirmSwitchWindow { Owner = this };
                if (confirmWin.ShowDialog() != true)
                {
                    lblStatus.Text = "Cancelled.";
                    return;
                }
            }

            lblStatus.Text = "Verifying...";
            var licenseManager = new LicenseManager();
            
            // Call ActivateOnlineAsync instead of VerifySubscriptionAsync to register the device seat
            var (success, keyId, errorMsg) = await licenseManager.ActivateOnlineAsync(key);

            if (success)
            {
                var (valid, email) = await licenseManager.VerifySubscriptionAsync(key);
                if (valid)
                {
                    // Clear database if switching keys, OR if there are any leftover records of another key
                    try
                    {
                        using (var db = new Data.LocalDbContext())
                        {
                            var nonMatching = db.ServiceMemos.Where(m => m.CloudOwnerKey != key).ToList();
                            if (isSwitching || nonMatching.Any())
                            {
                                db.ServiceMemos.RemoveRange(db.ServiceMemos);
                                db.SaveChanges();
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error clearing local db on activation: {ex.Message}");
                    }

                    SettingsManager.Default.SubscriptionKey = key;
                    SettingsManager.Default.CloudUserEmail = email;
                    SettingsManager.Default.IsCloudSyncEnabled = true;

                    try
                    {
                        var info = await licenseManager.GetCurrentLicenseInfoAsync();
                        bool hasCloud = info.cloudSyncEnabled;
                        if (hasCloud)
                        {
                            SettingsManager.Default.IsCloudSyncEnabled = true;
                            SettingsManager.Default.SyncMode = "Hybrid";
                        }
                        else
                        {
                            SettingsManager.Default.IsCloudSyncEnabled = false;
                            SettingsManager.Default.SyncMode = "LocalOnly";
                        }
                    }
                    catch { }

                    SettingsManager.Save();

                    await ShowConfigAsync(email);
                    MessageBox.Show("Subscription verified and activated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    SupabaseClientManager.Initialize();

                    // Instantly trigger sync so that the offline status clears and data goes to cloud
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        await CloudSyncService.SyncWithCloudAsync();
                    });
                }
            }
            else
            {
                lblStatus.Text = "Invalid key. Please try again.";
                MessageBox.Show($"Verification/Activation failed: {errorMsg}", "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.IsCloudSyncEnabled = chkEnableSync.IsChecked ?? false;
            SettingsManager.Default.SyncImagesEnabled = chkSyncImages.IsChecked ?? false;
            SettingsManager.Save();
            this.Close();
        }

        private void Deactivate_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to deactivate cloud sync? Your key will be removed from this device.", "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.Default.SubscriptionKey = string.Empty;
                SettingsManager.Default.CloudUserEmail = string.Empty;
                SettingsManager.Default.IsCloudSyncEnabled = false;
                SettingsManager.Default.SyncMode = "LocalOnly";
                SettingsManager.Save();
                ShowIntro();
            }
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            var licenseManager = new LicenseManager();
            var info = await licenseManager.GetCurrentLicenseInfoAsync();
            string key = info.key ?? "";
            
            try
            {
                string url = $"https://servicememomanager.com/cloud-activate?key={System.Uri.EscapeDataString(key)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Could not open activation portal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
