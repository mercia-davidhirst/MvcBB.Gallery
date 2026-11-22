using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MvcBB.ModKit;

namespace MvcBB.Gallery.Api
{
    [Mod("gallery")]
    [ApiController]
    [Route("api/mods/gallery/categories")]
    [Authorize]
    public class GalleryCategoriesController : ControllerBase
    {
        private readonly IGalleryCategoryRepository _categoryRepository;
        private readonly IGalleryImageRepository _imageRepository;

        public GalleryCategoriesController(IGalleryCategoryRepository categoryRepository, IGalleryImageRepository imageRepository)
        {
            _categoryRepository = categoryRepository;
            _imageRepository = imageRepository;
        }

        [HttpGet]
        public ActionResult<IEnumerable<GalleryCategory>> GetAll() => Ok(_categoryRepository.GetAll());

        [HttpGet("{id}")]
        public ActionResult<GalleryCategory> GetById(int id)
        {
            var category = _categoryRepository.GetById(id);
            return category == null ? NotFound(new { message = "Category not found" }) : Ok(category);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public ActionResult<GalleryCategory> Create(CreateCategoryRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var created = _categoryRepository.Add(new GalleryCategory
            {
                Name = request.Name,
                Description = request.Description,
                SortOrder = request.SortOrder
            });
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        public IActionResult Update(int id, UpdateCategoryRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (_categoryRepository.GetById(id) == null) return NotFound(new { message = "Category not found" });

            _categoryRepository.Update(new GalleryCategory
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                SortOrder = request.SortOrder
            });
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public IActionResult Delete(int id, int? moveToCategoryId = null)
        {
            if (_categoryRepository.GetById(id) == null) return NotFound(new { message = "Category not found" });

            if (moveToCategoryId.HasValue)
            {
                if (moveToCategoryId.Value == id || _categoryRepository.GetById(moveToCategoryId.Value) == null)
                    return BadRequest(new { message = "Choose a different, existing category to move the images to." });

                _imageRepository.MoveCategoryImages(id, moveToCategoryId.Value);
                _categoryRepository.Remove(id);
                return Ok(new { imageUrls = Array.Empty<string>() });
            }

            // Deleting the category cascades to its images. Return their files
            // so the App can remove them from disk too, leaving out any file
            // another entry still points at.
            var imageUrls = _imageRepository.GetImageUrlsByCategory(id);
            _categoryRepository.Remove(id);
            var orphanedUrls = imageUrls.Distinct().Where(url => !_imageRepository.IsImageUrlInUse(url)).ToList();
            return Ok(new { imageUrls = orphanedUrls });
        }
    }
}
