using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpCommandProcessor : IRtmpMessageProcessor
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpCommandProcessor>();
        private string _application;
        private readonly RtmpContext _context;
        private readonly RtmpSession _session;
        private int _streams = 0;

        public RtmpCommandProcessor(RtmpContext context, RtmpSession session)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            var command = new AmfCommandMessage();
            _logger.LogInformation($"Procesing command:{command.Name} for session {_session.Id}s");
            command.Decode(message.Payload.Span);
            switch (command.Name)
            {
                case "connect":
                    await HandleConnectAsync(command);
                    break;
                case "createStream":
                    await HandleCreateStreamAsync(command);
                    break;
                case "closeStream":
                    await HandleCloseStreamAsync(command);
                    break;
                case "deleteStream":
                    await HandleDeleteStreamAsync(command);
                    break;
                case "releaseStream":
                    // nothting to do.
                    break;
                case "FCPublish":
                case "FCUnpublish":
                    break;
                case "publish":
                    await HandlePublishStreamAsync(message, command);
                    break;
                case "play":
                    await HandlePlayAsync(message, command);
                    break;
                case "pause":
                    await HandlePauseAsync(message, command);
                    break;
                case "getStreamLength":
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command {command.Name} {command.Data} ");
            }
        }

        private Task HandleCloseStreamAsync(AmfCommandMessage command)
        {
            throw new NotImplementedException();
        }

        private Task HandlePauseAsync(RtmpMessage message, AmfCommandMessage command)
        {
            throw new NotImplementedException();
        }

        private async Task HandlePlayAsync(RtmpMessage message, AmfCommandMessage command)
        {
            var streamName = command.Data.streamName;
            var streamPath = $"/{_application}/{streamName.Split("?")[0]}";
            var streamId = message.Message.StreamId;
            var stream = new RtmpNetStream(streamId, streamName, streamPath);

            if (_session.IsPlaying)
            {
                _logger.LogInformation($@"[rtmp play] NetConnection is playing.id ={_session.Id}
                streamPath ={streamPath}
                streamId ={streamId} ");
                await SendStatusMessageAsync(streamId, "error", "NetStream.Play.BadConnection", "Connection already playing");
                return;
            }
            else
            {
                _session.PlayStream = stream;
                await RespondPlayAsync(stream);
            }

            if (_context.TryGetPublishser(streamPath, out var publisher))
            {
                await OnStartPlayAsync(publisher, stream);
                _context.AddPlayer(streamPath, _session.Id);
            }
            else
            {
                _logger.LogInformation($@"[rtmp play] Stream not found.id ={_session.Id}
                streamPath ={streamPath}
                streamId ={streamId}");
                _context.AddIdlePlayer(streamPath, _session.Id);
            }
        }

        private async Task RespondPlayAsync(RtmpNetStream stream)
        {
            await SendUserControlMessageAsync(UserControlMessageType.StreamBegin, stream.Id);
            await SendStatusMessageAsync(stream.Id, "status", "NetStream.Play.Reset", "Playing and resetting stream.");
            await SendStatusMessageAsync(stream.Id, "status", "NetStream.Play.Start", "Started playing stream.");
            await SendRtmpSampleAccess();
        }

        private async Task SendRtmpSampleAccess()
        {
            var command = new AmfDataMessage
            {
                Name = "|RtmpSampleAccess",
                Data = new
                {
                    bool1 = false,
                    bool2 = false
                }
            };
            await SendDataMessageAsync(0, command);
        }

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

        public async Task  OnStartPlayAsync(RtmpSession publisher, RtmpNetStream stream)
        {
            if (publisher.MetaData.Length != 0)
            {
                var payload = publisher.MetaData;
                var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Data);
                var messageHeader = new RtmpMessageHeader(0, payload.Length, RtmpMessageType.DataAMF0, stream.Id);
                var message = new RtmpMessage(header, messageHeader, payload);
                await _session.SendMessageAsync(message);
            }

            if (publisher.AudioCodec?.CodecId == RtmpConstants.AacAudio)
            {
                var payload = publisher.AudioCodec.AacSequenceHeader;
                var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Audio);
                var messageHeader = new RtmpMessageHeader(0, payload.Length, RtmpMessageType.Audio, stream.Id);
                var message = new RtmpMessage(header, messageHeader, payload);
                await _session.SendMessageAsync(message);
            }

            if (publisher.VideoCodec?.CodecId == RtmpConstants.H264Video || publisher.VideoCodec?.CodecId == RtmpConstants.H265Video)
            {
                var payload = publisher.VideoCodec.AvcSequenceHeader;
                var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Video);
                var messageHeader = new RtmpMessageHeader(0, payload.Length, RtmpMessageType.Video, stream.Id);
                var message = new RtmpMessage(header, messageHeader, payload);
                await _session.SendMessageAsync(message);
            }

            if (publisher.RtmpGopCacheQueue != null)
            {
                foreach (var message in publisher.RtmpGopCacheQueue)
                {
                    var modifiedMessage = new RtmpMessage(
                        message.Header, 
                        message.Message.Translate(stream.Id),
                        message.Payload);
                    await _session.SendMessageAsync(modifiedMessage);
                }
            }

            _logger.LogInformation($@"[rtmp play] Join stream. id ={_session.Id} streamPath ={stream.Path} streamId ={stream.Id} ");
        }

        private async Task HandleDeleteStreamAsync(AmfCommandMessage command)
        {
            if (_session.IsPlaying)
            {
                _context.RemovePlayer(_session.PlayStream.Path, _session.Id);
                _logger.LogInformation($@"[rtmp play] Close stream. id ={_session.Id}
                streamPath ={_session.PlayStream.Path}
                streamId =${ _session.PlayStream.Id}");
                await SendStatusMessageAsync(_session.PlayStream.Id, "status", "NetStream.Play.Stop", "Stopped playing stream.");

            }
            if (_session.IsPublishing && command.Data.streamId == _session.PublishStream.Id)
            {
                var stream = _session.PublishStream;
                _logger.LogInformation($@"[rtmp publish] Close stream. id ={_session.Id}
                streamPath ={stream.Path} streamId ={stream.Id}");
                await SendStatusMessageAsync(stream.Id, "status", "NetStream.Unpublish.Success", $"{stream.Path} is now unpublished.");

                await _session.SendStopToAllPlayersAsync();
                _session.PublishStream = null;
                _context.RemovePublisher(stream.Path);
            }
        }

        public async Task OnStopPlayAsync(object publishser, RtmpNetStream stream)
        {
            var description = $"{stream.Path} is now unpublished.";
            var code = "NetStream.Play.UnpublishNotify";
            await SendStatusMessageAsync(stream.Id, "status", code, description);
        }

        private async Task HandlePublishStreamAsync(RtmpMessage message, AmfCommandMessage command)
        {
            var streamName = command.Data.streamName;
            var streamPath = $"/{_application}/{streamName}";
            var streamId = (int)message.Message.StreamId;
            var stream = new RtmpNetStream(streamId, streamName, streamPath);
            string code, level, description;

            if (!_context.TryAddPublishser(streamPath, _session.Id))
            {
                _logger.LogError($@"[rtmp publish] Already has a stream. id ={_session.Id} streamPath ={streamPath} streamId ={streamId}");
                level = "error";
                code = "NetStream.Publish.BadName";
                description = "Stream already publishing";
            }
            else if (_session.PublishStream != null)
            {
                level = "error";
                description = $"{streamPath} is already published.";
                code = "NetStream.Publish.BadConnection";
            }
            else
            {
                _session.PublishStream = stream;
                level = "status";
                description = $"{streamPath} is now published.";
                code = "NetStream.Publish.Start";
            }
            await SendStatusMessageAsync(stream.Id, level, code, description);
            await _session.SendStartToAllPlayersAsync();
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

        private Task SetPeerBandwidthAsync(int bandwidth)
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

        private async Task HandleCreateStreamAsync(AmfCommandMessage command)
        {
            ++_streams;
            var resultCommand = new AmfCommandMessage
            {
                Name = "_result",
                Data = new
                {
                    transId = command.Data.transId,
                    cmdObj = (object) null,
                    info = _streams
                }
            };
            await SendCommandMessageAsync(0, resultCommand);
        }

        private async Task HandleConnectAsync(AmfCommandMessage command)
        {
            _application = command.Data.cmdObj.app;
            await SendWindowAckSizeAsync(RtmpConstants.DefaultWindowAckSize);
            await SetPeerBandwidthAsync(RtmpConstants.DefaultWindowAckSize);
            await SetChunkSizeAsync(RtmpChunk.DefaultChunkBodyLength);

            var resultCommand = new AmfCommandMessage
            {
                Name = "_result",
                Data = new
                {
                    transId = command.Data.transId,
                    cmdObj = new
                    {
                        fmsVer = "FMS/3,0,1,123",
                        capabilities = 31
                    },
                    info = new
                    {
                        level = "status",
                        code = "NetConnection.Connect.Success",
                        description = "Connection succeeded.",
                        objectEncoding = (double)command.EncodingType
                    }
                }
            };
            await SendCommandMessageAsync(0, command: resultCommand);
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
