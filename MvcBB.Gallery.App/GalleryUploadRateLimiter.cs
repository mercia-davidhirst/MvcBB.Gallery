namespace MvcBB.Gallery.App
{
    /// <summary>
    /// Caps how many uploads one member can make in a short window, so nobody
    /// can fill the server's disk by uploading over and over. Kept in memory
    /// per App process (mods can't add the host's own rate-limiting
    /// middleware), so the count resets when MvcBB.App restarts.
    /// </summary>
    public class GalleryUploadRateLimiter
    {
        public const int MaxUploads = 20;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        private readonly Dictionary<int, Queue<DateTime>> _uploadsByUser = new();
        private readonly object _lock = new();

        /// <summary>Records an upload attempt for the user, or returns false if they're over the limit.</summary>
        public bool TryAcquire(int userId)
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if (!_uploadsByUser.TryGetValue(userId, out var uploads))
                {
                    uploads = new Queue<DateTime>();
                    _uploadsByUser[userId] = uploads;
                }

                while (uploads.Count > 0 && now - uploads.Peek() >= Window)
                    uploads.Dequeue();

                if (uploads.Count >= MaxUploads)
                    return false;

                uploads.Enqueue(now);

                // Drop members with no recent uploads so the map can't grow forever.
                if (_uploadsByUser.Count > 1000)
                {
                    foreach (var key in _uploadsByUser.Where(kv => kv.Value.Count == 0 || now - kv.Value.Last() >= Window).Select(kv => kv.Key).ToList())
                        _uploadsByUser.Remove(key);
                }

                return true;
            }
        }
    }
}
