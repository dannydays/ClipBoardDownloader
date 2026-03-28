using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

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

            if (!File.Exists(_ytdl.YoutubeDLPath))
            {
                await Utils.DownloadYtDlp(binFolder);
            }
            if (!File.Exists(_ytdl.FFmpegPath))
            {
                await Utils.DownloadFFmpeg(binFolder);
            }
        }

        public async Task<YoutubeDLSharp.Metadata.VideoData> GetVideoMetadataAsync(string url)
        {
            var res = await _ytdl.RunVideoDataFetch(url);
            if (res.Success)
            {
                return res.Data;
            }
            throw new Exception($"Failed to fetch metadata: {string.Join("\n", res.ErrorOutput)}");
        }

        public async Task<RunResult<string>> DownloadAsync(string url, bool isVideo, bool accelerate, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            var baseFolder = CBDownloader.Services.SettingsService.Current.DownloadFolderPath;
            var subFolder = isVideo ? "Videos" : "Audios";
            var finalOutputFolder = Path.Combine(baseFolder, subFolder);
            
            if (!Directory.Exists(finalOutputFolder))
            {
                Directory.CreateDirectory(finalOutputFolder);
            }

            _ytdl.OutputFolder = finalOutputFolder;

            var options = new OptionSet();
            
            if (CBDownloader.Services.SettingsService.Current.UseBrowserCookies)
            {
                options.AddCustomOption("--cookies-from-browser", CBDownloader.Services.SettingsService.Current.BrowserForCookies);
            }
            
            if (accelerate)
            {
                options.ConcurrentFragments = 4;
            }

            if (isVideo)
            {
                return await _ytdl.RunVideoDownload(url, format: "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best",
                    overrideOptions: options, progress: progress, ct: ct);
            }
            else
            {
                options.ExtractAudio = true;
                options.AudioFormat = AudioConversionFormat.Mp3;
                options.AudioQuality = 0;
                
                return await _ytdl.RunAudioDownload(url, AudioConversionFormat.Mp3, overrideOptions: options, progress: progress, ct: ct);
            }
        }
    }
}
