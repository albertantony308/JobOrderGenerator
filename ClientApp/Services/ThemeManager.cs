using System;
using System.Linq;
using System.Windows;

namespace ClientApp.Services
{
    public static class ThemeManager
    {
        public static void SetTheme(bool isDark, string? preset = null)
        {
            var appResources = Application.Current.Resources;
            
            if (preset == null) preset = SettingsManager.Default.SelectedPreset;

            string fileName;
            if (isDark)
            {
                fileName = preset switch
                {
                    "Solarized" => "Themes/SolarizedDark.xaml",
                    "Ice" => "Themes/IceDark.xaml",
                    _ => "Themes/MidnightDark.xaml"
                };
            }
            else
            {
                fileName = preset switch
                {
                    "Solarized" => "Themes/SolarizedLight.xaml",
                    "Mint" => "Themes/MintLight.xaml",
                    _ => "Themes/PureLight.xaml"
                };
            }

            // Find any dictionary that looks like a theme file
            var colorTheme = appResources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains(".xaml") && 
                                    (d.Source.OriginalString.Contains("Theme") || d.Source.OriginalString.Contains("Themes/")));

            if (colorTheme != null)
            {
                int index = appResources.MergedDictionaries.IndexOf(colorTheme);
                appResources.MergedDictionaries[index] = new ResourceDictionary { Source = new Uri(fileName, UriKind.Relative) };
            }
            
            SettingsManager.Default.IsDarkMode = isDark;
            SettingsManager.Default.SelectedPreset = preset;
            SettingsManager.Save();
        }

        public static void Initialize()
        {
            SetTheme(SettingsManager.Default.IsDarkMode);
        }
    }
}
