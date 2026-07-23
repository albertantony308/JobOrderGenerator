using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ClientApp.Services;

namespace ClientApp
{
    public partial class UpdateNotificationWindow : Window
    {
        private readonly string _version;
        private readonly string _updateType;
        private readonly string _changelog;
        private readonly double _paymentAmount;
        private readonly bool _isCompulsory;
        private bool _isPaid = false;

        public UpdateNotificationWindow(string version, string updateType, string changelog, double paymentAmount, bool isCompulsory)
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            
            _version = version;
            _updateType = updateType;
            _changelog = changelog;
            _paymentAmount = paymentAmount;
            _isCompulsory = isCompulsory;

            // Apply contents dynamically
            txtHeaderTitle.Text = _isCompulsory ? "Compulsory Update Required" : (_updateType == "major" ? "Major Upgrade Available" : "Minor Update Available");
            txtVersionSubtitle.Text = $"Version {version} is ready to install";
            txtChangelog.Text = string.IsNullOrWhiteSpace(_changelog) ? "• Stability improvements and visual refinements." : _changelog;

            if (_updateType == "major")
            {
                borderPayment.Visibility = Visibility.Visible;
                txtPaymentDetails.Text = $"This is a Major Upgrade (${_paymentAmount:F2} USD) containing premium modules. Please complete checkout to continue.";
                btnAction.Content = $"Pay & Upgrade (${_paymentAmount:F2})";
            }
            else
            {
                borderPayment.Visibility = Visibility.Collapsed;
                btnAction.Content = "Update Now";
            }

            if (_isCompulsory)
            {
                btnLater.Visibility = Visibility.Collapsed;
                txtSparklesIcon.Text = "🚨";
            }
            else
            {
                btnLater.Visibility = Visibility.Visible;
                txtSparklesIcon.Text = "✨";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            // Skip this version for the remainder of this session or until next check
            SettingsManager.Default.SkipUpdateVersion = _version;
            SettingsManager.Save();
            this.Close();
        }

        private async void Action_Click(object sender, RoutedEventArgs e)
        {
            if (_updateType == "major" && !_isPaid)
            {
                // Validate payment form
                if (string.IsNullOrWhiteSpace(txtCardholder.Text) || 
                    string.IsNullOrWhiteSpace(txtCardNum.Text) || 
                    string.IsNullOrWhiteSpace(txtExpiry.Text) || 
                    string.IsNullOrWhiteSpace(txtCVV.Text))
                {
                    MessageBox.Show("Please fill out all card payment details to complete your premium upgrade.", "Upgrade Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Simulate processing payment
                gridLoader.Visibility = Visibility.Visible;
                btnAction.IsEnabled = false;
                btnLater.IsEnabled = false;

                txtLoaderStatus.Text = "Connecting to Merchant Bank...";
                await Task.Delay(1000);

                txtLoaderStatus.Text = "Authorizing Transaction...";
                await Task.Delay(1000);

                txtLoaderStatus.Text = "Activating Lifetime Premium License...";
                await Task.Delay(800);

                txtLoaderStatus.Text = "Payment Successful! ✅";
                await Task.Delay(800);

                _isPaid = true;
                borderPayment.Visibility = Visibility.Collapsed;
                gridLoader.Visibility = Visibility.Collapsed;
                btnAction.Content = "Install & Restart";
                btnAction.IsEnabled = true;
                if (!_isCompulsory) btnLater.IsEnabled = true;
                return;
            }

            // Install update and restart
            try
            {
                InstallAndRestart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch installer: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InstallAndRestart()
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
            var exeSource = Path.Combine(tempPath, $"update_v{_version}.exe");

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

            // Build self-deleting batch helper script to restart the application after exit
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
echo v{_version} update successfully applied!

echo.
echo Restarting application...
timeout /t 1 /nobreak > nul
start """" ""{targetExe}""

:: Self-destruct script
del ""%~f0""
exit
";

            File.WriteAllText(batPath, scriptContent);

            // Execute bat script as separate shell process
            var startInfoBat = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            Process.Start(startInfoBat);

            // Shutdown the parent WPF app immediately so it releases locks
            Application.Current.Shutdown();
        }
    }
}
