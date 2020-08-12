using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MediaCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

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
                await SendResponse(context.Request, context.Response, segment);
        }

        private static async Task NotFoundAsync(HttpResponse response)
        {
            response.StatusCode = 404;
            await response.WriteAsync("Not Found!");
        }

        private async Task SendResponse(HttpRequest request, HttpResponse response, MediaSegment mediaSegment)
        {
            response.Headers.Add("Accept-Ranges", "bytes");
            if (mediaSegment.Complete)
            {
                var length = mediaSegment.Length;
                response.ContentLength = mediaSegment.Length;
                _logger.LogInformation($"Sending complete segment for {mediaSegment.Path} length:{mediaSegment.Length} ");
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

            // check for byte range.
            var values = request.Headers["Range"];
            if (values.Count > 0 )
            {
                var rangeHeader = RangeHeaderValue.Parse(values[0]);
                response.StatusCode = 206;
                var range = rangeHeader.Ranges.Single(); // only one range is supported.
                var offset = range.From ?? 0;
                var length = range.To.HasValue ? range.To.Value - offset : long.MaxValue;
                length = mediaSegment.Complete ? Math.Min(length, mediaSegment.Length) : length;
                response.Headers.ContentLength = length;
                var lengthString = mediaSegment.Complete ? mediaSegment.Length.ToString() : "*";
                response.Headers.Add("Content-Range", $"bytes {range.From}-{range.To}/{lengthString}");
                foreach (var buffer in GetRangeBuffers(mediaSegment, (int) offset, (int)length))
                    await SendBufferAsync(response, buffer, !mediaSegment.Complete);
            }
            else
            {
                response.StatusCode = 200;

                foreach (var index in mediaSegment.GetBufferIndex())
                {
                    var buffer = GetMediaBuffer(mediaSegment, index);
                    await SendBufferAsync(response, buffer.Memory, flush: !mediaSegment.Complete);
                }
            }

        }

        private MediaBuffer GetMediaBuffer(MediaSegment mediaSegment, int index)
        {
            var cacheKey = mediaSegment.GetChunkKey(index);
            var buffer = _cache.Get<MediaBuffer>(cacheKey);
            if (buffer == null)
                throw new InvalidOperationException($"Missing cache entry for {cacheKey}");
            return buffer;
        }

        private async Task SendBufferAsync(HttpResponse respone, ReadOnlyMemory<byte> memory, bool flush = false)
        {
            await respone.Body.WriteAsync(memory);
            if (flush)
                await respone.Body.FlushAsync();
        }

        private IEnumerable<ReadOnlyMemory<byte>> GetRangeBuffers(MediaSegment segment, int offset, int length)
        {
            var curOffset = 0;
            foreach (var index in segment.GetBufferIndex())
            {
                var buffer = GetMediaBuffer(segment, index);
                if (offset > curOffset + buffer.Length)
                {
                    curOffset += buffer.Length;
                    continue;
                }

                var curLength = Math.Min(length, buffer.Length);
                yield return buffer.Memory.Slice(offset - curOffset, curLength);

                length -= curLength;
                if (length == 0)
                    break;
            }
        }
    }
}
