namespace MvcBB.Gallery.Api
{
    public class GalleryImage
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int UploadedByUserId { get; set; }
        public DateTime UploadedAt { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }

        // Join/request-scoped fields - never stored columns on Mod_Gallery_Images.
        public string? UploadedByUsername { get; set; }
        public bool LikedByCurrentUser { get; set; }
        public List<GalleryComment>? Comments { get; set; }
    }
}
