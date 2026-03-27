using System.Text.RegularExpressions;

namespace CBDownloader.Utils
{
    public static class RegexHelper
    {
        private static readonly Regex YoutubeUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|\S*?[?&]v=)|youtu\.be\/)([a-zA-Z0-9_-]{11})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InstagramUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?instagram\.com\/(?:p|reel|tv)\/([^\/?#&]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValidSupportedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return YoutubeUrlRegex.IsMatch(url) || InstagramUrlRegex.IsMatch(url);
        }

        public static string ExtractVideoId(string url)
        {
             var match = YoutubeUrlRegex.Match(url);
             return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
