using System;
using System.IO;
using System.Text.Json;

namespace ClientApp.Services
{
    public class AppSettings
    {
        public bool IncludeImageInPrint { get; set; } = false;
        public bool IsCloudSyncEnabled { get; set; } = false;
        public bool SyncImagesEnabled { get; set; } = false;
        public string SyncMode { get; set; } = "Hybrid"; // Options: "InternetOnly", "Hybrid", "LocalOnly"
        public string CloudAuthToken { get; set; } = string.Empty;
        public string SubscriptionKey { get; set; } = string.Empty;
        public string CloudUserEmail { get; set; } = string.Empty;
        public bool IsDarkMode { get; set; } = false;
        public string SelectedPreset { get; set; } = "Default";
        public double AppFontSize { get; set; } = 14.0;
        public string DefaultCountryCode { get; set; } = "+1";
        public bool PrintIncludeModel { get; set; } = true;
        public bool PrintIncludeCost { get; set; } = true;
        public bool PrintIncludeDiagnostics { get; set; } = true;
        public string PrintArrangement { get; set; } = "Single";
        public string DefaultPaperSize { get; set; } = "A4";
        public double PrintMargin { get; set; } = 40; // 0=None, 18=Narrow, 40=Normal, 72=Wide
        public int DefaultPrintCopies { get; set; } = 1;

        // Branding & Customization
        public string CompanyName { get; set; } = "ANTIGRAVITY SERVICE";
        public string CompanyPhone { get; set; } = "+1 800 555 0199";
        public string CompanyPhone2 { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = "123 Innovation Drive, Tech Park Suite 400";
        public string CompanyLogoPath { get; set; } = string.Empty;
        public string TermsAndConditions { get; set; } = "1. Acknowledgment copy is compulsory for collecting your materials.\n2. Our responsibility is limited to service of the accepted material only. We are not responsible for consequential damages arising from delay in non-repairs of the material.\n3. The material has been accepted for service subject to internal verification should be find the material to have been tampered, misused, components removed, the material will be returned without repairs & the customer will have to pay the minimum service charge.\n4. If the item is not collected within 30 days from the date of deposit, we are not responsible for its safe custody.\n5. Service warranty is applicable only for the parts replaced/repaired for a period of 15 days.";
        public string SelectedTemplateId { get; set; } = "SystemTemplate:FullCorporate";
        public System.Collections.Generic.List<string> PreviousTemplateIds { get; set; } = new System.Collections.Generic.List<string>();
        public string CustomTemplateJson { get; set; } = string.Empty;
        public System.Collections.Generic.List<UserTemplate> UserTemplates { get; set; } = new System.Collections.Generic.List<UserTemplate>();

        // Update Manager Settings
        public bool IsUpdateReady { get; set; } = false;
        public string UpdateReadyVersion { get; set; } = string.Empty;
        public string UpdateReadyChangelog { get; set; } = string.Empty;
        public string UpdateReadyType { get; set; } = "minor"; // "minor" or "major"
        public double UpdateReadyPaymentAmount { get; set; } = 0.00;
        public string UpdateReadyFileUrl { get; set; } = string.Empty;
        public bool UpdateReadyCompulsory { get; set; } = false;
        public string SkipUpdateVersion { get; set; } = string.Empty;

        // Auto Backup Settings
        public bool IsAutoBackupEnabled { get; set; } = true;
        public int AutoBackupIntervalMinutes { get; set; } = 10;
    }

    public class UserTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "settings.json");
        public static AppSettings Default { get; private set; } = new AppSettings();

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    Default = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    Default = new AppSettings();
                }
            }
            else
            {
                Default = new AppSettings();
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            var json = JsonSerializer.Serialize(Default, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void DeleteUserTemplate(string name)
        {
            if (Default.UserTemplates != null)
            {
                Default.UserTemplates.RemoveAll(t => t.Name == name);
                Save();
            }
        }
    }
}
