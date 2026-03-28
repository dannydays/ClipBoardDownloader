using Microsoft.Win32;
using System;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using CBDownloader.Services;

namespace CBDownloader.Services
{
    public static class ThemeManager
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RegistryValueName = "AppsUseLightTheme";

        public static void Initialize()
        {
            ApplyTheme(SettingsService.Current.AppTheme);
            
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (SettingsService.Current.AppTheme == "System")
                {
                    ApplyTheme("System");
                }
            };
        }

        public static void ApplyTheme(string theme)
        {
            bool isDark = false;

            if (theme == "System")
            {
                isDark = IsSystemInDarkMode();
            }
            else
            {
                isDark = theme == "Dark";
            }

            string themeFile = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            var newDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{themeFile}")
            };

            var appResources = System.Windows.Application.Current.Resources;
            var oldDict = appResources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));

            if (oldDict != null)
            {
                appResources.MergedDictionaries.Remove(oldDict);
            }
            appResources.MergedDictionaries.Add(newDict);
        }

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    var result = key?.GetValue(RegistryValueName);
                    if (result != null)
                    {
                        return (int)result == 0;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
