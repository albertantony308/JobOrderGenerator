using System.Windows;
using System.Windows.Controls;
using ClientApp.Services;

namespace ClientApp
{
    public partial class GeneralSettingsWindow : Window
    {
        public GeneralSettingsWindow()
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            chkDarkMode.IsChecked = SettingsManager.Default.IsDarkMode;
            chkAutoBackup.IsChecked = SettingsManager.Default.IsAutoBackupEnabled;
            chkFullyOfflineModeGeneral.IsChecked = SettingsManager.Default.IsFullyOfflineMode;

            // Set backup interval ComboBox
            int interval = SettingsManager.Default.AutoBackupIntervalMinutes;
            foreach (ComboBoxItem item in cmbBackupInterval.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int val) && val == interval)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            txtVersion.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.SetTheme(chkDarkMode.IsChecked ?? false);
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            btnCheckUpdates.IsEnabled = false;
            txtUpdateCheckStatus.Text = "Checking server for available updates...";
            txtUpdateCheckStatus.Visibility = Visibility.Visible;

            // Clear skipped version preference so manual check always presents available updates
            SettingsManager.Default.SkipUpdateVersion = string.Empty;
            SettingsManager.Save();

            try
            {
                var update = await UpdateManager.Instance.CheckForUpdatesAsync();
                if (update != null)
                {
                    txtUpdateCheckStatus.Text = $"Version {update.Version} is available!";
                    btnCheckUpdates.IsEnabled = true;

                    var win = new UpdateNotificationWindow(update);
                    win.Owner = this;
                    win.ShowDialog();
                }
                else
                {
                    txtUpdateCheckStatus.Text = "You are running the latest version.";
                    btnCheckUpdates.IsEnabled = true;
                }
            }
            catch (System.Exception ex)
            {
                txtUpdateCheckStatus.Text = "Could not connect to update server.";
                btnCheckUpdates.IsEnabled = true;
            }
        }

        private void ExportActiveOrders_Click(object sender, RoutedEventArgs e)
        {
            BackupManager.ExportActiveOrdersBackup();
        }

        private void ImportBackup_Click(object sender, RoutedEventArgs e)
        {
            BackupManager.ImportBackup();
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.LoadData();
            }
        }

        private async void ForceLocalPush_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to FORCE PUSH your local database to all connected LAN peer computers on your Wi-Fi network?", "Confirm Local Push", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    int count = await LanSyncService.ForceLocalPushToAllPeersAsync();
                    MessageBox.Show($"Successfully pushed local database to {count} LAN record batch(es) across local computers.", "LAN Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Force Local Push error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ForceLocalPull_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to FORCE PULL all records from all connected LAN peer computers on your Wi-Fi network?", "Confirm Local Pull", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    int count = await LanSyncService.ForceLocalPullFromAllPeersAsync();
                    MessageBox.Show($"Successfully pulled latest data from {count} LAN peer computer(s).", "LAN Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (Application.Current.MainWindow is MainWindow mainWin)
                    {
                        mainWin.LoadData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Force Local Pull error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenCloudSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new CloudSettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            bool wasOffline = SettingsManager.Default.IsFullyOfflineMode;
            bool isOfflineNow = chkFullyOfflineModeGeneral.IsChecked ?? false;

            SettingsManager.Default.IsAutoBackupEnabled = chkAutoBackup.IsChecked ?? true;
            if (cmbBackupInterval.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Tag?.ToString(), out int interval))
            {
                SettingsManager.Default.AutoBackupIntervalMinutes = interval;
            }

            SettingsManager.Default.IsFullyOfflineMode = isOfflineNow;
            SettingsManager.Save();

            // When user turns OFF Fully Offline Mode and returns to Cloud Mode
            if (wasOffline && !isOfflineNow && SettingsManager.Default.IsCloudSyncEnabled)
            {
                var result = MessageBox.Show(
                    "You are turning OFF Fully Offline Mode and reconnecting to Cloud Sync.\n\n" +
                    "Would you like to mark THIS computer as the PRIMARY SOURCE DEVICE?\n\n" +
                    "• CLICK [YES]: All local records from this computer will be force-pushed to Supabase Cloud, replacing/updating cloud data for all other devices on your activation key.\n\n" +
                    "• CLICK [NO]: Reconnect normally and pull current data from the Cloud.",
                    "Primary Source Device Reconciliation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        SettingsManager.Default.IsPrimaryCloudSourceDevice = true;
                        SettingsManager.Save();

                        int pushed = await CloudSyncService.ForcePushAllLocalToCloudAsync();
                        MessageBox.Show($"Primary Source Device Reconciliation Complete!\n\nPushed {pushed} order(s) to Supabase Cloud.", "Reconciliation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not complete Cloud reconciliation: {ex.Message}", "Reconciliation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    SettingsManager.Default.IsPrimaryCloudSourceDevice = false;
                    SettingsManager.Save();
                }
            }

            this.Close();
        }
    }
}
