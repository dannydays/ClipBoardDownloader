using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CBDownloader.Models;
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

        public bool IsTopmost => SettingsService.Current.AlwaysOnTop;

        public MainViewModel()
        {
            _ytdlService = new YoutubeDLService();
        }

        public async Task InitializeAndFetchMetadata(string url)
        {
            url = Utils.RegexHelper.EnsureProtocol(url);
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

            if (Utils.RegexHelper.IsYoutubePlaylistUrl(VideoUrl))
            {
                var result = System.Windows.MessageBox.Show(
                    "This link is part of a playlist.\nDownload the full playlist?",
                    "Playlist Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ((App)System.Windows.Application.Current).ShowPlaylistWindow(_ytdlService, VideoUrl, VideoTitle, isVideo);
                    return;
                }
            }

            EnqueueSingleItem(VideoTitle, VideoThumbnailUrl, VideoUrl, isVideo);
        }

        private void EnqueueSingleItem(string title, string thumbnailUrl, string url, bool isVideo, string? playlistName = null)
        {
            var newItem = new DownloadItemViewModel(_ytdlService)
            {
                VideoTitle = title,
                VideoThumbnailUrl = thumbnailUrl,
                VideoUrl = url,
                IsVideo = isVideo,
                PlaylistName = playlistName
            };

            Downloads.Add(newItem);
            _ = newItem.StartDownloadAsync();
        }

        public void AddPlaylistItemsToQueue(List<PlaylistItemModel> items, bool isVideo, string? playlistName = null)
        {
            foreach (var item in items)
            {
                EnqueueSingleItem(item.Title, item.ThumbnailUrl, item.VideoUrl, isVideo, playlistName);
            }
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
            var itemsToRemove = new List<DownloadItemViewModel>();
            foreach (var item in Downloads)
            {
                if (!item.IsDownloading && (item.IsCompleted || item.DownloadStatus.Contains("Error") || item.DownloadStatus == "Cancelled."))
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
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
