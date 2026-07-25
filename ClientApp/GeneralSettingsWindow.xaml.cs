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

        private async void ForcePush_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to FORCE PUSH your local database to Supabase Cloud?\n\nThis will replace cloud records with your local records.", "Confirm Force Push", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    int count = await CloudSyncService.ForcePushAllLocalToCloudAsync();
                    MessageBox.Show($"Force Push Completed Successfully! Pushed {count} order(s) to Cloud.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Force Push error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ForcePull_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to FORCE PULL all records from Supabase Cloud to this device?\n\nThis will update your local database with cloud records.", "Confirm Force Pull", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    int count = await CloudSyncService.ForcePullAllCloudToLocalAsync();
                    MessageBox.Show($"Force Pull Completed Successfully! Pulled {count} order(s) to Local.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (Application.Current.MainWindow is MainWindow mainWin)
                    {
                        mainWin.LoadData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Force Pull error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenCloudSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new CloudSettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.IsAutoBackupEnabled = chkAutoBackup.IsChecked ?? true;
            if (cmbBackupInterval.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Tag?.ToString(), out int interval))
            {
                SettingsManager.Default.AutoBackupIntervalMinutes = interval;
            }
            SettingsManager.Save();
            this.Close();
        }
    }
}
