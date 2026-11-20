using Dapper;
using MvcBB.API.PostgreSql;

namespace MvcBB.Gallery.Api
{
    public class PostgreSqlGalleryCategoryRepository : IGalleryCategoryRepository
    {
        private readonly IPostgreSqlConnectionFactory _connectionFactory;

        public PostgreSqlGalleryCategoryRepository(IPostgreSqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IReadOnlyList<GalleryCategory> GetAll()
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.Query<GalleryCategory>(
                "SELECT id, name, description, sort_order, created_at FROM public.mod_gallery_categories ORDER BY sort_order, name").ToList();
        }

        public GalleryCategory? GetById(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            return connection.QuerySingleOrDefault<GalleryCategory>(
                "SELECT id, name, description, sort_order, created_at FROM public.mod_gallery_categories WHERE id = @Id", new { Id = id });
        }

        public GalleryCategory Add(GalleryCategory category)
        {
            using var connection = _connectionFactory.CreateConnection();
            var id = connection.QuerySingle<int>(@"
                INSERT INTO public.mod_gallery_categories (name, description, sort_order, created_at)
                VALUES (@Name, @Description, @SortOrder, (NOW() AT TIME ZONE 'utc'))
                RETURNING id",
                new { category.Name, category.Description, category.SortOrder });

            return GetById(id)!;
        }

        public void Update(GalleryCategory category)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute(
                "UPDATE public.mod_gallery_categories SET name = @Name, description = @Description, sort_order = @SortOrder WHERE id = @Id",
                new { category.Id, category.Name, category.Description, category.SortOrder });
        }

        public void Remove(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Execute("DELETE FROM public.mod_gallery_categories WHERE id = @Id", new { Id = id });
        }
    }
}
