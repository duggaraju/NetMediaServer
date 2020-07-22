namespace RtmpCore
{
    public class TransMuxerConfiguration
    {
        public string Ffmpeg { get; set; } = "ffmpeg.exe";

        public string GloblArguments { get; set; } = string.Empty;

        public string MediaDirectory { get; set; } = "media";

        public string VideoCodec { get; set; } = "copy";

        public string AudioCodec { get; set; } = "copy";

        public string DashParameters { get; set; } = "";
    }
}