using System.Windows;
using ClientApp.Services;

namespace ClientApp;

public partial class ActivationWindow : Window
{
    private readonly LicenseManager _licenseManager;

    public ActivationWindow()
    {
        InitializeComponent();
        WindowDwmFixer.ApplyFix(this);
        _licenseManager = new LicenseManager();
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ActivateButton.IsEnabled = false;
        ActivateButton.Content = "Activating...";

        var key = KeyInput.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            ShowError("Please enter a key.");
            return;
        }

        var currentKey = SettingsManager.Default.SubscriptionKey;
        bool isSwitching = !string.IsNullOrEmpty(currentKey) && currentKey != key;

        if (isSwitching)
        {
            var confirmWin = new ConfirmSwitchWindow { Owner = this };
            if (confirmWin.ShowDialog() != true)
            {
                ActivateButton.IsEnabled = true;
                ActivateButton.Content = "Activate";
                return;
            }
        }

        var (success, keyId, errorMsg) = await _licenseManager.ActivateOnlineAsync(key);

        if (success)
        {
            // Sync key to settings and enable cloud sync
            var (valid, email) = await _licenseManager.VerifySubscriptionAsync(key);
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
                    var info = await _licenseManager.GetCurrentLicenseInfoAsync();
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
                
                // Initialize Supabase Client
                SupabaseClientManager.Initialize();

                LicenseManager.CurrentStatus = await _licenseManager.VerifyLicenseStatusAsync();
            }

            var profile = await _licenseManager.GetProfileAsync(keyId);
            if (profile != null && !string.IsNullOrEmpty(profile.company_name))
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                var profileWindow = new ProfileSetupWindow(keyId);
                profileWindow.Show();
            }
            this.Close();
        }
        else
        {
            ShowError(errorMsg);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        ActivateButton.IsEnabled = true;
        ActivateButton.Content = "Activate";
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
