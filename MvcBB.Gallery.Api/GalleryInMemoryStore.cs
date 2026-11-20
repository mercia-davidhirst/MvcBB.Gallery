namespace MvcBB.Gallery.Api
{
    /// <summary>
    /// This mod's own in-process store for DataProvider=InMemory - mirrors
    /// core's InMemoryStore pattern, but scoped to this mod's own data
    /// (there's nothing for these tables in core's store). Usernames are
    /// resolved separately, against core's own InMemoryStore.Users.
    /// </summary>
    public class GalleryInMemoryStore
    {
        public List<GalleryCategory> Categories { get; } = new();
        public List<GalleryImage> Images { get; } = new();
        public List<GalleryComment> Comments { get; } = new();
        public List<(int ImageId, int UserId)> Likes { get; } = new();
    }
}
