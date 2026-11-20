using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MvcBB.ModKit;

namespace MvcBB.Gallery.App
{
    public class GalleryAppMod : IAppMod
    {
        public string Id => "gallery";
        public string Name => "Gallery";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            var apiBaseUrl = configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException("Api:BaseUrl is not configured");

            services.AddHttpClient("GalleryApi", client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddScoped<IGalleryService, GalleryService>();
            services.AddScoped<IGalleryImageStorage, GalleryImageStorageService>();
        }

        public IEnumerable<ModNavItem> GetNavItems()
        {
            yield return new ModNavItem("Gallery", "Gallery", "Index", "bi-images");
        }
    }
}
