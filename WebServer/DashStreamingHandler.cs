using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MediaCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WebServer
{
    [MemoryDiagnoser]
    public class DashStreamingHandler
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public DashStreamingHandler(IMemoryCache memoryCache, ILogger<DashStreamingHandler> logger)
        {
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Benchmark]
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
            if (length != 0)
            {
                response.ContentLength = mediaSegment.Length;
                _logger.LogWarning($"Sending segment for {mediaSegment.Path} length:{length} complete: {mediaSegment.Complete}");
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

            await foreach (var index in mediaSegment.GetBufferIndexAsync())
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
