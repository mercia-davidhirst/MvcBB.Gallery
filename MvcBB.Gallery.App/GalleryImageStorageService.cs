using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MvcBB.Shared.Services;

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

        // Matches exactly what SaveImageAsync returns. DeleteImage refuses
        // anything else, so a tampered or legacy URL can never point it at a
        // file outside the gallery folder.
        private static readonly Regex StoredUrlPattern = new(
            @"^/images/gallery/(?<file>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(png|jpg|jpeg|gif))\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

            // Check the file's actual bytes too - the name and content type are
            // whatever the uploader chose to send. Save it with the extension
            // of what it really is, so it's always served as that image type.
            await using var upload = file.OpenReadStream();
            var detectedExtension = await ImageSignature.DetectExtensionAsync(upload);
            if (detectedExtension == null)
                throw new GalleryServiceException("Only PNG, JPG, and GIF images are allowed.");

            var uploadsFolder = GalleryFolder;
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{detectedExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await upload.CopyToAsync(fileStream);
            }

            return $"/images/{Subfolder}/{uniqueFileName}";
        }

        public void DeleteImage(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return;

            var match = StoredUrlPattern.Match(relativeUrl);
            if (!match.Success)
                return;

            // Build the path from the folder plus the bare file name only, then
            // confirm it still resolves inside the gallery folder before deleting.
            var galleryFolder = Path.GetFullPath(GalleryFolder);
            var filePath = Path.GetFullPath(Path.Combine(galleryFolder, match.Groups["file"].Value));
            if (!filePath.StartsWith(galleryFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;

            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        private string GalleryFolder => Path.Combine(_webHostEnvironment.WebRootPath, "images", Subfolder);
    }
}
