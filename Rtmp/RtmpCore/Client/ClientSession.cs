using Microsoft.Extensions.Options;
using RtmpCore.Amf;
using RtmpCore.Session;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class ClientSession : RtmpSession
    {
        private readonly IOptions<ClientConfiguration> _configuration;
        private readonly ClientCommandProcessor _commandProcessor;
        private readonly RtmpDataProcessor _dataProcessor;
        private readonly CancellationTokenSource _source = new CancellationTokenSource();

        public ClientSession(TcpClient client, IOptions<ClientConfiguration> configuration) : base(client)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _commandProcessor = new ClientCommandProcessor(this);
            _dataProcessor = new RtmpDataProcessor(this);
        }

        public void Start()
        {
            Task.Run(async () => await RunAsync(_source.Token));
        }

        public void Stop()
        {
            _source.Cancel();
        }

        public async Task ConnectAsync(string appName)
        {
            var command = new AmfCommandMessage
            {
                Name = "connect",
                Data = new
                {
                    app = appName,
                    flashVer = _configuration.Value.FlashVersion,
                    objectEncoding = (double)AmfEncodingType.Amf0
                }
            };

            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        public async Task StartPlayback(string streamName)
        {
            if (IsPublishing || IsPlaying) throw new InvalidOperationException("Cannot start playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "connect",
                Data = new
                {
                    app = streamName,
                    flashVer = _configuration.Value.FlashVersion,
                    objectEncoding = (double)AmfEncodingType.Amf0
                }
            };
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        public async Task StopPlayback()
        {
            if (IsPublishing || !IsPlaying) throw new InvalidOperationException("Cannot stop playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "deleteStream",
                Data = new
                {
                    app = PlayStream.Name,
                    flashVer = _configuration.Value.FlashVersion,
                    objectEncoding = (double)AmfEncodingType.Amf0
                }
            };
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        public async Task StartPublishAsync(string streamName)
        {
            if (IsPublishing || IsPlaying) throw new InvalidOperationException("Cannot start playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "publish",
                Data = new
                {
                    app = streamName,
                    flashVer = _configuration.Value.FlashVersion,
                    objectEncoding = (double)AmfEncodingType.Amf0
                }
            };
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        public async Task SetMetadataAsync(StreamMetadata metadata)
        {
            if (!IsPublishing || IsPlaying) throw new InvalidOperationException("Cannot set metdata in current state.");
            var command = new AmfDataMessage
            {
                Name = "onMetadata",
                Data = new
                {
                    dataObj = metadata,
                }
            };
            await _dataProcessor.SendDataCommandAsync(command);
        }

        public async Task StopPublishAsync()
        {
            if (IsPlaying || !IsPublishing) throw new InvalidOperationException("Cannot stop playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "deleteStream",
                Data = new
                {
                    app = PlayStream.Name,
                    flashVer = _configuration.Value.FlashVersion,
                    objectEncoding = (double)AmfEncodingType.Amf0
                }
            };
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        protected override Task ProcessDataMessageAsync(RtmpMessage message)
        {
            throw new NotImplementedException();
        }

        protected override Task ProcessMediaMessageAsync(RtmpMessage message)
        {
            throw new NotImplementedException();
        }

        protected override async Task ProcessCommandMessageAsync(RtmpMessage message)
        {
            await _commandProcessor.ProcessMessageAsync(message);
        }
    }
}
