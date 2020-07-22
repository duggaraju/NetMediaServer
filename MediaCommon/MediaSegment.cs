using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MediaCommon
{  
    public sealed class MediaSegment
    {
        private int _length = 0;
        private volatile int _bufferCount = 0;
        private volatile bool _complete = false;
        private readonly ManualResetEventSlim _event = new ManualResetEventSlim(false);
        private readonly ILogger _logger;

        /// <summary>
        /// Indicates whether the buffer is complete or still being created.
        /// </summary>
        public int Length => _complete?  _length : 0;

        public string ContentType { get; }

        public int BufferCount => _bufferCount;

        public string Path { get; }

        public bool Complete => _complete;

        public string GetChunkKey(int index) => $"{Path}.{index}";

        public MediaSegment(string path, string contentType, ILogger logger)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ContentType = contentType ?? "application/octet-stream";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddBuffer(int length, bool last)
        {
            if (_complete) 
                throw new InvalidOperationException("Buffer is already complete!");
            _length += length;
            _bufferCount++;
            _complete = last;
            _event.Set();
        }

        public async IAsyncEnumerable<int> GetBufferIndexAsync()
        {
            var i = 0;
            while (i < _bufferCount)
                yield return i++;
            while (!_complete)
            {
                _logger.LogDebug($"${Path} is incomplete, waiting for more buffers to arrive ...");
                var got = _event.Wait(TimeSpan.FromMilliseconds(100));
                if (got && i < _bufferCount)
                    yield return i++;
            }
        }
    }
}
