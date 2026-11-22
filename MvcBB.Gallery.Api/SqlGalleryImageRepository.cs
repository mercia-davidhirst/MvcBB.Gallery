using Dapper;
using MvcBB.API.SQL;

namespace MvcBB.Gallery.Api
{
    public class SqlGalleryImageRepository : IGalleryImageRepository
    {
        private const string ImageSelect = @"
            SELECT i.Id, i.CategoryId, i.Name, i.Description, i.ImageUrl, i.FileSizeBytes,
                   i.UploadedByUserId, i.UploadedAt, i.ViewCount, i.LikeCount, i.CommentCount,
                   u.Username AS UploadedByUsername
            FROM dbo.Mod_Gallery_Images i
            INNER JOIN dbo.Users u ON u.Id = i.UploadedByUserId";

        private readonly ISqlConnectionFactory _connectionFactory;

        public SqlGalleryImageRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public PagedImages GetByCategory(int categoryId, int page, int pageSize)
        {
            using var connection = _connectionFactory.CreateConnection();

            var total = connection.QuerySingle<int>(
                "SELECT COUNT(*) FROM dbo.Mod_Gallery_Images WHERE CategoryId = @CategoryId", new { CategoryId = categoryId });

            var items = connection.Query<GalleryImage>(ImageSelect + @"
                WHERE i.CategoryId = @CategoryId
                ORDER BY i.UploadedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { CategoryId = categoryId, Offset = (page - 1) * pageSize, PageSize = pageSize }).ToList();

            return new PagedImages { Items = items, TotalCount = total };
        }

        public GalleryImage? GetById(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryImage>(ImageSelect + " WHERE i.Id = @Id", new { Id = id });
        }

        public IReadOnlyList<GalleryImage> GetRecent(int count)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryImage>(ImageSelect + " ORDER BY i.UploadedAt DESC OFFSET 0 ROWS FETCH NEXT @Count ROWS ONLY",
                new { Count = count }).ToList();
        }

        public GalleryImage Add(GalleryImage image)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO dbo.Mod_Gallery_Images (CategoryId, Name, Description, ImageUrl, FileSizeBytes, UploadedByUserId, UploadedAt)
                OUTPUT inserted.Id
                VALUES (@CategoryId, @Name, @Description, @ImageUrl, @FileSizeBytes, @UploadedByUserId, SYSUTCDATETIME())",
                new { image.CategoryId, image.Name, image.Description, image.ImageUrl, image.FileSizeBytes, image.UploadedByUserId });

            return GetById(id)!;
        }

        public void Update(GalleryImage image)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE dbo.Mod_Gallery_Images SET CategoryId = @CategoryId, Name = @Name, Description = @Description WHERE Id = @Id",
                new { image.Id, image.CategoryId, image.Name, image.Description });
        }

        public void Remove(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("DELETE FROM dbo.Mod_Gallery_Images WHERE Id = @Id", new { Id = id });
        }

        public void IncrementViewCount(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("UPDATE dbo.Mod_Gallery_Images SET ViewCount = ViewCount + 1 WHERE Id = @Id", new { Id = id });
        }

        public bool IsImageUrlInUse(string imageUrl)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingle<int>(
                "SELECT COUNT(*) FROM dbo.Mod_Gallery_Images WHERE ImageUrl = @ImageUrl", new { ImageUrl = imageUrl }) > 0;
        }

        public IReadOnlyList<string> GetImageUrlsByCategory(int categoryId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<string>(
                "SELECT ImageUrl FROM dbo.Mod_Gallery_Images WHERE CategoryId = @CategoryId", new { CategoryId = categoryId }).ToList();
        }

        public void MoveCategoryImages(int fromCategoryId, int toCategoryId)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE dbo.Mod_Gallery_Images SET CategoryId = @ToCategoryId WHERE CategoryId = @FromCategoryId",
                new { FromCategoryId = fromCategoryId, ToCategoryId = toCategoryId });
        }

        public IReadOnlyList<GalleryComment> GetComments(int imageId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryComment>(@"
                SELECT c.Id, c.ImageId, c.Content, c.CreatedByUserId, c.CreatedAt, u.Username AS CreatedByUsername
                FROM dbo.Mod_Gallery_Comments c
                INNER JOIN dbo.Users u ON u.Id = c.CreatedByUserId
                WHERE c.ImageId = @ImageId
                ORDER BY c.CreatedAt DESC", new { ImageId = imageId }).ToList();
        }

        public GalleryComment? GetCommentById(int commentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryComment>(@"
                SELECT c.Id, c.ImageId, c.Content, c.CreatedByUserId, c.CreatedAt, u.Username AS CreatedByUsername
                FROM dbo.Mod_Gallery_Comments c
                INNER JOIN dbo.Users u ON u.Id = c.CreatedByUserId
                WHERE c.Id = @Id", new { Id = commentId });
        }

        public GalleryComment AddComment(GalleryComment comment)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO dbo.Mod_Gallery_Comments (ImageId, Content, CreatedByUserId, CreatedAt)
                OUTPUT inserted.Id
                VALUES (@ImageId, @Content, @CreatedByUserId, SYSUTCDATETIME());

                UPDATE dbo.Mod_Gallery_Images SET CommentCount = CommentCount + 1 WHERE Id = @ImageId;",
                new { comment.ImageId, comment.Content, comment.CreatedByUserId });

            return GetCommentById(id)!;
        }

        public void RemoveComment(int commentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(@"
                UPDATE dbo.Mod_Gallery_Images SET CommentCount = CommentCount - 1
                WHERE Id = (SELECT ImageId FROM dbo.Mod_Gallery_Comments WHERE Id = @Id);

                DELETE FROM dbo.Mod_Gallery_Comments WHERE Id = @Id;",
                new { Id = commentId });
        }

        public bool HasLiked(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<int?>(
                "SELECT Id FROM dbo.Mod_Gallery_Likes WHERE ImageId = @ImageId AND UserId = @UserId",
                new { ImageId = imageId, UserId = userId }) != null;
        }

        public bool AddLike(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingle<bool>(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.Mod_Gallery_Likes WHERE ImageId = @ImageId AND UserId = @UserId)
                BEGIN
                    INSERT INTO dbo.Mod_Gallery_Likes (ImageId, UserId, CreatedAt) VALUES (@ImageId, @UserId, SYSUTCDATETIME());
                    UPDATE dbo.Mod_Gallery_Images SET LikeCount = LikeCount + 1 WHERE Id = @ImageId;
                    SELECT CAST(1 AS BIT);
                END
                ELSE
                BEGIN
                    SELECT CAST(0 AS BIT);
                END",
                new { ImageId = imageId, UserId = userId });
        }

        public bool RemoveLike(int imageId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingle<bool>(@"
                IF EXISTS (SELECT 1 FROM dbo.Mod_Gallery_Likes WHERE ImageId = @ImageId AND UserId = @UserId)
                BEGIN
                    DELETE FROM dbo.Mod_Gallery_Likes WHERE ImageId = @ImageId AND UserId = @UserId;
                    UPDATE dbo.Mod_Gallery_Images SET LikeCount = LikeCount - 1 WHERE Id = @ImageId;
                    SELECT CAST(1 AS BIT);
                END
                ELSE
                BEGIN
                    SELECT CAST(0 AS BIT);
                END",
                new { ImageId = imageId, UserId = userId });
        }
    }
}
