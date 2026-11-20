namespace MvcBB.Gallery.Api
{
    public class PagedImages
    {
        public IReadOnlyList<GalleryImage> Items { get; set; } = Array.Empty<GalleryImage>();
        public int TotalCount { get; set; }
    }
}
