using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpMessageWriter : IRtmpMessageProcessor
    {
        private readonly Stream _stream;
        private readonly RtmpSession _session;
        private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
        private readonly Channel<RtmpMessage> _channel = Channel.CreateBounded<RtmpMessage>(10);

        public RtmpMessageWriter(RtmpSession session, Stream stream)
        {
            _session = session ?? throw new ArgumentException(nameof(session));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _ = SendMessagesAsync();
        }

        public void Stop()
        {
            _channel.Writer.Complete();
            _channel.Reader.Completion.Wait();
        }


        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            await _channel.Writer.WriteAsync(message);
        }

        private async Task SendMessagesAsync()
        {
            while (await _channel.Reader.WaitToReadAsync())
                while (_channel.Reader.TryRead(out var message))
                    await SendMessageAsync(message);
        }

        private async Task SendMessageAsync(RtmpMessage message)
        {
            var chunkPayloadLength = Math.Min(message.Payload.Length, _session.OutgoingChunkLength);

            using var buffer = _memoryPool.Rent(chunkPayloadLength + RtmpChunk.MaxHeaderLength);
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
