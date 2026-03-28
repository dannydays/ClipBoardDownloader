using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CBDownloader.Services;
using WinForms = System.Windows.Forms;

namespace CBDownloader.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CBDownloader";

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private string _updateStatus = "Ready";

        [ObservableProperty]
        private string _currentVersion;

        [ObservableProperty]
        private string _downloadFolderPath;

        [ObservableProperty]
        private bool _useBrowserCookies;

        [ObservableProperty]
        private string _browserForCookies;

        [ObservableProperty]
        private string _appTheme;
        
        [ObservableProperty]
        private bool _alwaysOnTop;

        public ObservableCollection<string> AvailableBrowsers { get; } = new ObservableCollection<string> { "edge", "chrome", "firefox", "opera", "opera:gx", "brave", "vivaldi" };
        public ObservableCollection<string> AvailableThemes { get; } = new ObservableCollection<string> { "System", "Light", "Dark" };

        public SettingsViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
            StartWithWindows = CheckIfAutoStartEnabled();

            DownloadFolderPath = SettingsService.Current.DownloadFolderPath;
            UseBrowserCookies = SettingsService.Current.UseBrowserCookies;
            BrowserForCookies = SettingsService.Current.BrowserForCookies;
            AppTheme = SettingsService.Current.AppTheme;
            AlwaysOnTop = SettingsService.Current.AlwaysOnTop;
        }

        partial void OnAlwaysOnTopChanged(bool value)
        {
            SettingsService.Current.AlwaysOnTop = value;
            SettingsService.Save();
            
            // Apply immediately to current instance
            if (System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Topmost = value;
            }
        }

        partial void OnAppThemeChanged(string value)
        {
            SettingsService.Current.AppTheme = value;
            SettingsService.Save();
            ThemeManager.ApplyTheme(value);
        }

        partial void OnDownloadFolderPathChanged(string value)
        {
            SettingsService.Current.DownloadFolderPath = value;
            SettingsService.Save();
        }

        partial void OnUseBrowserCookiesChanged(bool value)
        {
            SettingsService.Current.UseBrowserCookies = value;
            SettingsService.Save();
        }

        partial void OnBrowserForCookiesChanged(string value)
        {
            SettingsService.Current.BrowserForCookies = value;
            SettingsService.Save();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select Download Folder",
                SelectedPath = DownloadFolderPath
            };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                DownloadFolderPath = dialog.SelectedPath;
            }
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key != null)
                {
                    if (value)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Failed to set startup: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            UpdateStatus = "Checking for updates...";
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ClipBoardDownloader-App");
                
                var url = "https://api.github.com/repos/dannydays/ClipBoardDownloader/releases/latest";
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GitHubRelease>();
                    if (result != null && !string.IsNullOrEmpty(result.tag_name))
                    {
                        if (result.tag_name != CurrentVersion)
                        {
                            UpdateStatus = $"Update available: {result.tag_name}! Opening browser...";
                            OpenBrowser(result.html_url);
                        }
                        else
                        {
                            UpdateStatus = "You are up to date!";
                        }
                    }
                    else
                    {
                        UpdateStatus = "Could not parse release info.";
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    UpdateStatus = "You are up to date! (No online releases yet)";
                }
                else
                {
                    UpdateStatus = $"Check failed: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Error: {ex.Message}";
            }
        }

        private bool CheckIfAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private class GitHubRelease
        {
            public string tag_name { get; set; } = string.Empty;
            public string html_url { get; set; } = string.Empty;
        }
    }
}
