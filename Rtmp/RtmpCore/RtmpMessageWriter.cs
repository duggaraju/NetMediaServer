using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RtmpCore
{
    class RtmpMessageWriter : IRtmpMessageProcessor
    {
        private readonly Stream _stream;
        private readonly RtmpSession _session;
        private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;

        public RtmpMessageWriter(RtmpSession session, Stream stream)
        {
            _session = session ?? throw new ArgumentException(nameof(session));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            var maxChunkLength = _session.OutgoingChunkLength + RtmpChunk.MaxHeaderLength;

            using var buffer = _memoryPool.Rent(maxChunkLength);
            foreach (var chunk in ChunkMessage(message))
            {
                var memory = buffer.Memory.Slice(0, chunk.Length);
                chunk.Write(memory.Span);
                await _stream.WriteAsync(memory);
            }
        }

        public IEnumerable<RtmpChunk> ChunkMessage(RtmpMessage message)
        {
            var length = message.Message.Length;
            var memory = message.Payload;
            var offset = 0;
            while(length != 0)
            {
                var chunkHeader = new RtmpChunkHeader(
                    offset == 0 ? RtmpChunkHeaderType.Type0 : RtmpChunkHeaderType.Type2,
                    message.Header.StreamId);
                var chunkLength = Math.Min(_session.OutgoingChunkLength, length);
                var chunk = new RtmpChunk(chunkHeader, message.Message, memory.Slice(offset, chunkLength));
                yield return chunk;
                length -= chunkLength;
                offset += chunkLength;
            }
        }
    }
}
