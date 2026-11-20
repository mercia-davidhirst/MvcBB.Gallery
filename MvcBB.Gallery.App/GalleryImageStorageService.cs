using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace MvcBB.Gallery.App
{
    /// <summary>
    /// This mod's own copy of MvcBB.App's ImageUploadService pattern (validate,
    /// save under the host's wwwroot, return a relative URL) - mods can't
    /// reference MvcBB.App's internal service directly, only MvcBB.ModKit, so
    /// this is intentionally duplicated rather than shared. IWebHostEnvironment
    /// resolves to the *host* app's web root since this mod's types run inside
    /// the host's own DI container.
    /// </summary>
    public class GalleryImageStorageService : IGalleryImageStorage
    {
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
        private static readonly string[] AllowedContentTypes = { "image/png", "image/jpeg", "image/gif" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB - gallery photos/art, not profile pictures
        private const string Subfolder = "gallery";

        private readonly IWebHostEnvironment _webHostEnvironment;

        public GalleryImageStorageService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> SaveImageAsync(IFormFile file)
        {
            if (file.Length == 0)
                throw new GalleryServiceException("The selected file is empty.");

            if (file.Length > MaxFileSizeBytes)
                throw new GalleryServiceException("Image must be smaller than 10 MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new GalleryServiceException("Only PNG, JPG, and GIF images are allowed.");

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", Subfolder);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/images/{Subfolder}/{uniqueFileName}";
        }

        public void DeleteImage(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return;

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, relativeUrl.TrimStart('/'));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
