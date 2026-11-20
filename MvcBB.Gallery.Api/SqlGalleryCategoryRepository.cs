using Dapper;
using MvcBB.API.SQL;

namespace MvcBB.Gallery.Api
{
    public class SqlGalleryCategoryRepository : IGalleryCategoryRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public SqlGalleryCategoryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IReadOnlyList<GalleryCategory> GetAll()
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryCategory>(
                "SELECT Id, Name, Description, SortOrder, CreatedAt FROM dbo.Mod_Gallery_Categories ORDER BY SortOrder, Name").ToList();
        }

        public GalleryCategory? GetById(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryCategory>(
                "SELECT Id, Name, Description, SortOrder, CreatedAt FROM dbo.Mod_Gallery_Categories WHERE Id = @Id", new { Id = id });
        }

        public GalleryCategory Add(GalleryCategory category)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO dbo.Mod_Gallery_Categories (Name, Description, SortOrder, CreatedAt)
                OUTPUT inserted.Id
                VALUES (@Name, @Description, @SortOrder, SYSUTCDATETIME())",
                new { category.Name, category.Description, category.SortOrder });

            return GetById(id)!;
        }

        public void Update(GalleryCategory category)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE dbo.Mod_Gallery_Categories SET Name = @Name, Description = @Description, SortOrder = @SortOrder WHERE Id = @Id",
                new { category.Id, category.Name, category.Description, category.SortOrder });
        }

        public void Remove(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("DELETE FROM dbo.Mod_Gallery_Categories WHERE Id = @Id", new { Id = id });
        }
    }
}
