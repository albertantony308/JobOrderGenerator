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
