using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MvcBB.Gallery.App
{
    /// <summary>Client for MvcBB.Gallery.Api's endpoints. Every action in this mod
    /// requires login (no anonymous access), so every call attaches the signed-in
    /// user's own token - there's no separate anonymous/service-token path here.</summary>
    public class GalleryService : IGalleryService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public GalleryService(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClientFactory.CreateClient("GalleryApi");
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<GalleryCategoryDto>> GetCategoriesAsync()
        {
            Authorize();
            var response = await _httpClient.GetAsync("api/mods/gallery/categories");
            await EnsureSuccessAsync(response);
            return (await DeserializeAsync<List<GalleryCategoryDto>>(response)) ?? new List<GalleryCategoryDto>();
        }

        public async Task<GalleryCategoryDto?> GetCategoryAsync(int id)
        {
            Authorize();
            var response = await _httpClient.GetAsync($"api/mods/gallery/categories/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureSuccessAsync(response);
            return await DeserializeAsync<GalleryCategoryDto>(response);
        }

        public async Task<GalleryCategoryDto> CreateCategoryAsync(GalleryCategoryDto category)
        {
            Authorize();
            var response = await PostJsonAsync("api/mods/gallery/categories", category);
            await EnsureSuccessAsync(response);
            return (await DeserializeAsync<GalleryCategoryDto>(response))!;
        }

        public async Task UpdateCategoryAsync(int id, GalleryCategoryDto category)
        {
            Authorize();
            var response = await PutJsonAsync($"api/mods/gallery/categories/{id}", category);
            await EnsureSuccessAsync(response);
        }

        public async Task DeleteCategoryAsync(int id)
        {
            Authorize();
            var response = await _httpClient.DeleteAsync($"api/mods/gallery/categories/{id}");
            await EnsureSuccessAsync(response);
        }

        public async Task<PagedImagesDto> GetImagesAsync(int categoryId, int page)
        {
            Authorize();
            var response = await _httpClient.GetAsync($"api/mods/gallery/categories/{categoryId}/images?page={page}");
            await EnsureSuccessAsync(response);
            return (await DeserializeAsync<PagedImagesDto>(response)) ?? new PagedImagesDto();
        }

        public async Task<GalleryImageDto?> GetImageAsync(int id)
        {
            Authorize();
            var response = await _httpClient.GetAsync($"api/mods/gallery/images/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureSuccessAsync(response);
            return await DeserializeAsync<GalleryImageDto>(response);
        }

        public async Task<IEnumerable<GalleryImageDto>> GetRecentAsync(int count)
        {
            Authorize();
            var response = await _httpClient.GetAsync($"api/mods/gallery/images/recent?count={count}");
            await EnsureSuccessAsync(response);
            return (await DeserializeAsync<List<GalleryImageDto>>(response)) ?? new List<GalleryImageDto>();
        }

        public async Task<GalleryImageDto> CreateImageAsync(int categoryId, string name, string? description, string imageUrl, long fileSizeBytes)
        {
            Authorize();
            var response = await PostJsonAsync("api/mods/gallery/images", new
            {
                categoryId,
                name,
                description,
                imageUrl,
                fileSizeBytes
            });
            await EnsureSuccessAsync(response);
            return (await DeserializeAsync<GalleryImageDto>(response))!;
        }

        public async Task UpdateImageAsync(int id, int categoryId, string name, string? description)
        {
            Authorize();
            var response = await PutJsonAsync($"api/mods/gallery/images/{id}", new { categoryId, name, description });
            await EnsureSuccessAsync(response);
        }

        public async Task<string?> DeleteImageAsync(int id)
        {
            Authorize();
            var response = await _httpClient.DeleteAsync($"api/mods/gallery/images/{id}");
            await EnsureSuccessAsync(response);
            var result = await DeserializeAsync<JsonElement>(response);
            return result.TryGetProperty("imageUrl", out var url) ? url.GetString() : null;
        }

        public async Task AddCommentAsync(int imageId, string content)
        {
            Authorize();
            var response = await PostJsonAsync($"api/mods/gallery/images/{imageId}/comments", new { content });
            await EnsureSuccessAsync(response);
        }

        public async Task DeleteCommentAsync(int commentId)
        {
            Authorize();
            var response = await _httpClient.DeleteAsync($"api/mods/gallery/comments/{commentId}");
            await EnsureSuccessAsync(response);
        }

        public async Task<(bool Liked, int LikeCount)> ToggleLikeAsync(int imageId)
        {
            Authorize();
            var response = await _httpClient.PostAsync($"api/mods/gallery/images/{imageId}/like", content: null);
            await EnsureSuccessAsync(response);
            var result = await DeserializeAsync<JsonElement>(response);
            return (result.GetProperty("liked").GetBoolean(), result.GetProperty("likeCount").GetInt32());
        }

        private void Authorize()
        {
            var token = _httpContextAccessor.HttpContext?.User.FindFirst("api_token")?.Value
                ?? throw new GalleryServiceException("Your session has expired. Please log in again.");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private Task<HttpResponseMessage> PostJsonAsync(string url, object payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return _httpClient.PostAsync(url, content);
        }

        private Task<HttpResponseMessage> PutJsonAsync(string url, object payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return _httpClient.PutAsync(url, content);
        }

        private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return default;
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new GalleryServiceException("Your session has expired. Please log in again.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new GalleryServiceException("You don't have permission to do that.");

            var body = await response.Content.ReadAsStringAsync();
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("message", out var message))
                    throw new GalleryServiceException(message.GetString() ?? "The gallery service returned an error.");
            }
            catch (JsonException)
            {
                // Not a JSON error body - fall through to the generic exception below.
            }

            response.EnsureSuccessStatusCode();
        }
    }
}
