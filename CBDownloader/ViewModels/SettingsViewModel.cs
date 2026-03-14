using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

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

        public SettingsViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
            StartWithWindows = CheckIfAutoStartEnabled();
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
