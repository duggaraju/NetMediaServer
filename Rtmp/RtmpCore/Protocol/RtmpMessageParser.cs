using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpMessageParser
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpMessageParser>();
        private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
        private readonly Dictionary<int, RtmpMessageEntry> _messageCache = new Dictionary<int, RtmpMessageEntry>();
        private readonly RtmpSession _session;

        class RtmpMessageEntry
        {
            public int Length { get; set; }

            public RtmpChunk LastChunk { get; set; }

            public RtmpMessage Message { get; set; }

            public int BytesRead { get; set; }

            public int BytesRemaining => Length - BytesRead;
        }

        public RtmpMessageParser(RtmpSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ParseMessagesAsync(PipeReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                try
                {
                    var position = TryParseChunk(reader, result.Buffer, out var chunk, out var messageEntry);
                    if (position == null)
                    {
                        position = result.Buffer.Start;
                    }
                    else if (AssembleMessage(messageEntry, chunk, out var message))
                    {
                        await _session.DispatchMessageAsync(message);
                        message.Dispose();
                    }
                    reader.AdvanceTo(position.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse chunks");
                }

                if (result.IsCompleted)
                    break;
            }
        }

        /// <summary>
        /// Try and parse the next RTMP chunk and advance the reader. 
        /// </summary>
        /// <returns>true if chunk is available else false.</returns>
        private SequencePosition? TryParseChunk(PipeReader reader, ReadOnlySequence<byte> buffer, out RtmpChunk chunk, out RtmpMessageEntry messageEntry)
        {
            var sequenceReader = new SequenceReader<byte>(buffer);
            if (!TryParseChunk(ref sequenceReader, out chunk, out messageEntry))
                return null;
            return sequenceReader.Position;
        }

        private bool AssembleMessage(RtmpMessageEntry messageEntry, RtmpChunk chunk, out RtmpMessage message)
        {
            message = null;
            var currentMessage = messageEntry.Message;
            if (currentMessage == null)
            {
                var payload = _memoryPool.Rent(chunk.Message.Length);
                currentMessage = new RtmpMessage(chunk.Header, chunk.Message, payload);
                messageEntry.Message = currentMessage;
            }

            var memory = currentMessage.Payload.Slice(messageEntry.BytesRead, chunk.PayloadLength);
            var reader = new SequenceReader<byte>(chunk.Payload);
            reader.TryCopyTo(memory.Span);
            messageEntry.BytesRead += chunk.PayloadLength;
            // if message is complete .. return it.
            if (messageEntry.BytesRemaining == 0)
            {
                message = messageEntry.Message;
                _logger.LogInformation($"Parsed message :{message.Header}: Message: {message.Message}");
                messageEntry.BytesRead = 0;
                messageEntry.Message = null;
                return true;
            }
            return false;
        }

        private bool TryParseChunk(ref SequenceReader<byte> reader, out RtmpChunk chunk, out RtmpMessageEntry messageEntry)
        {
            messageEntry = null;
            chunk = null;
            _logger.LogDebug($"Before parsing chunk: Sequence Reader Length:{reader.Length}: Consumed:{reader.Consumed}  Remaining:{reader.Remaining}");
            var header = new RtmpChunkHeader();
            if (!header.TryParse(ref reader))
                return false;

            if (!_messageCache.TryGetValue(header.StreamId, out messageEntry))
            {
                messageEntry = new RtmpMessageEntry();
                _messageCache.Add(header.StreamId, messageEntry);
            }

            var message = new RtmpMessageHeader();
            if (!message.TryParse(ref reader, header.HeaderType, messageEntry.LastChunk?.Message))
                return false;

            chunk = new RtmpChunk(header, message);
            if (header.HeaderType != RtmpChunkHeaderType.Type3)
            {
                messageEntry.Length = chunk.Message.Length;
                messageEntry.BytesRead = 0;
            }
            else
            {
                
            }

            var chunkLength = Math.Min(messageEntry.BytesRemaining, _session.IncomingChunkLength);
            if (!chunk.TryParseBody(ref reader, chunkLength))
                return false;
            _logger.LogDebug($"After parsing chunk: Sequence Reader Length:{reader.Length}: Consumed:{reader.Consumed}  Remaining:{reader.Remaining}");
            _logger.LogDebug($"Parsed chunk Header :{chunk.Header}: Message: {chunk.Message}");
            messageEntry.LastChunk = chunk;
            return true;
        }
    }
}
