namespace MvcBB.Gallery.Api
{
    public class InMemoryGalleryCategoryRepository : IGalleryCategoryRepository
    {
        private readonly GalleryInMemoryStore _store;

        public InMemoryGalleryCategoryRepository(GalleryInMemoryStore store)
        {
            _store = store;
        }

        public IReadOnlyList<GalleryCategory> GetAll() => _store.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();

        public GalleryCategory? GetById(int id) => _store.Categories.FirstOrDefault(c => c.Id == id);

        public GalleryCategory Add(GalleryCategory category)
        {
            category.Id = _store.Categories.Count == 0 ? 1 : _store.Categories.Max(c => c.Id) + 1;
            category.CreatedAt = DateTime.UtcNow;
            _store.Categories.Add(category);
            return category;
        }

        public void Update(GalleryCategory category)
        {
            var existing = _store.Categories.FirstOrDefault(c => c.Id == category.Id);
            if (existing == null) return;
            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.SortOrder = category.SortOrder;
        }

        public void Remove(int id)
        {
            _store.Categories.RemoveAll(c => c.Id == id);
            var imageIds = _store.Images.Where(i => i.CategoryId == id).Select(i => i.Id).ToList();
            _store.Images.RemoveAll(i => i.CategoryId == id);
            _store.Comments.RemoveAll(c => imageIds.Contains(c.ImageId));
            _store.Likes.RemoveAll(l => imageIds.Contains(l.ImageId));
        }
    }
}
