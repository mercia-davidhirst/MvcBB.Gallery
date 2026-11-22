using Microsoft.AspNetCore.Http;

namespace MvcBB.Gallery.App
{
    public interface IGalleryImageStorage
    {
        /// <summary>Validates and saves the file in the gallery's private folder, returning its relative URL.</summary>
        Task<string> SaveImageAsync(IFormFile file);

        void DeleteImage(string? relativeUrl);

        /// <summary>Full path of a stored gallery file, or null if the name isn't one this service made or the file doesn't exist.</summary>
        string? GetImageFilePath(string fileName);
    }
}
