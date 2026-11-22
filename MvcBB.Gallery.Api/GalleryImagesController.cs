using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MvcBB.ModKit;

namespace MvcBB.Gallery.Api
{
    [Mod("gallery")]
    [ApiController]
    [Route("api/mods/gallery")]
    [Authorize]
    public class GalleryImagesController : ControllerBase
    {
        private const int DefaultPageSize = 12;
        private const int MaxPageSize = 50;
        private const int MaxPage = 100_000;
        private const int MaxRecentCount = 50;

        private readonly IGalleryImageRepository _imageRepository;
        private readonly IGalleryCategoryRepository _categoryRepository;

        public GalleryImagesController(IGalleryImageRepository imageRepository, IGalleryCategoryRepository categoryRepository)
        {
            _imageRepository = imageRepository;
            _categoryRepository = categoryRepository;
        }

        [HttpGet("categories/{categoryId}/images")]
        public ActionResult<PagedImages> GetByCategory(int categoryId, int page = 1, int pageSize = DefaultPageSize)
        {
            if (_categoryRepository.GetById(categoryId) == null)
                return NotFound(new { message = "Category not found" });

            return Ok(_imageRepository.GetByCategory(
                categoryId, Math.Clamp(page, 1, MaxPage), Math.Clamp(pageSize, 1, MaxPageSize)));
        }

        [HttpGet("images/recent")]
        public ActionResult<IEnumerable<GalleryImage>> GetRecent(int count = 5) =>
            Ok(_imageRepository.GetRecent(Math.Clamp(count, 1, MaxRecentCount)));

        [HttpGet("images/{id}")]
        public ActionResult<GalleryImage> GetById(int id)
        {
            var image = _imageRepository.GetById(id);
            if (image == null) return NotFound(new { message = "Image not found" });

            _imageRepository.IncrementViewCount(id);
            image.ViewCount++;

            var userId = CurrentUserId;
            image.LikedByCurrentUser = userId.HasValue && _imageRepository.HasLiked(id, userId.Value);
            image.Comments = _imageRepository.GetComments(id).ToList();
            return Ok(image);
        }

        [HttpPost("images")]
        public ActionResult<GalleryImage> Create(CreateImageRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (_categoryRepository.GetById(request.CategoryId) == null)
                return BadRequest(new { message = "Category not found" });

            // Each stored file belongs to exactly one entry. Otherwise a member
            // could point a new entry at someone else's file, delete their own
            // entry, and take the other member's file with it.
            if (_imageRepository.IsImageUrlInUse(request.ImageUrl))
                return Conflict(new { message = "That image file is already in use." });

            var userId = CurrentUserId!.Value;
            var created = _imageRepository.Add(new GalleryImage
            {
                CategoryId = request.CategoryId,
                Name = request.Name,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                FileSizeBytes = request.FileSizeBytes,
                UploadedByUserId = userId
            });
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("images/{id}")]
        public IActionResult Update(int id, UpdateImageRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var image = _imageRepository.GetById(id);
            if (image == null) return NotFound(new { message = "Image not found" });
            if (!CanManage(image.UploadedByUserId)) return Forbid();
            if (_categoryRepository.GetById(request.CategoryId) == null)
                return BadRequest(new { message = "Category not found" });

            _imageRepository.Update(new GalleryImage
            {
                Id = id,
                CategoryId = request.CategoryId,
                Name = request.Name,
                Description = request.Description
            });
            return NoContent();
        }

        [HttpDelete("images/{id}")]
        public IActionResult Delete(int id)
        {
            var image = _imageRepository.GetById(id);
            if (image == null) return NotFound(new { message = "Image not found" });
            if (!CanManage(image.UploadedByUserId)) return Forbid();

            _imageRepository.Remove(id);

            // Only hand the file back for deletion once nothing else points at
            // it (older data may predate the one-entry-per-file rule).
            var fileStillUsed = _imageRepository.IsImageUrlInUse(image.ImageUrl);
            return Ok(new { imageUrl = fileStillUsed ? null : image.ImageUrl });
        }

        [HttpPost("images/{id}/comments")]
        public ActionResult<GalleryComment> AddComment(int id, CreateCommentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (_imageRepository.GetById(id) == null) return NotFound(new { message = "Image not found" });

            var comment = _imageRepository.AddComment(new GalleryComment
            {
                ImageId = id,
                Content = request.Content,
                CreatedByUserId = CurrentUserId!.Value
            });
            return Ok(comment);
        }

        [HttpDelete("comments/{id}")]
        public IActionResult DeleteComment(int id)
        {
            var comment = _imageRepository.GetCommentById(id);
            if (comment == null) return NotFound(new { message = "Comment not found" });
            if (!CanManage(comment.CreatedByUserId)) return Forbid();

            _imageRepository.RemoveComment(id);
            return NoContent();
        }

        [HttpPost("images/{id}/like")]
        public ActionResult ToggleLike(int id)
        {
            var image = _imageRepository.GetById(id);
            if (image == null) return NotFound(new { message = "Image not found" });

            var userId = CurrentUserId!.Value;
            bool liked;
            if (_imageRepository.HasLiked(id, userId))
            {
                _imageRepository.RemoveLike(id, userId);
                liked = false;
            }
            else
            {
                _imageRepository.AddLike(id, userId);
                liked = true;
            }

            var refreshed = _imageRepository.GetById(id)!;
            return Ok(new { liked, likeCount = refreshed.LikeCount });
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private bool CanManage(int ownerUserId) =>
            User.IsInRole("Administrator") || User.IsInRole("Moderator") || CurrentUserId == ownerUserId;
    }
}
