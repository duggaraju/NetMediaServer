using System;
using System.Buffers;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MediaCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WebServer
{
    [MemoryDiagnoser]
    public class DashIngestHandler
    {
        const int BUFFER_SIZE = 16 * 1024;
        private readonly MemoryPool<byte> _pool = MemoryPool<byte>.Shared;
        private readonly IMemoryCache _cache;
        const string ManifestContentType = "application/dash+xml";
        const string HlsContentType = "text/m3u8";
        const string CmafSegmentContentType = "video/mp4";
        private readonly ILogger _logger;

        public DashIngestHandler(IMemoryCache memoryCache, ILogger<DashIngestHandler> logger)
        {
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Benchmark]
        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogInformation(
                $"{context.Request.Method} {context.Request.Path} type: {context.Request.ContentType}  size: {context.Request.Headers.ContentLength}  Encoding:{context.Request.Headers["Transfer-Encoding"]}");

            var options = new MemoryCacheEntryOptions();
            var path = context.Request.Path.Value;
            string contentType = context.Request.ContentType;
            var expire = false;
            if (path.Contains("/chunk"))
            {
                _logger.LogInformation(
                    $"Chunk {context.Request.Path} size: {context.Request.Headers.ContentLength}  Encoding:{context.Request.Headers["Transfer-Encoding"]}");
                contentType = CmafSegmentContentType;
                options.SetSlidingExpiration(TimeSpan.FromSeconds(60));
                expire = true;
            }
            else if (path.Contains("/init"))
            {
                contentType = CmafSegmentContentType;
                options.Priority = CacheItemPriority.NeverRemove;
            }
            else if (path.EndsWith(".mpd"))
            {
                contentType = ManifestContentType;
                options.Priority = CacheItemPriority.NeverRemove;
            }
            else if (path.EndsWith(".m3u8"))
            {
                contentType = HlsContentType;
                options.Priority = CacheItemPriority.NeverRemove;
            }

            var mediaBuffer = new MediaSegment(context.Request.Path.Value, contentType, _logger);
            _cache.Set<MediaSegment>(context.Request.Path, mediaBuffer, options);

            try
            {
                await ReadBodyAsync(context.Request, mediaBuffer, expire);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed to read body for {context.Request.Path}");
                _cache.Remove(context.Request.Path);
            }

            _logger.LogInformation($"completed {context.Request.Path} size:{mediaBuffer.Length} type:{contentType} ");
            context.Response.StatusCode = 201;
        }

        private async Task ReadBodyAsync(HttpRequest request, MediaSegment mediaSegment, bool expire)
        {
            int count = 0;
            while (true)
            {
                var (buffer, endOfStream) = await ReadBufferAsync(request);
                var cacheKey = mediaSegment.GetChunkKey(count++);
                var options = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = expire ? TimeSpan.FromSeconds(60) : (TimeSpan?)null,
                    Priority = expire ? CacheItemPriority.Normal : CacheItemPriority.NeverRemove,
                    Size = BUFFER_SIZE
                };
                options.RegisterPostEvictionCallback(RemoveCacheEntry);
                _cache.Set<MediaBuffer>(cacheKey, buffer, options);
                _logger.LogDebug($"Got buffer of size: {buffer.Length} for {cacheKey} ");
                mediaSegment.AddBuffer(buffer.Length, endOfStream);
                if (endOfStream)
                    break;
            }
        }

        private async Task<(MediaBuffer, bool)> ReadBufferAsync(HttpRequest request)
        {
            IMemoryOwner<byte> memoryBuffer = null;
            try
            {
                bool endOfStream = false;
                int bufferLength = 0;

                memoryBuffer = _pool.Rent(BUFFER_SIZE);
                var memory = memoryBuffer.Memory;
                while (memory.Length > 0)
                {
                    var bytesRead = await request.Body.ReadAsync(memory);
                    if (bytesRead == 0)
                    {
                        endOfStream = true;
                        break;
                    }

                    bufferLength += bytesRead;
                    memory = memory.Slice(bytesRead);
                }
                var mediaBuffer = new MediaBuffer(memoryBuffer, bufferLength);
                memoryBuffer = null; // avoid it being released.
                return (mediaBuffer, endOfStream);
            }
            finally
            {
                memoryBuffer?.Dispose();
            }
        }

        private static void RemoveCacheEntry(object key, object value, EvictionReason reason , object state)
        {
            if (value is MediaBuffer buffer)
                buffer.Release();
        }
    }
}
