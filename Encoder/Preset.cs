using FFMpegCore;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Packager
{
    // All bit rates in Kbps
    public record struct VideoLayer(int Width, int Height, int Bitrate);

    public record struct AudioLayer(int Channels, int Bitrate);

    public record Preset(IList<VideoLayer> Videos, IList<AudioLayer> Audios, string VideoCodec, string AudioCodec)
    {
        public Preset(IList<VideoLayer> videos, IList<AudioLayer> audios) : this(videos, audios, "h264", "aac")
        { }
    }

    public static class Presets
    {
        public static VideoLayer[] Video720p => new[]
        {
            new VideoLayer(640, 360, 512),
            new VideoLayer(850, 480, 800),
            new VideoLayer(1200, 720, 1200),
        };

        public static VideoLayer[] Video1080p => Video720p.Concat(new[]
        {
            new VideoLayer(1920, 1080, 2000)
        }).ToArray();

        public static AudioLayer[] AudioAac => new[] { new AudioLayer(2, 64) };

        public static Preset H264720p => new Preset(Video720p, AudioAac);

        public static Preset H2641080p => new Preset(Video1080p, AudioAac);

        public static async Task<Preset> GetAdaptivePresetAsync(Uri uri, ILogger logger, CancellationToken cancellationToken)
        {
            var analysis = await (uri.Scheme == Uri.UriSchemeFile ?
                FFProbe.AnalyseAsync(uri.LocalPath, cancellationToken: cancellationToken) :
                FFProbe.AnalyseAsync(uri, cancellationToken: cancellationToken));
            // TODO:  based or analysis add or remove layers.
            if (!analysis.VideoStreams.Any())
            {
                logger.LogWarning("The input doesn't contain any video");
            }

            if (analysis.VideoStreams.Count > 1)
            {
                logger.LogWarning("More than one video present. Only using the first video stream.");
            }
            var video = analysis.VideoStreams[0];
            var videos = video.Height >= 1080 ? Video1080p : Video720p;
            if (analysis.AudioStreams.Count == 0)
            {
                logger.LogWarning("No audio stream found. Encoding and packaging only video.");
            }
            var audios = analysis.AudioStreams.Count > 0 ? AudioAac : Array.Empty<AudioLayer>();

            return new Preset(videos, audios);
        }
    }
}
