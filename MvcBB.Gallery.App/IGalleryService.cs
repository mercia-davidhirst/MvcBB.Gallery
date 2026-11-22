namespace MvcBB.Gallery.App
{
    public interface IGalleryService
    {
        Task<IEnumerable<GalleryCategoryDto>> GetCategoriesAsync();
        Task<GalleryCategoryDto?> GetCategoryAsync(int id);
        Task<GalleryCategoryDto> CreateCategoryAsync(GalleryCategoryDto category);
        Task UpdateCategoryAsync(int id, GalleryCategoryDto category);
        /// <summary>
        /// Deletes a category. With <paramref name="moveToCategoryId"/>, its images move there first;
        /// without it they're deleted too, and their stored file URLs are returned for removal from disk.
        /// </summary>
        Task<IReadOnlyList<string>> DeleteCategoryAsync(int id, int? moveToCategoryId);

        Task<PagedImagesDto> GetImagesAsync(int categoryId, int page);
        Task<GalleryImageDto?> GetImageAsync(int id);
        Task<IEnumerable<GalleryImageDto>> GetRecentAsync(int count);
        Task<GalleryImageDto> CreateImageAsync(int categoryId, string name, string? description, string imageUrl, long fileSizeBytes);
        Task UpdateImageAsync(int id, int categoryId, string name, string? description);

        /// <summary>Returns the deleted image's stored file URL, so the caller can remove it from disk.</summary>
        Task<string?> DeleteImageAsync(int id);

        Task AddCommentAsync(int imageId, string content);
        Task DeleteCommentAsync(int commentId);

        Task<(bool Liked, int LikeCount)> ToggleLikeAsync(int imageId);
    }
}
