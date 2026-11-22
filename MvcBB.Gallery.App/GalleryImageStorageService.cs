using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MvcBB.Shared.Services;

namespace MvcBB.Gallery.App
{
    /// <summary>
    /// This mod's own copy of MvcBB.App's ImageUploadService pattern (validate,
    /// save, return a relative URL) - mods can't reference MvcBB.App's internal
    /// service directly, only MvcBB.ModKit, so this is intentionally duplicated
    /// rather than shared.
    ///
    /// Unlike core uploads, gallery files are kept OUTSIDE the host's wwwroot
    /// (by default under the host's content root, App_Data/gallery, or wherever
    /// Gallery:StoragePath points), so the static file middleware never serves
    /// them. GalleryController.ImageFile serves them instead, at the same
    /// /images/gallery/&lt;file&gt; URL, and only to signed-in members.
    /// </summary>
    public class GalleryImageStorageService : IGalleryImageStorage
    {
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
        private static readonly string[] AllowedContentTypes = { "image/png", "image/jpeg", "image/gif" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB - gallery photos/art, not profile pictures
        private const string Subfolder = "gallery";

        // A bare stored file name: exactly what SaveImageAsync generates.
        private const string FileNamePattern =
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(png|jpg|jpeg|gif)";

        private static readonly Regex StoredFileName = new(
            "^" + FileNamePattern + @"\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Matches exactly what SaveImageAsync returns. DeleteImage refuses
        // anything else, so a tampered or legacy URL can never point it at a
        // file outside the gallery folder.
        private static readonly Regex StoredUrlPattern = new(
            @"^/images/gallery/(?<file>" + FileNamePattern + @")\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        public GalleryImageStorageService(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
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

            var fileName = match.Groups["file"].Value;
            DeleteFromFolder(GalleryFolder, fileName);

            // A file from before 1.1.0 that the startup move hasn't reached.
            if (LegacyPublicFolder is string legacyFolder)
                DeleteFromFolder(legacyFolder, fileName);
        }

        public string? GetImageFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !StoredFileName.IsMatch(fileName))
                return null;

            var filePath = ResolveInside(GalleryFolder, fileName);
            return filePath != null && File.Exists(filePath) ? filePath : null;
        }

        /// <summary>
        /// Moves files saved under wwwroot/images/gallery by earlier versions
        /// into the private folder, so they stop being publicly reachable.
        /// Their URLs don't change. Returns how many files were moved.
        /// </summary>
        public int MoveLegacyPublicFiles()
        {
            if (LegacyPublicFolder is not string legacyFolder || !Directory.Exists(legacyFolder))
                return 0;

            var privateFolder = GalleryFolder;
            Directory.CreateDirectory(privateFolder);

            var moved = 0;
            foreach (var sourcePath in Directory.GetFiles(legacyFolder))
            {
                var fileName = Path.GetFileName(sourcePath);
                if (!StoredFileName.IsMatch(fileName))
                    continue;

                var targetPath = ResolveInside(privateFolder, fileName);
                if (targetPath == null)
                    continue;

                if (File.Exists(targetPath))
                    File.Delete(sourcePath);
                else
                    File.Move(sourcePath, targetPath);
                moved++;
            }
            return moved;
        }

        private static void DeleteFromFolder(string folder, string fileName)
        {
            var filePath = ResolveInside(folder, fileName);
            if (filePath != null && File.Exists(filePath))
                File.Delete(filePath);
        }

        // Builds the path from the folder plus the bare file name only, then
        // confirms it still resolves inside that folder.
        private static string? ResolveInside(string folder, string fileName)
        {
            var fullFolder = Path.GetFullPath(folder);
            var filePath = Path.GetFullPath(Path.Combine(fullFolder, fileName));
            return filePath.StartsWith(fullFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? filePath
                : null;
        }

        private string GalleryFolder
        {
            get
            {
                var configured = _configuration["Gallery:StoragePath"];
                return string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(_webHostEnvironment.ContentRootPath, "App_Data", Subfolder)
                    : Path.GetFullPath(configured, _webHostEnvironment.ContentRootPath);
            }
        }

        private string? LegacyPublicFolder =>
            string.IsNullOrEmpty(_webHostEnvironment.WebRootPath)
                ? null
                : Path.Combine(_webHostEnvironment.WebRootPath, "images", Subfolder);
    }
}
