using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RtmpCore
{
    public class RtmpSession
    {
        const byte RtmpVersion = 3;
        const int RtmpHandshakeSize = 1536;

        private readonly RtmpContext _context;
        private readonly TcpClient _client;
        private readonly Pipe _pipe;
        private readonly NetworkStream _networkStream;
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpSession>();
        private readonly RtmpMessageWriter _writer;
        private readonly RtmpCommandProcessor _commandProcessor;
        private bool _keyFrameSent = false;

        public Queue<RtmpMessage> RtmpGopCacheQueue { get; } = null;

        public Guid Id { get; }

        public int IncomingChunkLength { get; internal set; } = RtmpChunk.DefaultChunkBodyLength;

        public int OutgoingChunkLength { get; internal set; } = RtmpChunk.DefaultChunkBodyLength;
        
        public int WindowAcknowledgementSize { get; internal set; }

        public RtmpNetStream PublishStream { get; internal set; }

        public RtmpNetStream PlayStream { get; internal set; }

        public bool IsPublishing => PublishStream != null;

        public bool IsPlaying => PlayStream != null;

        public Memory<byte> MetaData { get; internal set; }
        
        public AudioCodecInfo AudioCodec { get; internal set; }

        public VideoCodecInfo VideoCodec { get; internal set; }

        public RtmpSession(RtmpContext context, TcpClient client)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _pipe = new Pipe();
            _networkStream = _client.GetStream();
            _writer = new RtmpMessageWriter(this, _networkStream);
            _commandProcessor = new RtmpCommandProcessor(context, this);
            Id = Guid.NewGuid();
            context.Sessions.Add(Id, this);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await InitiateHandShakeAsync(cancellationToken);
                var readTask = ReadDataAsync(_networkStream, _pipe.Writer, cancellationToken);
                var parseTask = ParseChunksAsync(_pipe.Reader, cancellationToken);
                await Task.WhenAll(readTask, parseTask);
            }
            finally
            {
                _logger.LogInformation($"RTMP session complete {Id}");
                _client.Close();
                _context.Sessions.Remove(Id);
                if (IsPlaying)
                    _context.RemovePlayer(IsPublishing ? PublishStream.Path : PlayStream.Path, Id);
                else if (IsPublishing)
                    _context.RemovePublisher(PublishStream.Path);
            }
        }

        private async Task ReadDataAsync(NetworkStream stream, PipeWriter writer, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 140;

            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter
                var memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    var bytesRead = await stream.ReadAsync(memory, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Network read failed");
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();
        }

        private async Task SendVersionAsync(int epoch = 0, CancellationToken cancellationToken = default)
        {
            _networkStream.WriteByte(RtmpVersion);

            var buffer = new byte[RtmpHandshakeSize];
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(), epoch); ;
            var random = new Random();
            random.NextBytes(buffer.AsSpan().Slice(8));

            await _networkStream.WriteAsync(buffer, cancellationToken);
        }

        private async Task<(byte[] Buffer, int Epoch)> ReadVersionAsync(CancellationToken cancellationToken)
        {
            var version = _networkStream.ReadByte();
            if (version != RtmpVersion)
            {
                _logger.LogWarning("Unkown version {version}");
            }

            var buffer = new byte[RtmpHandshakeSize];
            await _networkStream.ReadAsync(buffer, cancellationToken);
            BinaryPrimitives.TryReadInt32BigEndian(buffer.AsSpan(), out var epoch);
            return (buffer, epoch);
        }

        private async Task SendAckAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _networkStream.WriteAsync(buffer, cancellationToken);
        }

        private async Task<byte[]> ReadAckAsync(CancellationToken cancellationToken = default)
        {
            var buffer = new byte[RtmpHandshakeSize];
            await _networkStream.ReadAsync(buffer, cancellationToken);
            return buffer;
        }

        private async Task InitiateHandShakeAsync(CancellationToken cancellationToken)
        {
            await SendVersionAsync(0, cancellationToken);
            var (buffer, epoch) = await ReadVersionAsync(cancellationToken);
            await SendAckAsync(buffer, cancellationToken);
            await ReadAckAsync(cancellationToken);
            _logger.LogInformation($"Received version with epoch:{epoch}");
        }

        /// <summary>
        /// Send a mesage to the other side of this session.
        /// </summary>
        /// <param name="message">The Rtmp message to send.</param>
        /// <returns></returns>
        public async Task SendMessageAsync(RtmpMessage message)
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
            await _writer.ProcessMessageAsync(message);
        }

        public async Task SendStartToAllPlayersAsync()
        {
            var players = _context.GetPlayers(PublishStream.Path);
            foreach (var player in players)
            {
                await player.SendStartAsync(this);
            }
        }

        private async Task SendStartAsync(RtmpSession publishser)
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

        private async Task ParseChunksAsync(PipeReader reader, CancellationToken cancellationToken)
        {
            var messageWriter = new RtmpMessageWriter(this, _networkStream);
            var processor = new RtmpMessageProcessor(
                _commandProcessor,
                new RtmpControlMessageProcessor(_context, this),
                new RtmpDataProcessor(_context, this),
                new RtmpMediaProcessor(_context, this),
                new RtmpEventProcessor());
            var messageParser = new RtmpMessageParser(this, processor);
            await messageParser.ParseMessagesAsync(reader, cancellationToken);
        }

        public async Task SendStopToAllPlayersAsync()
        {
            var players = _context.GetPlayers(PublishStream.Path);
            foreach (var player in players)
            {
                await player.SendStopAsync(this);
            }
        }

        public async Task SendStopAsync(RtmpSession publisher)
        {
            await _commandProcessor.OnStopPlayAsync(publisher, PlayStream);
        }
    }
}
