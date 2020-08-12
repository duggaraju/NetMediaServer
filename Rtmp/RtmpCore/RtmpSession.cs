using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public abstract class RtmpSession
    {
        const byte RtmpVersion = 3;
        const int RtmpHandshakeSize = 1536;

        private readonly TcpClient _client;
        private readonly Pipe _pipe;
        private readonly NetworkStream _networkStream;
        private readonly RtmpMessageParser _messageParser;
        private readonly RtmpMessageWriter _writer;
        private readonly RtmpEventProcessor _eventProcessor = new RtmpEventProcessor();
        private int _transactionId = 0;

        protected readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpSession>();
        protected readonly RtmpControlMessageProcessor _controlMessageProcessor;

        public Guid Id { get; }

        public int NextTrasactionId => ++_transactionId;

        public int IncomingChunkLength { get; internal set; } = RtmpChunk.DefaultChunkBodyLength;

        public int OutgoingChunkLength { get; internal set; } = RtmpChunk.DefaultChunkBodyLength;

        public int WindowAcknowledgementSize { get; internal set; }

        public RtmpStream PublishStream { get; internal set; }

        public RtmpStream PlayStream { get; internal set; }

        public bool IsPublishing => PublishStream != null;

        public bool IsPlaying => PlayStream != null;

        public RtmpSession(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            Id = Guid.NewGuid();
            //var options = new PipeOptions(pauseWriterThreshold: 0x1FFFF);
            _pipe = new Pipe();
            _networkStream = _client.GetStream();
            _writer = new RtmpMessageWriter(this, _networkStream);
            _controlMessageProcessor = new RtmpControlMessageProcessor(this);
            _messageParser = new RtmpMessageParser(this);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await InitiateHandShakeAsync(cancellationToken);
                var readTask = ReadDataAsync(_networkStream, _pipe.Writer, cancellationToken);
                var parseTask = ParseChunksAsync(_pipe.Reader, cancellationToken);
                await Task.WhenAny(readTask, parseTask);
                _logger.LogInformation($"session {Id} finished");
            }
            finally
            {
                _logger.LogInformation($"RTMP session complete {Id}");
                _client.Close();
            }
        }

        private async Task ReadDataAsync(NetworkStream stream, PipeWriter writer, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 140;

            while (true)
            {
                // Allocate minimum bytes from the PipeWriter
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
                _logger.LogWarning($"Unknown RTMP version {version}");
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
        public virtual async Task SendMessageAsync(RtmpMessage message)
        {
            await _writer.ProcessMessageAsync(message);
        }

        private async Task ParseChunksAsync(PipeReader reader, CancellationToken cancellationToken)
        {
            await _messageParser.ParseMessagesAsync(reader, cancellationToken);
        }

        public Task DispatchMessageAsync(RtmpMessage message)
        {
            switch (message.Message.MessageType)
            {
                case RtmpMessageType.SetChunkSize:
                case RtmpMessageType.Acknowledgement:
                case RtmpMessageType.WindowAcknowledgementSize:
                case RtmpMessageType.SetPeerBandwidth:
                    return ProcessProtocolMessageAsync(message);
                case RtmpMessageType.CommandAMF0:
                case RtmpMessageType.CommandAMF3:
                    return ProcessCommandMessageAsync(message);
                case RtmpMessageType.DataAMF0:
                case RtmpMessageType.DataAMF3:
                    return ProcessDataMessageAsync(message);
                case RtmpMessageType.Audio:
                case RtmpMessageType.Video:
                    return ProcessMediaMessageAsync(message);
                case RtmpMessageType.UserCtrlMessage:
                    return ProcessUserControlMessageAsync(message);
                default:
                    throw new Exception($"Unknown message of type: {message.Message.MessageType}");
            }
        }

        protected virtual async Task ProcessProtocolMessageAsync(RtmpMessage message)
        {
            await _controlMessageProcessor.ProcessMessageAsync(message);
        }

        protected virtual async Task ProcessUserControlMessageAsync(RtmpMessage message)
        {
            await _eventProcessor.ProcessMessageAsync(message);
        }

        protected abstract Task ProcessCommandMessageAsync(RtmpMessage message);

        protected abstract Task ProcessDataMessageAsync(RtmpMessage message);

        protected abstract Task ProcessMediaMessageAsync(RtmpMessage message);
    }
}
