using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClientApp.Services;

namespace ClientApp
{
    public partial class UpdateNotificationWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private bool _isPaid = false;

        public UpdateNotificationWindow(UpdateInfo updateInfo)
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);

            _updateInfo = updateInfo;

            // Apply contents dynamically
            txtHeaderTitle.Text = _updateInfo.IsCompulsory 
                ? "Compulsory Update Required" 
                : (_updateInfo.UpdateType == "major" ? "Major Upgrade Available" : "Software Update Available");

            txtVersionSubtitle.Text = $"Version {_updateInfo.Version} is available";
            txtChangelog.Text = string.IsNullOrWhiteSpace(_updateInfo.Changelog) 
                ? "• Performance refinements, feature updates, and security patches." 
                : _updateInfo.Changelog;

            if (_updateInfo.UpdateType == "major")
            {
                borderPayment.Visibility = Visibility.Visible;
                txtPaymentDetails.Text = $"This is a Major Upgrade (${_updateInfo.PaymentAmount:F2} USD) containing premium modules. Please complete checkout to continue.";
                btnAction.Content = $"Pay & Upgrade (${_updateInfo.PaymentAmount:F2})";
            }
            else
            {
                borderPayment.Visibility = Visibility.Collapsed;
                btnAction.Content = _updateInfo.IsCompulsory ? "Download & Install" : "Update Now";
            }

            if (_updateInfo.IsCompulsory)
            {
                btnLater.Visibility = Visibility.Collapsed;
                borderCompulsoryNotice.Visibility = Visibility.Visible;
                txtSparklesIcon.Text = "🚨";
            }
            else
            {
                btnLater.Visibility = Visibility.Visible;
                borderCompulsoryNotice.Visibility = Visibility.Collapsed;
                txtSparklesIcon.Text = "✨";
            }
        }

        public UpdateNotificationWindow(string version, string updateType, string changelog, double paymentAmount, bool isCompulsory, string fileUrl = "")
            : this(new UpdateInfo
            {
                Version = version,
                UpdateType = updateType,
                Changelog = changelog,
                PaymentAmount = paymentAmount,
                IsCompulsory = isCompulsory,
                FileUrl = fileUrl
            })
        {
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo.IsCompulsory)
            {
                MessageBox.Show("This update is mandatory to continue using the application. The app will now exit.", "Compulsory Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            this.DialogResult = false;
            this.Close();
        }

        private async void Action_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo.UpdateType == "major" && !_isPaid)
            {
                if (string.IsNullOrWhiteSpace(txtCardholder.Text) || 
                    string.IsNullOrWhiteSpace(txtCardNum.Text) || 
                    string.IsNullOrWhiteSpace(txtExpiry.Text) || 
                    string.IsNullOrWhiteSpace(txtCVV.Text))
                {
                    MessageBox.Show("Please fill out all card payment details to complete your premium upgrade.", "Upgrade Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnAction.IsEnabled = false;
                btnLater.IsEnabled = false;

                txtDownloadDetails.Text = "Connecting to Merchant Bank...";
                gridChangelogView.Visibility = Visibility.Collapsed;
                gridDownloadView.Visibility = Visibility.Visible;
                txtPercent.Text = "Pay";

                await Task.Delay(1000);
                txtDownloadDetails.Text = "Authorizing Premium License...";
                await Task.Delay(1000);

                _isPaid = true;
                borderPayment.Visibility = Visibility.Collapsed;
            }

            _ = UpdateManager.Instance.StartDownloadAsync(_updateInfo);

            this.DialogResult = true;
            this.Close();
        }

        public static void InstallAndRestart(UpdateInfo _updateInfo)
        {
            // Safety offline backup into Documents\Service Memo Backups\
            BackupManager.CreatePreUpdateSafetyBackup();

            var fileUrl = _updateInfo.FileUrl ?? SettingsManager.Default.UpdateReadyFileUrl ?? "";
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
            var exeSource = Path.Combine(tempPath, $"update_v{_updateInfo.Version}.exe");

            var batPath = Path.Combine(baseDirectory, "install-update.bat");

            string runAction = "";
            if (isSetupInstaller && File.Exists(exeSource))
            {
                runAction = $@"echo Launching Setup Installer...
start """" ""{exeSource}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            }
            else if (File.Exists(exeSource))
            {
                runAction = $@"echo Copying new application executable...
copy /y ""{exeSource}"" ""{targetExe}""
if errorlevel 1 (
    echo Error: Failed to copy update file.
    pause
)
start """" ""{targetExe}""";
            }

            string scriptContent = $@"@echo off
title Job Order Generator Updater
echo ============================================
echo      JOB ORDER GENERATOR AUTO-UPDATER      
echo ============================================
echo.
echo Waiting for application process to terminate...
timeout /t 3 /nobreak > nul

{runAction}

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

            Application.Current.Shutdown();
        }
    }
}
