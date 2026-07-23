using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System.Diagnostics;
using ClientApp.Services;

namespace ClientApp;

public partial class App : Application
{
    public App()
    {
        // Register global class handler for Window.Loaded event to automatically assign the application icon to all windows.
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(Window_Loaded));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window && window.Icon == null)
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/app_icon.png", UriKind.RelativeOrAbsolute);
                window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }
    }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // Prevent the app from launching if the installer or update script is currently running
        var runningProcesses = System.Diagnostics.Process.GetProcesses();
        bool isUpdaterRunning = runningProcesses.Any(p => 
            p.ProcessName.Contains("JobOrderGenerator_Setup_", StringComparison.OrdinalIgnoreCase) || 
            (p.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase) && p.MainWindowTitle.Contains("Job Order Generator Updater", StringComparison.OrdinalIgnoreCase))
        );

        if (isUpdaterRunning)
        {
            MessageBox.Show("An update is currently being applied. Please wait until the setup is complete.", "Update in Progress", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            using (var db = new ClientApp.Data.LocalDbContext())
            {
                db.Migrate();
            }
            ThemeManager.Initialize();
            BackupManager.InitializeAutoBackup();
            LanSyncService.StartHttpApiServer();

            // 1. Check for available updates on startup
            try
            {
                var update = await UpdateManager.Instance.CheckForUpdatesAsync();
                if (update != null && SettingsManager.Default.SkipUpdateVersion != update.Version)
                {
                    var updateWindow = new UpdateNotificationWindow(update);
                    bool? dialogResult = updateWindow.ShowDialog();

                    if (update.IsCompulsory && dialogResult != true)
                    {
                        // User cancelled compulsory update - shutdown app
                        Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup update check failed: {ex.Message}");
            }

            // 2. Start periodic live update checks every 60 seconds for in-app notifications
            UpdateManager.Instance.StartPeriodicCheck(60);

            var licenseManager = new LicenseManager();
            var status = await licenseManager.VerifyLicenseStatusAsync();
            LicenseManager.CurrentStatus = status;

            if (status.IsValid)
            {
                if (status.ShowBigWarning && !string.IsNullOrEmpty(status.WarningMessage))
                {
                    MessageBox.Show(status.WarningMessage, "License Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

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
                    SettingsManager.Save();
                }
                catch { }

                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                if (!string.IsNullOrEmpty(status.WarningMessage))
                {
                    MessageBox.Show(status.WarningMessage, "License Required", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                var activationWindow = new ActivationWindow();
                activationWindow.Show();
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.InnerException?.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
