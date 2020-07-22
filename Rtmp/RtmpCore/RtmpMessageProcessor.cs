using System;
using System.Threading.Tasks;

namespace RtmpCore
{
    class RtmpMessageProcessor : IRtmpMessageProcessor
    {
        private readonly IRtmpMessageProcessor _commandProcessor;
        private readonly IRtmpMessageProcessor _dataProcessor;
        private readonly IRtmpMessageProcessor _controlMessageProcessor;
        private readonly IRtmpMessageProcessor _mediaProcessor;
        private readonly IRtmpMessageProcessor _eventProcessor;

        public RtmpMessageProcessor(
            IRtmpMessageProcessor commandProcessor,
            IRtmpMessageProcessor controlProcessor,
            IRtmpMessageProcessor dataProcessor,
            IRtmpMessageProcessor mediaProcessor,
            IRtmpMessageProcessor eventProcessor)
        {
            _commandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));
            _controlMessageProcessor = controlProcessor ?? throw new ArgumentNullException(nameof(controlProcessor));
            _dataProcessor = dataProcessor ?? throw new ArgumentException(nameof(dataProcessor));
            _mediaProcessor = mediaProcessor ?? throw new ArgumentNullException(nameof(mediaProcessor));
            _eventProcessor = eventProcessor ?? throw new ArgumentNullException(nameof(eventProcessor));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            switch (message.Message.MessageType)
            {
                case RtmpMessageType.SetChunkSize:
                case RtmpMessageType.Acknowledgement:
                case RtmpMessageType.WindowAcknowledgementSize:
                case RtmpMessageType.SetPeerBandwidth:
                    await _controlMessageProcessor.ProcessMessageAsync(message);
                    break;
                case RtmpMessageType.CommandAMF0:
                case RtmpMessageType.CommandAMF3:
                    await _commandProcessor.ProcessMessageAsync(message);
                    break;
                case RtmpMessageType.DataAMF0:
                case RtmpMessageType.DataAMF3:
                    await _dataProcessor.ProcessMessageAsync(message);
                    break;
                case RtmpMessageType.Audio:
                case RtmpMessageType.Video:
                    await _mediaProcessor.ProcessMessageAsync(message);
                    break;
                case RtmpMessageType.UserCtrlMessage:
                    await _eventProcessor.ProcessMessageAsync(message);
                    break;
                default:
                    throw new Exception($"Unknown message of type: {message.Message.MessageType}");
            }
        }
    }
}
