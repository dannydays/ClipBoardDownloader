using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using CBDownloader.Models;

namespace CBDownloader.Services
{
    public class YoutubeDLService
    {
        private readonly YoutubeDL _ytdl;

        public YoutubeDLService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var binFolder = Path.Combine(appData, "CBDownloader", "bin");
            if (!Directory.Exists(binFolder)) Directory.CreateDirectory(binFolder);

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!path.Contains(binFolder))
            {
                Environment.SetEnvironmentVariable("PATH", binFolder + ";" + path);
            }

            _ytdl = new YoutubeDL
            {
                YoutubeDLPath = Path.Combine(binFolder, "yt-dlp.exe"),
                FFmpegPath = Path.Combine(binFolder, "ffmpeg.exe")
            };
        }

        public async Task EnsureBinariesExist()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var binFolder = Path.Combine(appData, "CBDownloader", "bin");

            if (!File.Exists(Path.Combine(binFolder, "deno.exe")))
            {
                await DownloadDenoAsync(binFolder);
            }

            if (!File.Exists(_ytdl.YoutubeDLPath))
            {
                await Utils.DownloadYtDlp(binFolder);
            }
            if (!File.Exists(_ytdl.FFmpegPath))
            {
                await Utils.DownloadFFmpeg(binFolder);
            }

            await TryUpdateYtDlpAsync();
        }

        private async Task DownloadDenoAsync(string binFolder)
        {
            var denoPath = Path.Combine(binFolder, "deno.exe");
            if (File.Exists(denoPath)) return;

            var zipPath = Path.Combine(binFolder, "deno.zip");
            try
            {
                string url = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CBDownloader");
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                ZipFile.ExtractToDirectory(zipPath, binFolder, true);
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }

        private async Task TryUpdateYtDlpAsync()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _ytdl.YoutubeDLPath,
                        Arguments = "-U",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
            }
            catch { }
        }

        private OptionSet? GetCookieOptions(string url)
        {
            if (!SettingsService.Current.UseBrowserCookies)
                return null;

            var options = new OptionSet();
            
            var binFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CBDownloader", "bin");
            var txtCookiesPath = Path.Combine(binFolder, "youtube_cookies.txt");
            
            // App-Bound encryption breaks --cookies-from-browser for chromium browsers, 
            // so we now always rely on the extension's exported cookies file for ALL sites.
            if (File.Exists(txtCookiesPath))
            {
                options.AddCustomOption("--cookies", $"\"{txtCookiesPath}\"");
            }
            
            return options;
        }

        public async Task<YoutubeDLSharp.Metadata.VideoData> GetVideoMetadataAsync(string url)
        {
            var res = await _ytdl.RunVideoDataFetch(url);
            if (res.Success)
                return res.Data;

            var cookieOpts = GetCookieOptions(url);
            if (cookieOpts != null)
            {
                res = await _ytdl.RunVideoDataFetch(url, overrideOptions: cookieOpts);
                if (res.Success)
                    return res.Data;
            }

            throw new Exception($"Failed to fetch metadata: {string.Join("\n", res.ErrorOutput)}");
        }

        public async Task<(string PlaylistTitle, List<PlaylistItemModel> Items)> GetPlaylistMetadataAsync(string url)
        {
            var options = new OptionSet { YesPlaylist = true };
            
            if (SettingsService.Current.UseBrowserCookies)
            {
                var binFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CBDownloader", "bin");
                var txtCookiesPath = Path.Combine(binFolder, "youtube_cookies.txt");
                
                if (File.Exists(txtCookiesPath))
                {
                    options.AddCustomOption("--cookies", $"\"{txtCookiesPath}\"");
                }
            }

            var res = await _ytdl.RunVideoDataFetch(url, flat: true, overrideOptions: options);

            if (!res.Success)
                throw new Exception($"Failed to fetch playlist: {string.Join("\n", res.ErrorOutput)}");

            var entries = res.Data?.Entries;
            if (entries == null || entries.Length == 0)
                throw new Exception("Playlist is empty or could not be parsed.");

            var items = new List<PlaylistItemModel>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var videoUrl = !string.IsNullOrEmpty(entry.WebpageUrl)
                    ? entry.WebpageUrl
                    : $"https://www.youtube.com/watch?v={entry.ID}";

                long? durationSeconds = entry.Duration.HasValue ? (long?)Convert.ToInt64(entry.Duration.Value) : null;
                string duration = durationSeconds.HasValue
                    ? TimeSpan.FromSeconds(durationSeconds.Value).ToString(durationSeconds.Value >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
                    : string.Empty;

                items.Add(new PlaylistItemModel
                {
                    Index = i + 1,
                    Title = entry.Title ?? $"Video {i + 1}",
                    VideoUrl = videoUrl,
                    ThumbnailUrl = entry.Thumbnail ?? string.Empty,
                    Duration = duration
                });
            }

            return (res.Data?.Title ?? "Playlist", items);
        }

        public async Task<RunResult<string>> DownloadAsync(string url, bool isVideo, bool accelerate, IProgress<DownloadProgress> progress, CancellationToken ct, string? playlistName = null)
        {
            var baseFolder = SettingsService.Current.DownloadFolderPath;
            var subFolder = isVideo ? "Videos" : "Audios";
            var finalOutputFolder = Path.Combine(baseFolder, subFolder);

            if (!string.IsNullOrWhiteSpace(playlistName))
            {
                var safeName = string.Join("_", playlistName.Split(Path.GetInvalidFileNameChars()));
                finalOutputFolder = Path.Combine(finalOutputFolder, safeName);
            }

            if (!Directory.Exists(finalOutputFolder))
            {
                Directory.CreateDirectory(finalOutputFolder);
            }

            _ytdl.OutputFolder = finalOutputFolder;

            var options = new OptionSet();
            if (accelerate) options.ConcurrentFragments = 4;
            
            if (!isVideo)
            {
                options.ExtractAudio = true;
                options.AudioFormat = AudioConversionFormat.Mp3;
                options.AudioQuality = 0;
            }

            var result = await RunDownloadWithRetry(url, isVideo, options, progress, ct);
            
            if (!result.Success && SettingsService.Current.UseBrowserCookies)
            {
                var cookieOptions = GetCookieOptions(url) ?? new OptionSet();
                if (accelerate) cookieOptions.ConcurrentFragments = 4;
                
                if (!isVideo)
                {
                    cookieOptions.ExtractAudio = true;
                    cookieOptions.AudioFormat = AudioConversionFormat.Mp3;
                    cookieOptions.AudioQuality = 0;
                }

                result = await RunDownloadWithRetry(url, isVideo, cookieOptions, progress, ct);
            }

            return result;
        }

        private async Task<RunResult<string>> RunDownloadWithRetry(string url, bool isVideo, OptionSet options, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            if (isVideo)
            {
                return await _ytdl.RunVideoDownload(url, format: "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best",
                    overrideOptions: options, progress: progress, ct: ct);
            }
            else
            {
                return await _ytdl.RunAudioDownload(url, AudioConversionFormat.Mp3, overrideOptions: options, progress: progress, ct: ct);
            }
        }
    }
}
