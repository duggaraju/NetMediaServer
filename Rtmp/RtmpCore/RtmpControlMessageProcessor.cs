using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpControlMessageProcessor : IRtmpMessageProcessor
    {
        private readonly RtmpSession _sesion;
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpControlMessageProcessor>();

        public RtmpControlMessageProcessor(RtmpSession session)
        {
            _sesion = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task ProcessMessageAsync(RtmpMessage message)
        {
            BinaryPrimitives.TryReadInt32BigEndian(message.Payload.Span, out var value);
            switch(message.Message.MessageType)
            {
                case RtmpMessageType.SetChunkSize:
                    SetChunkSize(value);
                    break;
                case RtmpMessageType.SetPeerBandwidth:
                    SetPeerBandwidth(value);
                    break;
                case RtmpMessageType.WindowAcknowledgementSize:
                    SetWindowAcknowledgementSize(value);
                    break;
                case RtmpMessageType.Acknowledgement:
                    break;
                default:
                    throw new Exception($"Unknown control command {message.Message.MessageType}");
            }

            return Task.CompletedTask;
        }

        private void SetWindowAcknowledgementSize(int value)
        {
            _sesion.WindowAcknowledgementSize = value;
        }

        public void SetChunkSize(int chunkSize)
        {
            _logger.LogInformation("Chunk size set to: {0}", chunkSize);
            _sesion.IncomingChunkLength = chunkSize;
        }

        public void SetPeerBandwidth(int bandWidth)
        {
            _logger.LogDebug("Bandwidth set to: {0}", bandWidth);
        }

        public void Acknowledgement(int value)
        {
            _logger.LogDebug("Received acknowledgment: {0}", value);
        }
    }
}
