using System;
using System.Threading.Tasks;
using MediaCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WebServer
{
    public class HlsStreamingHandler
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public HlsStreamingHandler(IMemoryCache memoryCache, ILogger<HlsStreamingHandler> logger)
        {
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var segment = _cache.Get<MediaSegment>(context.Request.Path);
            if (segment == null)
                await NotFoundAsync(context.Response);
            else
                await SendResponse(context.Response, segment);
        }

        private static async Task NotFoundAsync(HttpResponse response)
        {
            response.StatusCode = 404;
            await response.WriteAsync("Not Found!");
        }

        private async Task SendResponse(HttpResponse response, MediaSegment mediaSegment)
        {
            response.StatusCode = 200;
            var length = mediaSegment.Length;
            if (mediaSegment.Complete)
            {
                response.ContentLength = mediaSegment.Length;
                _logger.LogWarning($"Sending full response for {mediaSegment.Path} length:{mediaSegment.Length} ");
            }
            else
            {
                _logger.LogWarning($"Chunked transfer encoding for {mediaSegment.Path}");
            }
            response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                NoCache = true,
                NoStore = true
            };

            foreach (var index in mediaSegment.GetBufferIndex())
            {
                var cacheKey = mediaSegment.GetChunkKey(index);
                var buffer = _cache.Get<MediaBuffer>(cacheKey);
                if (buffer == null)
                    throw new InvalidOperationException($"Missing cache entry for {cacheKey}");
                await response.Body.WriteAsync(buffer.Memory);
                await response.Body.FlushAsync();
            }
        }
    }
}
