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

            // 1. Check if an update is already downloaded and ready to install
            if (SettingsManager.Default.IsUpdateReady && 
                SettingsManager.Default.SkipUpdateVersion != SettingsManager.Default.UpdateReadyVersion)
            {
                bool showUpdateWindow = true;
                var currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVer != null && Version.TryParse(SettingsManager.Default.UpdateReadyVersion, out Version? readyVer))
                {
                    if (readyVer <= currentVer)
                    {
                        // The installed version is already equal to or newer than the downloaded update. Discard the update.
                        SettingsManager.Default.IsUpdateReady = false;
                        SettingsManager.Default.UpdateReadyVersion = string.Empty;
                        SettingsManager.Save();
                        showUpdateWindow = false;
                    }
                }
                
                if (showUpdateWindow)
                {
                    var updateWindow = new UpdateNotificationWindow(
                        SettingsManager.Default.UpdateReadyVersion,
                        SettingsManager.Default.UpdateReadyType,
                        SettingsManager.Default.UpdateReadyChangelog,
                        SettingsManager.Default.UpdateReadyPaymentAmount,
                        SettingsManager.Default.UpdateReadyCompulsory
                    );

                    updateWindow.ShowDialog();

                    // If the user selected to update, the app was already shutdown inside the window.
                    // If they clicked "Do it later", we continue to the main application.
                    if (SettingsManager.Default.UpdateReadyCompulsory)
                    {
                        // If it was compulsory and they closed the window without updating, exit the app.
                        Shutdown();
                        return;
                    }
                }
            }

            // 2. Initialize silent background updates check
            UpdateManager.Instance.DownloadCompleted += (version) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"A new software update (Version {version}) has been downloaded successfully in the background.\n\nWould you like to restart the application now to apply the update?",
                        "Software Update Ready",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var fileUrl = SettingsManager.Default.UpdateReadyFileUrl ?? "";
                        bool isSetupInstaller = fileUrl.Contains("_setup_", StringComparison.OrdinalIgnoreCase);

                        // Reset update ready flags
                        SettingsManager.Default.IsUpdateReady = false;
                        SettingsManager.Default.UpdateReadyVersion = string.Empty;
                        SettingsManager.Default.UpdateReadyChangelog = string.Empty;
                        SettingsManager.Default.UpdateReadyType = "minor";
                        SettingsManager.Default.UpdateReadyPaymentAmount = 0.00;
                        SettingsManager.Default.UpdateReadyFileUrl = string.Empty;
                        SettingsManager.Default.UpdateReadyCompulsory = false;
                        SettingsManager.Save();

                        var targetExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                        var baseDirectory = Path.GetDirectoryName(targetExe) ?? AppDomain.CurrentDomain.BaseDirectory;
                        var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "updates");
                        var exeSource = Path.Combine(tempPath, $"update_v{version}.exe");

                        if (isSetupInstaller && File.Exists(exeSource))
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = exeSource,
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);
                            Application.Current.Shutdown();
                            return;
                        }

                        // Launch the installer batch script
                        var batPath = Path.Combine(baseDirectory, "install-update.bat");

                        string copyCommand = ":: Simulated file copy copy/overwrite";
                        if (File.Exists(exeSource))
                        {
                            copyCommand = $@"echo Copying new application executable...
copy /y ""{exeSource}"" ""{targetExe}""
if errorlevel 1 (
    echo Error: Failed to copy the new update file.
    pause
)";
                        }

                        string scriptContent = $@"@echo off
title Job Order Generator Updater
echo ============================================
echo      JOB ORDER GENERATOR AUTO-UPDATER      
echo ============================================
echo.
echo Waiting for parent application to exit...
timeout /t 2 /nobreak > nul

echo.
echo Applying application binary patches...
{copyCommand}
echo v{version} update successfully applied!

echo.
echo Restarting application...
timeout /t 1 /nobreak > nul
start """" ""{targetExe}""

:: Self-destruct script
del ""%~f0""
exit
";
                        File.WriteAllText(batPath, scriptContent);
                        var startInfoBat = new ProcessStartInfo
                        {
                            FileName = batPath,
                            UseShellExecute = true,
                            CreateNoWindow = false,
                            WindowStyle = ProcessWindowStyle.Normal
                        };
                        Process.Start(startInfoBat);
                        Current.Shutdown();
                    }
                });
            };

            // Start silent check in background thread
            _ = Task.Run(async () =>
            {
                var update = await UpdateManager.Instance.CheckForUpdatesAsync();
                if (update != null && SettingsManager.Default.UpdateReadyVersion != update.Version)
                {
                    await UpdateManager.Instance.StartDownloadAsync(update);
                }
            });

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
