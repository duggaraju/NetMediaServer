using Microsoft.Extensions.Logging;
using RtmpCore.Amf;
using System;
using System.Threading.Tasks;

namespace RtmpCore
{
    class RtmpDataProcessor : IRtmpMessageProcessor
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<ServerDataProcessor>();
        private readonly RtmpSession _session;

        public RtmpDataProcessor(RtmpSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            var command = new AmfDataMessage();
            command.Decode(message.Payload.Span);
            await HandleDataCommandAsync(command, message);
        }

        public async Task SendDataCommandAsync(AmfDataMessage data)
        {
            var body = new byte[data.GetLength()];
            data.Encode(body.AsSpan());
            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Data);
            var messageHeader = new RtmpMessageHeader(0, body.Length, RtmpMessageType.DataAMF0, 0);
            var message = new RtmpMessage(header, messageHeader, body);
            await _session.SendMessageAsync(message);
        }

        protected virtual Task HandleDataCommandAsync(AmfDataMessage command, RtmpMessage message)
        {
            throw new NotImplementedException($"Do not know how to process {command.Name}");
        }
    }
}
