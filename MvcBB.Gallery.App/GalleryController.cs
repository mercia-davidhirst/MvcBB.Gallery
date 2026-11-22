using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MvcBB.ModKit;

namespace MvcBB.Gallery.App
{
    [Mod("gallery")]
    [Authorize]
    public class GalleryController : Controller
    {
        private const int RecentCount = 6;

        private readonly IGalleryService _galleryService;
        private readonly IGalleryImageStorage _imageStorage;
        private readonly GalleryUploadRateLimiter _uploadRateLimiter;

        public GalleryController(IGalleryService galleryService, IGalleryImageStorage imageStorage, GalleryUploadRateLimiter uploadRateLimiter)
        {
            _galleryService = galleryService;
            _imageStorage = imageStorage;
            _uploadRateLimiter = uploadRateLimiter;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                ViewBag.Categories = await _galleryService.GetCategoriesAsync();
                ViewBag.Recent = await _galleryService.GetRecentAsync(RecentCount);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
                ViewBag.Categories = Array.Empty<GalleryCategoryDto>();
                ViewBag.Recent = Array.Empty<GalleryImageDto>();
            }
            return View();
        }

        public async Task<IActionResult> Category(int id, int page = 1)
        {
            var category = await _galleryService.GetCategoryAsync(id);
            if (category == null) return NotFound();

            ViewBag.Category = category;
            ViewBag.Page = page;
            return View(await _galleryService.GetImagesAsync(id, page));
        }

        public async Task<IActionResult> Image(int id)
        {
            var image = await _galleryService.GetImageAsync(id);
            if (image == null) return NotFound();

            ViewBag.CanManage = CanManage(image.UploadedByUserId);
            return View(image);
        }

        /// <summary>
        /// Serves a stored gallery file at the URL it was saved under. The files
        /// live outside wwwroot, so this (behind the controller's [Authorize])
        /// is the only way to open one: guests get the login redirect instead.
        /// </summary>
        [HttpGet("/images/gallery/{fileName}")]
        public IActionResult ImageFile(string fileName)
        {
            var filePath = _imageStorage.GetImageFilePath(fileName);
            if (filePath == null) return NotFound();

            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            // Members only: browsers may cache it, shared caches/proxies must not.
            Response.Headers["Cache-Control"] = "private, max-age=86400";
            return PhysicalFile(filePath, contentType, enableRangeProcessing: false);
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            ViewBag.Categories = await _galleryService.GetCategoriesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int categoryId, string name, string? description, IFormFile file)
        {
            try
            {
                if (file == null) throw new GalleryServiceException("Please choose a file to upload.");

                // Check everything the API would reject before writing the file,
                // so a bad form never leaves a file behind on disk.
                name = name?.Trim() ?? string.Empty;
                if (name.Length == 0) throw new GalleryServiceException("Please give the image a name.");
                if (name.Length > 200) throw new GalleryServiceException("The name must be 200 characters or fewer.");
                if (description?.Length > 2000) throw new GalleryServiceException("The description must be 2000 characters or fewer.");
                if (await _galleryService.GetCategoryAsync(categoryId) == null)
                    throw new GalleryServiceException("Please choose a category.");

                if (CurrentUserId is not int userId || !_uploadRateLimiter.TryAcquire(userId))
                    throw new GalleryServiceException("You've uploaded a lot of images in a short time. Please wait a few minutes and try again.");

                var imageUrl = await _imageStorage.SaveImageAsync(file);
                GalleryImageDto created;
                try
                {
                    created = await _galleryService.CreateImageAsync(categoryId, name, description, imageUrl, file.Length);
                }
                catch
                {
                    // The API didn't record it, so nothing points at this file.
                    _imageStorage.DeleteImage(imageUrl);
                    throw;
                }
                return RedirectToAction(nameof(Image), new { id = created.Id });
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
                ViewBag.Categories = await _galleryService.GetCategoriesAsync();
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var image = await _galleryService.GetImageAsync(id);
            if (image == null) return NotFound();
            if (!CanManage(image.UploadedByUserId)) return Forbid();

            ViewBag.Categories = await _galleryService.GetCategoriesAsync();
            return View(image);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int categoryId, string name, string? description)
        {
            try
            {
                await _galleryService.UpdateImageAsync(id, categoryId, name, description);
                return RedirectToAction(nameof(Image), new { id });
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var image = await _galleryService.GetImageAsync(id);
            var categoryId = image?.CategoryId;

            try
            {
                var deletedUrl = await _galleryService.DeleteImageAsync(id);
                _imageStorage.DeleteImage(deletedUrl);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Image), new { id });
            }

            return categoryId.HasValue ? RedirectToAction(nameof(Category), new { id = categoryId }) : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int imageId, string content)
        {
            try
            {
                await _galleryService.AddCommentAsync(imageId, content);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Image), new { id = imageId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id, int imageId)
        {
            try
            {
                await _galleryService.DeleteCommentAsync(id);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Image), new { id = imageId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LikeImage(int id)
        {
            try
            {
                await _galleryService.ToggleLikeAsync(id);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Image), new { id });
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Categories() => View(await _galleryService.GetCategoriesAsync());

        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public IActionResult CreateCategory() => View(new GalleryCategoryDto());

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(GalleryCategoryDto category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _galleryService.CreateCategoryAsync(category);
                return RedirectToAction(nameof(Categories));
            }
            catch (GalleryServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _galleryService.GetCategoryAsync(id);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, GalleryCategoryDto category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _galleryService.UpdateCategoryAsync(id, category);
                return RedirectToAction(nameof(Categories));
            }
            catch (GalleryServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _galleryService.GetCategoryAsync(id);
            if (category == null) return NotFound();

            ViewBag.ImageCount = (await _galleryService.GetImagesAsync(id, 1)).TotalCount;
            ViewBag.OtherCategories = (await _galleryService.GetCategoriesAsync()).Where(c => c.Id != id).ToList();
            return View(category);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id, int? moveToCategoryId)
        {
            try
            {
                var orphanedUrls = await _galleryService.DeleteCategoryAsync(id, moveToCategoryId);
                foreach (var url in orphanedUrls)
                    _imageStorage.DeleteImage(url);
            }
            catch (GalleryServiceException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(DeleteCategory), new { id });
            }
            return RedirectToAction(nameof(Categories));
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private bool CanManage(int ownerUserId) =>
            User.IsInRole("Administrator") || User.IsInRole("Moderator") || CurrentUserId == ownerUserId;
    }
}
