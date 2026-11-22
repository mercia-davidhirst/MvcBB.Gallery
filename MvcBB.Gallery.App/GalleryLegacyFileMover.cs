using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MvcBB.Gallery.App
{
    /// <summary>
    /// On startup, moves any gallery files an earlier version saved under the
    /// public wwwroot into the private gallery folder, so they can only be
    /// opened by signed-in members from then on.
    /// </summary>
    public class GalleryLegacyFileMover : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GalleryLegacyFileMover> _logger;

        public GalleryLegacyFileMover(IServiceScopeFactory scopeFactory, ILogger<GalleryLegacyFileMover> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var storage = scope.ServiceProvider.GetRequiredService<IGalleryImageStorage>();
                if (storage is GalleryImageStorageService service)
                {
                    var moved = service.MoveLegacyPublicFiles();
                    if (moved > 0)
                        _logger.LogInformation("Gallery: moved {Count} image file(s) out of the public web folder.", moved);
                }
            }
            catch (Exception ex)
            {
                // Never stop the forum from starting over this; DeleteImage still
                // cleans up any file left behind.
                _logger.LogError(ex, "Gallery: could not move image files out of the public web folder.");
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
