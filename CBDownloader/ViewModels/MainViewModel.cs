using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using CBDownloader.Services;
using YoutubeDLSharp;

namespace CBDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly YoutubeDLService _ytdlService;

        public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new ObservableCollection<DownloadItemViewModel>();

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

            try
            {
                await _ytdlService.EnsureBinariesExist();
                var metadata = await _ytdlService.GetVideoMetadataAsync(url);
                VideoTitle = metadata.Title;
                VideoThumbnailUrl = metadata.Thumbnail;
            }
            catch (Exception)
            {
                VideoTitle = "Ready for links (Instagram/YouTube)";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanDownload() => !IsBusy && !string.IsNullOrWhiteSpace(VideoUrl) && !VideoTitle.Contains("error", StringComparison.OrdinalIgnoreCase);

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private void DownloadVideo() => AddDownloadToQueue(true);

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private void DownloadAudio() => AddDownloadToQueue(false);

        private void AddDownloadToQueue(bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(VideoUrl) || IsBusy) return;

            var newItem = new DownloadItemViewModel(_ytdlService)
            {
                VideoTitle = this.VideoTitle,
                VideoThumbnailUrl = this.VideoThumbnailUrl,
                VideoUrl = this.VideoUrl,
                IsVideo = isVideo
            };

            Downloads.Add(newItem);
            
            // Start download without awaiting to avoid blocking UI
            _ = newItem.StartDownloadAsync();

            ClearPreview();
        }

        private void ClearPreview()
        {
            VideoUrl = string.Empty;
            VideoTitle = "Waiting for link...";
            VideoThumbnailUrl = string.Empty;
        }

        [RelayCommand]
        private void Exit()
        {
            foreach (var dl in Downloads)
            {
                if (dl.IsDownloading) dl.CancelCommand.Execute(null);
            }
            System.Windows.Application.Current.MainWindow?.Hide();
        }

        [RelayCommand]
        private void ClearCompleted()
        {
            var itemsToRemove = new System.Collections.Generic.List<DownloadItemViewModel>();
            foreach(var item in Downloads)
            {
                if(!item.IsDownloading && (item.IsCompleted || item.DownloadStatus.Contains("Error") || item.DownloadStatus == "Cancelled."))
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach(var item in itemsToRemove)
            {
                Downloads.Remove(item);
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            ((App)System.Windows.Application.Current).ShowSettings();
        }
    }
}
