namespace MvcBB.Gallery.Api
{
    public class GalleryComment
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Join field - not a stored column on Mod_Gallery_Comments.
        public string? CreatedByUsername { get; set; }
    }
}
