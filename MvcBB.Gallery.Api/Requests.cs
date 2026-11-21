using System.ComponentModel.DataAnnotations;

namespace MvcBB.Gallery.Api
{
    public class CreateCategoryRequest
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
    }

    public class UpdateCategoryRequest : CreateCategoryRequest
    {
    }

    public class CreateImageRequest
    {
        public int CategoryId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        // Only the exact shape GalleryImageStorageService produces is accepted
        // (/images/gallery/<guid>.<ext>). The App deletes the file at this path
        // when the image is removed, so a client-chosen path such as
        // "/../appsettings.json" must never be stored.
        [Required]
        [RegularExpression(GalleryImagePath.Pattern, ErrorMessage = "Invalid image path.")]
        public string ImageUrl { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }
    }

    public class UpdateImageRequest
    {
        public int CategoryId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }
    }

    public class CreateCommentRequest
    {
        [Required, StringLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}
