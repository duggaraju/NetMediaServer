using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class ServerMediaProcessor : IRtmpMessageProcessor
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<ServerMediaProcessor>();
        private readonly RtmpContext _context;
        private readonly ServerSession _session;

        public ServerMediaProcessor(RtmpContext context, ServerSession session)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task ProcessMessageAsync(RtmpMessage message)
        {
            if (message.Message.MessageType == RtmpMessageType.Audio)
                ProcessAudio(message);
            else if (message.Message.MessageType == RtmpMessageType.Video)
                ProcessVideo(message);

            if (_session.IsPublishing)
            {
                await _session.SendToAllPalyersAsync(message);
            }
        }

        private void ProcessAudio(RtmpMessage message)
        {
            var payload = message.Payload.Span;
            var sound_format = (payload[0] >> 4) & 0x0f;
            var sound_type = payload[0] & 0x01;
            var sound_size = (payload[0] >> 1) & 0x01;
            var sound_rate = (payload[0] >> 2) & 0x03;
            var audio = _session.AudioCodec;
            if (audio == null)
            {
                audio = new AudioCodecInfo
                {
                    CodecId = sound_format,
                    Samplerate = RtmpConstants.AudioSoundRates[sound_rate],
                    Channels = ++sound_type
                };
                _session.AudioCodec = audio;
                if (sound_format == 4)
                {
                    audio.Samplerate = 16000;
                }
                else if (sound_format == 5)
                {
                    audio.Samplerate = 8000;
                }
                else if (sound_format == 11)
                {
                    audio.Samplerate = 16000;
                }
                else if (sound_format == 14)
                {
                    audio.Samplerate = 8000;
                }
                if (sound_format != 10)
                {
                    _logger.LogInformation($@"[rtmp publish] Handle audio. id ={_session.Id}
                streamPath ={_session.PublishStream.Path}
                sound_format ={sound_format}
                sound_type ={sound_type}
                sound_size ={sound_size}
                sound_rate ={sound_rate}
                codec_name ={audio.CodecName} {audio.Samplerate} 
                {audio.Channels}ch");
                }
            }

            if (audio.CodecId == RtmpConstants.AacAudio && payload[1] == 0)
            {
                dynamic info = AVUtil.ReadAACSpecificConfig(payload);
                audio.AacSequenceHeader = new byte[payload.Length];
                payload.CopyTo(audio.AacSequenceHeader.Span);
                audio.ProfileName = AVUtil.GetAacProfileName(info);
                audio.Samplerate = info.sample_rate;
                audio.Channels = info.channels;
                _logger.LogInformation(
        $@"[rtmp publish] Handle audio. id ={_session.Id}
                streamPath ={_session.PublishStream.Path}
                sound_format ={sound_format}
                sound_type ={sound_type}
                sound_size ={sound_size}
                sound_rate ={sound_rate}
                codec_name ={audio.CodecName} {audio.Samplerate} {audio.Channels}ch");
            }
        }

        private void ProcessVideo(RtmpMessage message)
        {
            var  payload = message.Payload.Span;
            var  frame_type = (payload[0] >> 4) & 0x0f;
            var  codec_id = payload[0] & 0x0f;
            var video = _session.VideoCodec;

            if (video == null)
            {
                video = new VideoCodecInfo
                {
                    CodecId = codec_id,
                };
                _logger.LogInformation(
        $@"[rtmp publish] Handle video. id ={_session.Id}
                streamPath ={_session.PublishStream.Path}
                frame_type ={frame_type}
                codec_id ={codec_id}
                codec_name ={video.CodecName} {video.Width} x {video.Height}");
            }


            if (codec_id == RtmpConstants.H264Video || codec_id == RtmpConstants.H265Video)
            {
                //cache avc sequence header
                if (frame_type == 1 && payload[1] == 0)
                {
                    video.AvcSequenceHeader = new byte[payload.Length];
                    payload.CopyTo(video.AvcSequenceHeader.Span);
                    dynamic info = AVUtil.ReadAVCSpecificConfig(payload);
                    video.Width = info.width;
                    video.Height = info.height;
                    video.ProfileName = AVUtil.GetAVCProfileName(info);
                    video.Level = info.level;
                }
            }
        }
    }
}
