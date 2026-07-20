using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ClientApp.Services;

namespace ClientApp
{
    public partial class ConfirmSwitchWindow : Window
    {
        public ConfirmSwitchWindow()
        {
            InitializeComponent();
            LoadBackupStatus();
            LoadCloudStatus();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void LoadBackupStatus()
        {
            try
            {
                var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "backups");
                if (Directory.Exists(backupDir))
                {
                    var dirInfo = new DirectoryInfo(backupDir);
                    var files = dirInfo.GetFiles("*.json")
                                       .OrderByDescending(f => f.LastWriteTime)
                                       .ToList();

                    if (files.Any())
                    {
                        var newestFile = files.First();
                        lblBackupStatus.Text = $"✓ Backups found: {files.Count} local backups are available.";
                        lblBackupStatus.Foreground = System.Windows.Media.Brushes.MediumSeaGreen;
                        lblLastBackupTime.Text = $"Last backup: {newestFile.LastWriteTime:f}";
                        return;
                    }
                }

                // If no backups
                lblBackupStatus.Text = "⚠ Warning: No auto-backups found in the local folder. We recommend taking a backup from settings first.";
                lblBackupStatus.Foreground = System.Windows.Media.Brushes.Coral;
                lblLastBackupTime.Text = "You can manually export a backup before proceeding.";
            }
            catch (Exception ex)
            {
                lblBackupStatus.Text = "Could not check backup status: " + ex.Message;
                lblBackupStatus.Foreground = System.Windows.Media.Brushes.Coral;
            }
        }

        private void LoadCloudStatus()
        {
            // If cloud sync is not active/enabled, show the promo box to upgrade
            if (!SettingsManager.Default.IsCloudSyncEnabled)
            {
                CloudPromoBox.Visibility = Visibility.Visible;
            }
            else
            {
                CloudPromoBox.Visibility = Visibility.Collapsed;
            }
        }

        private void UpgradePlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate to the user portal dashboard website
                string url = "http://localhost:5173/";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open web browser: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Proceed_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
