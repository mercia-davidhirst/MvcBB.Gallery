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

        public GalleryCategoriesController(IGalleryCategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
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
        public IActionResult Delete(int id)
        {
            if (_categoryRepository.GetById(id) == null) return NotFound(new { message = "Category not found" });

            _categoryRepository.Remove(id);
            return NoContent();
        }
    }
}
