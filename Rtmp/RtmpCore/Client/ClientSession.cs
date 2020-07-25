using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RtmpCore.Amf;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore.Client
{
    public class ClientSession : RtmpSession
    {
        private readonly IOptions<ClientConfiguration> _configuration;
        private readonly ClientCommandProcessor _commandProcessor;
        private readonly RtmpDataProcessor _dataProcessor;
        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private Dictionary<int, Func<AmfCommandMessage, Task>> _pendingTransactions = new Dictionary<int, Func<AmfCommandMessage, Task>>();
        private ClientSessionState _state = ClientSessionState.Disconntectd;

        public ClientSessionState State => _state;

        public event EventHandler<EventArgs> StateChanged;

        public event EventHandler<EventArgs<RtmpMessage>> MediaReceived;

        public ClientSession(TcpClient client, IOptions<ClientConfiguration> configuration) : base(client)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _commandProcessor = new ClientCommandProcessor(this);
            _dataProcessor = new RtmpDataProcessor(this);
            _commandProcessor.ResponseReceived += OnResponseReceived;
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
                TransactionId = NextTrasactionId,
                CommandObject = new
                {
                    app = appName,
                    flashVer = _configuration.Value.FlashVersion,
                },
            };

            _pendingTransactions.Add(command.TransactionId, (args) =>
            {
                _logger.LogInformation($"Connect to server version:{args.CommandObject.fmsVer} capabilities:{args.CommandObject.capabilities}");
                UpdateState(ClientSessionState.Conntected);
                return Task.CompletedTask;
            });
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        public async Task StartPlayback(string streamName)
        {
            if (IsPublishing || IsPlaying) throw new InvalidOperationException("Cannot start playback in current state.");

            var transactionId = NextTrasactionId;
            var command = new AmfCommandMessage
            {
                Name = "createStream",
                TransactionId = transactionId,
            };

            _pendingTransactions.Add(transactionId, async(args) => await PlayAsync(args, streamName));
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        private async Task PlayAsync(AmfCommandMessage command, string streamName)
        {
            _logger.LogInformation($"Created stream {command.AdditionalArguments[0]}");
            AmfCommandMessage playCommand = new AmfCommandMessage
            {
                Name = "play",
                TransactionId = 0,
                AdditionalArguments = { streamName }
            };
            _pendingTransactions.Add(0, args =>
            {
                UpdateState(ClientSessionState.Playing);
                return Task.CompletedTask;
            });
            await _commandProcessor.SendCommandMessageAsync(0, playCommand);
        }

        public async Task StopPlayback()
        {
            if (IsPublishing || !IsPlaying) throw new InvalidOperationException("Cannot stop playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "deleteStream",
                TransactionId = NextTrasactionId,
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
                TransactionId = NextTrasactionId,
                CommandObject = new
                {
                    app = streamName,
                    flashVer = _configuration.Value.FlashVersion,
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
            };
            await _dataProcessor.SendDataCommandAsync(command);
        }

        public async Task StopPublishAsync()
        {
            if (IsPlaying || !IsPublishing) throw new InvalidOperationException("Cannot stop playback in current state.");
            var command = new AmfCommandMessage
            {
                Name = "deleteStream",
                TransactionId = NextTrasactionId,
                CommandObject = new { }
            };
            await _commandProcessor.SendCommandMessageAsync(0, command);
        }

        protected override Task ProcessDataMessageAsync(RtmpMessage message)
        {
            AmfDataMessage command = new AmfDataMessage();
            command.Decode(message.Payload.Span);
            _logger.LogInformation($"Data command received {command.Name} {command.AdditionalArguments[0]}");
            return Task.CompletedTask;
        }

        protected override Task ProcessMediaMessageAsync(RtmpMessage message)
        {
            MediaReceived?.Invoke(this, new EventArgs<RtmpMessage>(message));
            return Task.CompletedTask;
        }

        protected override async Task ProcessCommandMessageAsync(RtmpMessage message)
        {
            await _commandProcessor.ProcessMessageAsync(message);
        }

        private void UpdateState(ClientSessionState state)
        {
            _state = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void OnResponseReceived(object sender, CommandArgs args)
        {
            if (args.Command.Name == "_result")
                await HandleSuccessResponse(args.Command);
            else if (args.Command.Name == "onStatus")
                HandleStatus(args.Command);
        }

        private void HandleStatus(AmfCommandMessage command)
        {
            var status = command.AdditionalArguments[0] as dynamic;
            if (status.code == "status")
            {
                UpdateState(ClientSessionState.Playing);
            }
        }

        private async Task HandleSuccessResponse(AmfCommandMessage command)
        {
            if (_pendingTransactions.Remove(command.TransactionId, out var pendingAction))
            {
                await pendingAction(command);
            }
            else
            {
                throw new InvalidOperationException("no pending transaction");
            }

        }
    }
}
