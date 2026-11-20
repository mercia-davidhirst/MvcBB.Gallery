using Microsoft.AspNetCore.Http;

namespace MvcBB.Gallery.App
{
    public interface IGalleryImageStorage
    {
        /// <summary>Validates and saves the file under wwwroot, returning its relative URL.</summary>
        Task<string> SaveImageAsync(IFormFile file);

        void DeleteImage(string? relativeUrl);
    }
}
