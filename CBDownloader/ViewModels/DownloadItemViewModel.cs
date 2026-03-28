using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using CBDownloader.Services;
using YoutubeDLSharp;

namespace CBDownloader.ViewModels
{
    public partial class DownloadItemViewModel : ObservableObject
    {
        private readonly YoutubeDLService _ytdlService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private string _videoTitle = string.Empty;

        [ObservableProperty]
        private string _videoThumbnailUrl = string.Empty;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _downloadStatus = "Pending...";

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private bool _isCompleted;
        
        [ObservableProperty]
        private string _videoUrl = string.Empty;

        public bool IsVideo { get; set; }
        
        public string? FilePath { get; private set; }

        public DownloadItemViewModel(YoutubeDLService ytdlService)
        {
            _ytdlService = ytdlService;
        }

        private bool _isPostProcessing;

        public async Task StartDownloadAsync()
        {
            IsDownloading = true;
            IsCompleted = false;
            _isPostProcessing = false;
            DownloadStatus = "Starting download...";
            DownloadProgress = 0;
            _cts = new CancellationTokenSource();

            var progress = new Progress<DownloadProgress>(p =>
            {
                var stateStr = p.State.ToString();
                if (stateStr == "Downloading" && !_isPostProcessing)
                {
                    var prc = p.Progress * 100;
                    if (prc >= DownloadProgress)
                    {
                        DownloadProgress = prc > 100 ? 100 : prc;
                    }
                    
                    if (DownloadProgress >= 100)
                    {
                        _isPostProcessing = true;
                        DownloadProgress = 99;
                        DownloadStatus = "Converting media (please wait)... 99.9%";
                    }
                    else
                    {
                        DownloadStatus = $"Downloading... {DownloadProgress:F1}% ({p.DownloadSpeed})";
                    }
                }
                else if (stateStr == "PostProcessing")
                {
                    if (!_isPostProcessing)
                    {
                        _isPostProcessing = true;
                        DownloadProgress = 99;
                        DownloadStatus = "Converting media (please wait)... 99.0%";
                    }
                }
            });

            try
            {
                var result = await _ytdlService.DownloadAsync(VideoUrl, IsVideo, true, progress, _cts.Token);
                _isPostProcessing = false;
                
                if (result.Success)
                {
                    FilePath = result.Data;
                    DownloadStatus = "Completed!";
                    DownloadProgress = 100;
                    IsCompleted = true;
                }
                else
                {
                    DownloadStatus = $"Error: {string.Join(" ", result.ErrorOutput)}";
                }
            }
            catch (OperationCanceledException)
            {
                _isPostProcessing = false;
                DownloadStatus = "Cancelled.";
            }
            catch (Exception ex)
            {
                _isPostProcessing = false;
                DownloadStatus = $"Error: {ex.Message}";
            }
            finally
            {
                _isPostProcessing = false;
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsDownloading)
            {
                _cts?.Cancel();
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (!string.IsNullOrEmpty(FilePath))
            {
                var folder = System.IO.Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
