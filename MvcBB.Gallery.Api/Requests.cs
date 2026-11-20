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

        [Required]
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
