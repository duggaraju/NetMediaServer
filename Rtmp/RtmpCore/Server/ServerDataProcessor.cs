﻿using Microsoft.Extensions.Logging;
using RtmpCore.Amf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RtmpCore
{
    class ServerDataProcessor : IRtmpMessageProcessor
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<ServerDataProcessor>();
        private readonly ServerSession _session;
        private readonly RtmpContext _context;

        public ServerDataProcessor(RtmpContext context, ServerSession session)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            var command = new AmfDataMessage();
            command.Decode(message.Payload.Span);
            switch (command.Name)
            {
                case "@setDataFrame":
                    await HandleSetDataFrame(command);
                    break;
                case "onMetaData":
                    await HandleMetadtataAsync(message, command);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command {command.Name} {command.AdditionalArguments} ");
            }
        }

        private Task HandleMetadtataAsync(RtmpMessage message, AmfDataMessage command)
        {
            return Task.CompletedTask;
        }

        private async Task HandleSetDataFrame(AmfDataMessage command)
        {
            _logger.LogInformation("Metadata found {0}", command.AdditionalArguments);
            var dataObj = (dynamic) command.AdditionalArguments[1];
            var metadata = new AmfDataMessage
            {
                Name = "onMetaData",
                AdditionalArguments = 
                {
                    dataObj
                }
            };
            _session.MetaData = new byte[metadata.GetLength()];
            metadata.Encode(_session.MetaData.Span);
            if (((IDictionary<string, object>)dataObj).ContainsKey("audiocodecid"))
            {
                _session.AudioCodec = new AudioCodecInfo
                {
                    CodecId = dataObj.audiocodecid is double ? (int) dataObj.audiocodecid : 0,
                    Channels = dataObj.stereo ? 2 : 1,
                    Samplerate = (int)dataObj.audiosamplerate,
                    Bitrate = (int)dataObj.audiodatarate
                };
            }
            if (((IDictionary<string, object>)dataObj).ContainsKey("videocodecid"))
            {
                _session.VideoCodec = new VideoCodecInfo
                {
                    CodecId = dataObj.videocodecid is double ? (int)dataObj.videocodecid : 0,
                    Width = (int) dataObj.width,
                    Height = (int) dataObj.height,
                    Framerate = (int) dataObj.framerate,
                    Bitrate = (int) dataObj.videodatarate
                };
            }

            var header = new RtmpChunkHeader(RtmpChunkHeaderType.Type0, RtmpConstants.RtmpChannel_Data);
            var messsageHeader = new RtmpMessageHeader(0, _session.MetaData.Length, RtmpMessageType.DataAMF0, 0);
            var message = new RtmpMessage(header, messsageHeader, _session.MetaData);
            await _session.SendToAllPalyersAsync(message);
        }
    }
}
