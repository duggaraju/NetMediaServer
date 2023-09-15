
using FFMpegCore;
using FFMpegCore.Enums;

namespace Packager
{
    public static class FFMpegExtensions
    {
        public static void AddVideo(this FFMpegArgumentOptions options, VideoLayer layer, string codec = "libx264")
        {
            options
                .DisableChannel(Channel.Audio)
                .Resize(layer.Width, layer.Height)
                .WithVideoBitrate(layer.Bitrate)
                .WithCustomArgument("-r 30 -g 60")
                .WithVideoCodec(codec)
                .ForceFormat("mp4")
                .WithCustomArgument("-movflags cmaf+delay_moov+skip_trailer+skip_sidx+frag_keyframe");
        }

        public static FFMpegArgumentOptions AddAudio(this FFMpegArgumentOptions options, AudioLayer layer, string codec = "aac")
        {
            return options
                        .DisableChannel(Channel.Video)
                        .WithAudioCodec(codec)
                        .WithAudioBitrate(layer.Bitrate)
                        .WithCustomArgument($"-ac {layer.Channels} ")
                        .ForceFormat("mp4")
                        .WithCustomArgument("-movflags cmaf+delay_moov+skip_trailer+skip_sidx -frag_duration 2");
        }
    }
}
