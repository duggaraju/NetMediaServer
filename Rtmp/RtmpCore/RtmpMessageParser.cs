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

        private readonly IRtmpMessageProcessor _messageProcessor;
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

        public RtmpMessageParser(RtmpSession session, IRtmpMessageProcessor messageProcessor)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        }

        public async Task ParseMessagesAsync(PipeReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                try
                {
                    var messages = ParseMessages(reader, result.Buffer);
                    foreach (var message in messages)
                    {
                        await _messageProcessor.ProcessMessageAsync(message);
                        message.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse chunks");
                    break;
                }

                if (result.IsCompleted)
                    break;
            }
        }

        private IEnumerable<RtmpMessage> ParseMessages(PipeReader reader, ReadOnlySequence<byte> buffer)
        {
            var messages = new List<RtmpMessage>();
            var sequenceReader = new SequenceReader<byte>(buffer);
            var position = sequenceReader.Position;
            while (sequenceReader.Remaining != 0)
            {
                if (!TryParseChunk(ref sequenceReader, out var chunk, out var messageEntry))
                    break;
                position = sequenceReader.Position;

                AssembleMessage(messageEntry, chunk);

                // if message is complete .. process it.
                if (messageEntry.BytesRemaining == 0)
                {
                    var message = messageEntry.Message;
                    _logger.LogInformation($"Parsed message :{message.Header}: Message: {message.Message}");
                    messages.Add(message);
                    messageEntry.BytesRead = 0;
                    messageEntry.Message = null;
                }

            }
            reader.AdvanceTo(position);
            return messages;
        }

        private void AssembleMessage(RtmpMessageEntry messageEntry, RtmpChunk chunk)
        {
            var message = messageEntry.Message;
            if (message == null)
            {
                var payload = _memoryPool.Rent(chunk.Message.Length);
                message = new RtmpMessage(chunk.Header, chunk.Message, payload);
                messageEntry.Message = message;
            }

            var memory = message.Payload.Slice(messageEntry.BytesRead, chunk.PayloadLength);
            var reader = new SequenceReader<byte>(chunk.Payload);
            reader.TryCopyTo(memory.Span);
            messageEntry.BytesRead += chunk.PayloadLength;
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
