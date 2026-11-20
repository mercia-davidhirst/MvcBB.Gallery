using System.ComponentModel.DataAnnotations;

namespace MvcBB.Gallery.App
{
    // Mirrors the shapes returned/accepted by MvcBB.Gallery.Api - duplicated
    // rather than shared, since there's no shared contracts package between
    // a mod's two tiers beyond MvcBB.ModKit (which only carries the mod
    // framework's own types, not this mod's feature-specific ones).

    public class GalleryCategoryDto
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GalleryImageDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int UploadedByUserId { get; set; }
        public string? UploadedByUsername { get; set; }
        public DateTime UploadedAt { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool LikedByCurrentUser { get; set; }
        public List<GalleryCommentDto>? Comments { get; set; }
    }

    public class GalleryCommentDto
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string? CreatedByUsername { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PagedImagesDto
    {
        public List<GalleryImageDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
