using MvcBB.API.InMemory;

namespace MvcBB.Gallery.Api
{
    public class InMemoryGalleryImageRepository : IGalleryImageRepository
    {
        private readonly GalleryInMemoryStore _store;
        private readonly InMemoryStore _coreStore;

        public InMemoryGalleryImageRepository(GalleryInMemoryStore store, InMemoryStore coreStore)
        {
            _store = store;
            _coreStore = coreStore;
        }

        private string? UsernameOf(int userId) => _coreStore.Users.FirstOrDefault(u => u.Id == userId)?.Username;

        // Hands out a copy, never the stored object itself: callers set
        // per-request fields (LikedByCurrentUser, Comments) on what they get
        // back, which must not leak into the shared store or to other users.
        private GalleryImage WithUsername(GalleryImage image) => new()
        {
            Id = image.Id,
            CategoryId = image.CategoryId,
            Name = image.Name,
            Description = image.Description,
            ImageUrl = image.ImageUrl,
            FileSizeBytes = image.FileSizeBytes,
            UploadedByUserId = image.UploadedByUserId,
            UploadedAt = image.UploadedAt,
            ViewCount = image.ViewCount,
            LikeCount = image.LikeCount,
            CommentCount = image.CommentCount,
            UploadedByUsername = UsernameOf(image.UploadedByUserId)
        };

        public PagedImages GetByCategory(int categoryId, int page, int pageSize)
        {
            var all = _store.Images.Where(i => i.CategoryId == categoryId).OrderByDescending(i => i.UploadedAt).ToList();
            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).Select(WithUsername).ToList();
            return new PagedImages { Items = paged, TotalCount = all.Count };
        }

        public GalleryImage? GetById(int id)
        {
            var image = _store.Images.FirstOrDefault(i => i.Id == id);
            return image == null ? null : WithUsername(image);
        }

        public IReadOnlyList<GalleryImage> GetRecent(int count) =>
            _store.Images.OrderByDescending(i => i.UploadedAt).Take(count).Select(WithUsername).ToList();

        public GalleryImage Add(GalleryImage image)
        {
            image.Id = _store.Images.Count == 0 ? 1 : _store.Images.Max(i => i.Id) + 1;
            image.UploadedAt = DateTime.UtcNow;
            image.ViewCount = 0;
            image.LikeCount = 0;
            image.CommentCount = 0;
            _store.Images.Add(image);
            return WithUsername(image);
        }

        public void Update(GalleryImage image)
        {
            var existing = _store.Images.FirstOrDefault(i => i.Id == image.Id);
            if (existing == null) return;
            existing.CategoryId = image.CategoryId;
            existing.Name = image.Name;
            existing.Description = image.Description;
        }

        public void Remove(int id)
        {
            _store.Images.RemoveAll(i => i.Id == id);
            _store.Comments.RemoveAll(c => c.ImageId == id);
            _store.Likes.RemoveAll(l => l.ImageId == id);
        }

        public void IncrementViewCount(int id)
        {
            var image = _store.Images.FirstOrDefault(i => i.Id == id);
            if (image != null) image.ViewCount++;
        }

        public bool IsImageUrlInUse(string imageUrl) =>
            _store.Images.Any(i => string.Equals(i.ImageUrl, imageUrl, StringComparison.Ordinal));

        public IReadOnlyList<string> GetImageUrlsByCategory(int categoryId) =>
            _store.Images.Where(i => i.CategoryId == categoryId).Select(i => i.ImageUrl).ToList();

        public void MoveCategoryImages(int fromCategoryId, int toCategoryId)
        {
            foreach (var image in _store.Images.Where(i => i.CategoryId == fromCategoryId))
                image.CategoryId = toCategoryId;
        }

        public IReadOnlyList<GalleryComment> GetComments(int imageId) =>
            _store.Comments.Where(c => c.ImageId == imageId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => { c.CreatedByUsername = UsernameOf(c.CreatedByUserId); return c; })
                .ToList();

        public GalleryComment? GetCommentById(int commentId)
        {
            var comment = _store.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment != null) comment.CreatedByUsername = UsernameOf(comment.CreatedByUserId);
            return comment;
        }

        public GalleryComment AddComment(GalleryComment comment)
        {
            comment.Id = _store.Comments.Count == 0 ? 1 : _store.Comments.Max(c => c.Id) + 1;
            comment.CreatedAt = DateTime.UtcNow;
            _store.Comments.Add(comment);

            var image = _store.Images.FirstOrDefault(i => i.Id == comment.ImageId);
            if (image != null) image.CommentCount++;

            comment.CreatedByUsername = UsernameOf(comment.CreatedByUserId);
            return comment;
        }

        public void RemoveComment(int commentId)
        {
            var comment = _store.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment == null) return;

            _store.Comments.Remove(comment);
            var image = _store.Images.FirstOrDefault(i => i.Id == comment.ImageId);
            if (image != null) image.CommentCount--;
        }

        public bool HasLiked(int imageId, int userId) => _store.Likes.Contains((imageId, userId));

        public bool AddLike(int imageId, int userId)
        {
            if (_store.Likes.Contains((imageId, userId))) return false;

            _store.Likes.Add((imageId, userId));
            var image = _store.Images.FirstOrDefault(i => i.Id == imageId);
            if (image != null) image.LikeCount++;
            return true;
        }

        public bool RemoveLike(int imageId, int userId)
        {
            if (!_store.Likes.Remove((imageId, userId))) return false;

            var image = _store.Images.FirstOrDefault(i => i.Id == imageId);
            if (image != null) image.LikeCount--;
            return true;
        }
    }
}
