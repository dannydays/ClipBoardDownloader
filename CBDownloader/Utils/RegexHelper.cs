using System;
using System.Text.RegularExpressions;

namespace CBDownloader.Utils
{
    public static class RegexHelper
    {
        private static readonly string[] SupportedDomains = new[]
        {
            "youtube.com", "youtu.be",
            "instagram.com",
            "tiktok.com",
            "twitter.com", "x.com",
            "vimeo.com",
            "facebook.com", "fb.watch",
            "reddit.com",
            "twitch.tv",
            "dailymotion.com",
            "soundcloud.com",
            "bilibili.com",
            "nicovideo.jp",
            "rumble.com",
            "bandcamp.com",
            "streamable.com",
            "bitchute.com",
            "odysee.com",
            "pinterest.com",
            "tumblr.com",
            "linkedin.com",
            "ted.com",
            "mixcloud.com",
            "vk.com",
            "ok.ru"
        };

        private static readonly Regex YoutubePlaylistUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/.*[?&]list=([^&\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YoutubePurePlaylistUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/playlist\?.*list=([^&\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YoutubeShortsRegex = new Regex(
            @"(https?:\/\/(?:www\.)?youtube\.com)\/shorts\/([a-zA-Z0-9_-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValidSupportedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(EnsureProtocol(url), UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != "http" && uri.Scheme != "https")
                return false;

            var host = uri.Host.ToLowerInvariant();
            foreach (var domain in SupportedDomains)
            {
                if (host == domain || host.EndsWith("." + domain))
                    return true;
            }

            return false;
        }

        public static bool IsYoutubePlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return YoutubePlaylistUrlRegex.IsMatch(url);
        }

        public static string NormalizeUrl(string url)
        {
            url = EnsureProtocol(url);
            url = NormalizeYoutubeShorts(url);
            return url;
        }

        public static string EnsureProtocol(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url;
            }
            return url;
        }

        private static string NormalizeYoutubeShorts(string url)
        {
            return YoutubeShortsRegex.Replace(url, "$1/v/$2");
        }
    }
}
