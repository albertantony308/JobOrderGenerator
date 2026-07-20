using System.Windows;
using ClientApp.Services;

namespace ClientApp;

public partial class ProfileSetupWindow : Window
{
    private readonly LicenseManager _licenseManager;
    private readonly string _keyId;
    private string _existingPhoneNumber = "";
    private string? _existingProfileId = null;
    public bool LaunchMainWindow { get; set; } = true;

    public ProfileSetupWindow(string keyId)
    {
        InitializeComponent();
        _licenseManager = new LicenseManager();
        _keyId = keyId;

        this.Loaded += async (s, e) =>
        {
            var profile = await _licenseManager.GetProfileAsync(_keyId);
            if (profile != null)
            {
                CompanyNameInput.Text = profile.company_name ?? "";
                _existingPhoneNumber = profile.phone_number ?? "";
                EmailInput.Text = profile.email_id ?? "";
                _existingProfileId = profile.id;
                SaveButton.Content = "Update Profile";
            }
        };
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        var name = CompanyNameInput.Text.Trim();
        var email = EmailInput.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            ShowError("Company Name is required.");
            return;
        }

        SaveButton.IsEnabled = false;
        SaveButton.Content = "Saving...";

        var profile = new CompanyProfile { id = _existingProfileId, company_name = name, phone_number = _existingPhoneNumber, email_id = email };
        bool success = await _licenseManager.SaveProfileAsync(_keyId, profile);

        if (success)
        {
            if (this.Owner != null || !this.LaunchMainWindow) 
            {
                this.DialogResult = true;
            }
            else 
            {
                // Verify if a MainWindow already exists
                bool hasMainWindow = false;
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow)
                    {
                        hasMainWindow = true;
                        break;
                    }
                }
                
                if (!hasMainWindow)
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
            }
            this.Close();
        }
        else
        {
            ShowError("Failed to save profile. Check connection.");
            SaveButton.IsEnabled = true;
            SaveButton.Content = "Complete Setup";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
