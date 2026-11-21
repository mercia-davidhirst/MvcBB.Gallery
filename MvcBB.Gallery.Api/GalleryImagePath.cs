namespace MvcBB.Gallery.Api
{
    /// <summary>The only image URL shape the gallery stores: what the App's
    /// GalleryImageStorageService returns for a saved upload.</summary>
    public static class GalleryImagePath
    {
        public const string Pattern =
            @"^/images/gallery/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(png|jpg|jpeg|gif)$";
    }
}
