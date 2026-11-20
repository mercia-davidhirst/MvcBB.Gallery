namespace MvcBB.Gallery.Api
{
    public interface IGalleryImageRepository
    {
        PagedImages GetByCategory(int categoryId, int page, int pageSize);
        GalleryImage? GetById(int id);
        IReadOnlyList<GalleryImage> GetRecent(int count);
        GalleryImage Add(GalleryImage image);
        void Update(GalleryImage image);
        void Remove(int id);
        void IncrementViewCount(int id);

        IReadOnlyList<GalleryComment> GetComments(int imageId);
        GalleryComment? GetCommentById(int commentId);
        GalleryComment AddComment(GalleryComment comment);
        void RemoveComment(int commentId);

        bool HasLiked(int imageId, int userId);
        /// <summary>No-ops (returns false) if the user already liked the image.</summary>
        bool AddLike(int imageId, int userId);
        /// <summary>No-ops (returns false) if the user hadn't liked the image.</summary>
        bool RemoveLike(int imageId, int userId);
    }
}
