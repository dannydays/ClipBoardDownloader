namespace CBDownloader.Models
{
    public class PlaylistItemModel
    {
        public string Title { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public int Index { get; set; }
    }
}
