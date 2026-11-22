using Dapper;
using MvcBB.API.PostgreSql;

namespace MvcBB.Gallery.Api
{
    public class PostgreSqlGalleryImageRepository : IGalleryImageRepository
    {
        private const string ImageSelect = @"
            SELECT i.id, i.category_id, i.name, i.description, i.image_url, i.file_size_bytes,
                   i.uploaded_by_user_id, i.uploaded_at, i.view_count, i.like_count, i.comment_count,
                   u.username AS uploaded_by_username
            FROM public.mod_gallery_images i
            INNER JOIN public.users u ON u.id = i.uploaded_by_user_id";

        private readonly IPostgreSqlConnectionFactory _connectionFactory;

        public PostgreSqlGalleryImageRepository(IPostgreSqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public PagedImages GetByCategory(int categoryId, int page, int pageSize)
        {
            using var connection = _connectionFactory.CreateConnection();

            var total = connection.QuerySingle<int>(
                "SELECT COUNT(*) FROM public.mod_gallery_images WHERE category_id = @CategoryId", new { CategoryId = categoryId });

            var items = connection.Query<GalleryImage>(ImageSelect + @"
                WHERE i.category_id = @CategoryId
                ORDER BY i.uploaded_at DESC
                OFFSET @Offset LIMIT @PageSize",
                new { CategoryId = categoryId, Offset = (page - 1) * pageSize, PageSize = pageSize }).ToList();

            return new PagedImages { Items = items, TotalCount = total };
        }

        public GalleryImage? GetById(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryImage>(ImageSelect + " WHERE i.id = @Id", new { Id = id });
        }

        public IReadOnlyList<GalleryImage> GetRecent(int count)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryImage>(ImageSelect + " ORDER BY i.uploaded_at DESC LIMIT @Count", new { Count = count }).ToList();
        }

        public GalleryImage Add(GalleryImage image)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO public.mod_gallery_images (category_id, name, description, image_url, file_size_bytes, uploaded_by_user_id, uploaded_at)
                VALUES (@CategoryId, @Name, @Description, @ImageUrl, @FileSizeBytes, @UploadedByUserId, (NOW() AT TIME ZONE 'utc'))
                RETURNING id",
                new { image.CategoryId, image.Name, image.Description, image.ImageUrl, image.FileSizeBytes, image.UploadedByUserId });

            return GetById(id)!;
        }

        public void Update(GalleryImage image)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE public.mod_gallery_images SET category_id = @CategoryId, name = @Name, description = @Description WHERE id = @Id",
                new { image.Id, image.CategoryId, image.Name, image.Description });
        }

        public void Remove(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("DELETE FROM public.mod_gallery_images WHERE id = @Id", new { Id = id });
        }

        public void IncrementViewCount(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("UPDATE public.mod_gallery_images SET view_count = view_count + 1 WHERE id = @Id", new { Id = id });
        }

        public bool IsImageUrlInUse(string imageUrl)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingle<int>(
                "SELECT COUNT(*) FROM public.mod_gallery_images WHERE image_url = @ImageUrl", new { ImageUrl = imageUrl }) > 0;
        }

        public IReadOnlyList<string> GetImageUrlsByCategory(int categoryId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<string>(
                "SELECT image_url FROM public.mod_gallery_images WHERE category_id = @CategoryId", new { CategoryId = categoryId }).ToList();
        }

        public void MoveCategoryImages(int fromCategoryId, int toCategoryId)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE public.mod_gallery_images SET category_id = @ToCategoryId WHERE category_id = @FromCategoryId",
                new { FromCategoryId = fromCategoryId, ToCategoryId = toCategoryId });
        }

        public IReadOnlyList<GalleryComment> GetComments(int imageId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryComment>(@"
                SELECT c.id, c.image_id, c.content, c.created_by_user_id, c.created_at, u.username AS created_by_username
                FROM public.mod_gallery_comments c
                INNER JOIN public.users u ON u.id = c.created_by_user_id
                WHERE c.image_id = @ImageId
                ORDER BY c.created_at DESC", new { ImageId = imageId }).ToList();
        }

        public GalleryComment? GetCommentById(int commentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryComment>(@"
                SELECT c.id, c.image_id, c.content, c.created_by_user_id, c.created_at, u.username AS created_by_username
                FROM public.mod_gallery_comments c
                INNER JOIN public.users u ON u.id = c.created_by_user_id
                WHERE c.id = @Id", new { Id = commentId });
        }

        public GalleryComment AddComment(GalleryComment comment)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO public.mod_gallery_comments (image_id, content, created_by_user_id, created_at)
                VALUES (@ImageId, @Content, @CreatedByUserId, (NOW() AT TIME ZONE 'utc'))
                RETURNING id",
                new { comment.ImageId, comment.Content, comment.CreatedByUserId });

            connection.Execute("UPDATE public.mod_gallery_images SET comment_count = comment_count + 1 WHERE id = @ImageId",
                new { comment.ImageId });

            return GetCommentById(id)!;
        }

        public void RemoveComment(int commentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            var imageId = connection.QuerySingleOrDefault<int?>(
                "SELECT image_id FROM public.mod_gallery_comments WHERE id = @Id", new { Id = commentId });
            if (imageId == null) return;

            connection.Execute("DELETE FROM public.mod_gallery_comments WHERE id = @Id", new { Id = commentId });
            connection.Execute("UPDATE public.mod_gallery_images SET comment_count = comment_count - 1 WHERE id = @ImageId",
                new { ImageId = imageId });
        }

        public bool HasLiked(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<int?>(
                "SELECT id FROM public.mod_gallery_likes WHERE image_id = @ImageId AND user_id = @UserId",
                new { ImageId = imageId, UserId = userId }) != null;
        }

        public bool AddLike(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            var insertedId = connection.QuerySingleOrDefault<int?>(@"
                INSERT INTO public.mod_gallery_likes (image_id, user_id, created_at)
                SELECT @ImageId, @UserId, (NOW() AT TIME ZONE 'utc')
                WHERE NOT EXISTS (SELECT 1 FROM public.mod_gallery_likes WHERE image_id = @ImageId AND user_id = @UserId)
                RETURNING id",
                new { ImageId = imageId, UserId = userId });

            if (insertedId == null) return false;

            connection.Execute("UPDATE public.mod_gallery_images SET like_count = like_count + 1 WHERE id = @ImageId", new { ImageId = imageId });
            return true;
        }

        public bool RemoveLike(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            var deletedId = connection.QuerySingleOrDefault<int?>(
                "DELETE FROM public.mod_gallery_likes WHERE image_id = @ImageId AND user_id = @UserId RETURNING id",
                new { ImageId = imageId, UserId = userId });

            if (deletedId == null) return false;

            connection.Execute("UPDATE public.mod_gallery_images SET like_count = like_count - 1 WHERE id = @ImageId", new { ImageId = imageId });
            return true;
        }
    }
}
