using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CBDownloader.Services;
using YoutubeDLSharp;

namespace CBDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly YoutubeDLService _ytdlService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private string _videoUrl = string.Empty;

        [ObservableProperty]
        private string _videoTitle = "Waiting for link...";

        [ObservableProperty]
        private string _videoThumbnailUrl = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand))]
        [NotifyCanExecuteChangedFor(nameof(DownloadAudioCommand))]
        private bool _isBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand))]
        [NotifyCanExecuteChangedFor(nameof(DownloadAudioCommand))]
        private bool _isDownloading;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _downloadStatus = string.Empty;

        public MainViewModel()
        {
            _ytdlService = new YoutubeDLService();
        }

        public async Task InitializeAndFetchMetadata(string url)
        {
            VideoUrl = url;
            VideoTitle = "Fetching video information...";
            VideoThumbnailUrl = string.Empty;
            IsBusy = true;
            DownloadStatus = string.Empty;
            DownloadProgress = 0;

            try
            {
                await _ytdlService.EnsureBinariesExist();
                var metadata = await _ytdlService.GetVideoMetadataAsync(url);
                VideoTitle = metadata.Title;
                VideoThumbnailUrl = metadata.Thumbnail;
            }
            catch (Exception ex)
            {
                VideoTitle = "Error fetching metadata.";
                DownloadStatus = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanDownload() => !IsBusy && !IsDownloading && !string.IsNullOrWhiteSpace(VideoUrl) && !VideoTitle.Contains("Error");

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadVideoAsync() => await DownloadInternalAsync(true);

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadAudioAsync() => await DownloadInternalAsync(false);

        private bool _isPostProcessing;

        private async Task DownloadInternalAsync(bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(VideoUrl) || IsDownloading || IsBusy) return;

            IsDownloading = true;
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
                var result = await _ytdlService.DownloadAsync(VideoUrl, isVideo, true, progress, _cts.Token);
                _isPostProcessing = false;
                
                if (result.Success)
                {
                    DownloadStatus = "Download completed successfully!";
                    DownloadProgress = 100;

                    if (!string.IsNullOrEmpty(result.Data))
                    {
                        var destFolder = System.IO.Path.GetDirectoryName(result.Data);
                        if (!string.IsNullOrEmpty(destFolder))
                        {
                            bool isAlreadyOpen = false;
                            try
                            {
                                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                                if (shellType != null)
                                {
                                    dynamic shell = Activator.CreateInstance(shellType)!;
                                    foreach (dynamic win in shell.Windows())
                                    {
                                        try
                                        {
                                            string url = win.LocationURL;
                                            if (!string.IsNullOrEmpty(url) && url.StartsWith("file:///"))
                                            {
                                                string localPath = new Uri(url).LocalPath;
                                                if (string.Equals(localPath.TrimEnd('\\'), destFolder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    isAlreadyOpen = true;
                                                    break;
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch { }

                            if (!isAlreadyOpen)
                            {
                                var startInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "explorer.exe",
                                    Arguments = $"\"{destFolder}\"",
                                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Maximized,
                                    UseShellExecute = true
                                };
                                System.Diagnostics.Process.Start(startInfo);
                            }
                        }
                    }
                    
                    System.Windows.Application.Current.MainWindow?.Hide();
                }
                else
                {
                    DownloadStatus = $"Error: {string.Join(" ", result.ErrorOutput)}";
                }
            }
            catch (OperationCanceledException)
            {
                _isPostProcessing = false;
                DownloadStatus = "Download cancelled.";
            }
            catch (Exception ex)
            {
                _isPostProcessing = false;
                DownloadStatus = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                _isPostProcessing = false;
                IsDownloading = false;
            }
        }

        public void CancelDownload()
        {
            if (IsDownloading)
            {
                _cts?.Cancel();
            }
        }

        [RelayCommand]
        private void Exit()
        {
            CancelDownload();
            System.Windows.Application.Current.MainWindow?.Hide();
        }
    }
}
