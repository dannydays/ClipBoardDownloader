using System;
using System.Threading.Tasks;

namespace CBDownloader.Services
{
    public static class Utils
    {
        public static async Task DownloadYtDlp(string directoryPath)
        {
            await YoutubeDLSharp.Utils.DownloadYtDlp(directoryPath);
        }

        public static async Task DownloadFFmpeg(string directoryPath)
        {
            await YoutubeDLSharp.Utils.DownloadFFmpeg(directoryPath);
        }
    }
}
