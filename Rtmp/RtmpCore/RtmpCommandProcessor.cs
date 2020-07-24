using Microsoft.Extensions.Logging;
using RtmpCore.Amf;
using System;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace RtmpCore
{
    public abstract class RtmpCommandProcessor : IRtmpMessageProcessor
    {
        protected readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpCommandProcessor>();
        private readonly RtmpSession _session;

        public RtmpCommandProcessor(RtmpSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            var command = new AmfCommandMessage();
            command.Decode(message.Payload.Span);
            _logger.LogInformation($"Procesing command:{command.Name} for session {_session.Id}s");
            await ProcessCommandAsync(command, message);
        }

        protected abstract Task ProcessCommandAsync(AmfCommandMessage command, RtmpMessage message);

        private async Task SendUserControlMessageAsync(UserControlMessageType type, int data)
        {
            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Protocol);
            var messageHeader = new RtmpMessageHeader(0, 6, RtmpMessageType.UserCtrlMessage, 0);
            var payload = new byte[6];
            BinaryPrimitives.WriteInt16BigEndian(payload.AsSpan(), (short)type);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan().Slice(2), data);
            var message = new RtmpMessage(header, messageHeader, payload);
            await _session.SendMessageAsync(message);
        }

        public async Task SendStatusMessageAsync(int streamId, string level, string code, string description)
        {
            var resultCommand = new AmfCommandMessage
            {
                Name = "onStatus",
                Data = new
                {
                    transId = 0,
                    cmdObj = (object)null,
                    info = new
                    {
                        level = level,
                        code = code,
                        description = description
                    }
                }
            };
            await SendCommandMessageAsync(streamId, command: resultCommand);
        }

        public Task SendWindowAckSizeAsync(int size)
        {
            return SendProtocolControlMessageAsync(RtmpMessageType.WindowAcknowledgementSize, size);
        }

        public Task SetPeerBandwidthAsync(int bandwidth)
        {
            return SendProtocolControlMessageAsync(RtmpMessageType.SetPeerBandwidth, bandwidth);
        }

        public Task SetChunkSizeAsync(int chunkSize)
        {
            return SendProtocolControlMessageAsync(RtmpMessageType.SetChunkSize, chunkSize);
        }

        public async Task SendProtocolControlMessageAsync(RtmpMessageType messageType, int data)
        {
            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Protocol);
            var messageHeader = new RtmpMessageHeader(0, 4, messageType, 0);
            var body = BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(data));
            var message = new RtmpMessage(header, messageHeader, body);
            await _session.SendMessageAsync(message);
        }

        public async Task SendResultAsync(object data)
        {
            var chunkHeader = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Invoke);
            var response = new AmfCommandMessage
            {
                Name = "_result",
                Data = data
            };
            var length = response.GetLength();
            var memory = new byte[length];
            response.Encode(memory);
            var messageHeader = new RtmpMessageHeader(0, length, RtmpMessageType.CommandAMF0, 0);
            var message = new RtmpMessage(chunkHeader, messageHeader, memory);
            await _session.SendMessageAsync(message);
        }

        public async Task SendCommandMessageAsync(int streamId, AmfCommandMessage command)
        {
            var body = new byte[command.GetLength()];
            command.Encode(body.AsSpan());
            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Invoke);
            var messageHeader = new RtmpMessageHeader(0, body.Length, RtmpMessageType.CommandAMF0, streamId);
            var message = new RtmpMessage(header, messageHeader, body);
            await _session.SendMessageAsync(message);
        }

        public async Task SendDataMessageAsync(int streamId, AmfDataMessage data)
        {
            var body = new byte[data.GetLength()];
            data.Encode(body.AsSpan());
            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Data);
            var messageHeader = new RtmpMessageHeader(0, body.Length, RtmpMessageType.DataAMF0, 0);
            var message = new RtmpMessage(header, messageHeader, body);
            await _session.SendMessageAsync(message);
        }
    }
}
