namespace MvcBB.Gallery.Api
{
    public interface IGalleryCategoryRepository
    {
        IReadOnlyList<GalleryCategory> GetAll();
        GalleryCategory? GetById(int id);
        GalleryCategory Add(GalleryCategory category);
        void Update(GalleryCategory category);
        void Remove(int id);
    }
}
