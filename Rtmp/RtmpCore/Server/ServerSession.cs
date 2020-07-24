using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RtmpCore
{
    public class ServerSession : RtmpSession
    {
        private readonly RtmpContext _context;
        private readonly ServerCommandProcessor _commandProcessor;
        private readonly ServerDataProcessor _dataProcessor;
        private readonly ServerMediaProcessor _mediaProcessor;
        private bool _keyFrameSent = false;

        public Queue<RtmpMessage> RtmpGopCacheQueue { get; } = null;

        public Memory<byte> MetaData { get; internal set; }
        
        public AudioCodecInfo AudioCodec { get; internal set; }

        public VideoCodecInfo VideoCodec { get; internal set; }

        public ServerSession(RtmpContext context, TcpClient client) : base(client)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commandProcessor = new ServerCommandProcessor(context, this);
            _dataProcessor = new ServerDataProcessor(context, this);
            _mediaProcessor = new ServerMediaProcessor(context, this);
            context.Sessions.Add(Id, this);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.RunAsync(cancellationToken);
            }
            finally
            {
                _context.Sessions.Remove(Id);
                if (IsPlaying)
                    _context.RemovePlayer(IsPublishing ? PublishStream.Path : PlayStream.Path, Id);
                else if (IsPublishing)
                    _context.RemovePublisher(PublishStream.Path);
            }
        }

        /// <summary>
        /// Send a mesage to the other side of this session.
        /// </summary>
        /// <param name="message">The Rtmp message to send.</param>
        /// <returns></returns>
        public override async Task SendMessageAsync(RtmpMessage message)
        {
            if (IsPlaying)
            {
                if (!_keyFrameSent && message.Message.MessageType == RtmpMessageType.Video)
                {
                    var frame_type = (message.Payload.Span[0] >> 4) & 0x0f;
                    _keyFrameSent = frame_type == 1;
                }
                if (!_keyFrameSent &&
                    (message.Message.MessageType == RtmpMessageType.Audio || message.Message.MessageType == RtmpMessageType.Video))
                {
                    //return;
                }
            }
            await base.SendMessageAsync(message);
        }

        public async Task SendStartToAllPlayersAsync()
        {
            var players = _context.GetPlayers(PublishStream.Path);
            foreach (var player in players)
            {
                await player.SendStartAsync(this);
            }
        }

        private async Task SendStartAsync(ServerSession publishser)
        {
            await _commandProcessor.OnStartPlayAsync(publishser, PlayStream);
        }

        public async Task SendToAllPalyersAsync(RtmpMessage message)
        {
            var players = _context.GetPlayers(PublishStream.Path);
            foreach (var player in players)
            {
                var stream = player.PlayStream;
                var header = message.Header;
                var messageHeader = message.Message.Translate(stream.Id);
                var playerMessage = new RtmpMessage(header, messageHeader, message.Payload);
                try
                {
                    await player.SendMessageAsync(playerMessage);
                }
                catch (Exception ex)
                {
                    //remove failing player.
                    _logger.LogError(ex, $"Failed to send to player {player.Id}");
                    _context.RemovePlayer(PublishStream.Path, player.Id);
                }
            }
        }

        public async Task SendStopToAllPlayersAsync()
        {
            var players = _context.GetPlayers(PublishStream.Path);
            foreach (var player in players)
            {
                await player.SendStopAsync(this);
            }
        }

        public async Task SendStopAsync(ServerSession publisher)
        {
            await _commandProcessor.OnStopPlayAsync(publisher, PlayStream);
        }

        protected override async Task ProcessDataMessageAsync(RtmpMessage message)
        {
            await _dataProcessor.ProcessMessageAsync(message);
        }

        protected override async Task ProcessMediaMessageAsync(RtmpMessage message)
        {
            await _mediaProcessor.ProcessMessageAsync(message);
        }

        protected override async Task ProcessCommandMessageAsync(RtmpMessage message)
        {
            await _commandProcessor.ProcessMessageAsync(message);
        }
    }
}
