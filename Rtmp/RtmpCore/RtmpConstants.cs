namespace RtmpCore
{
    public static class RtmpConstants
    {
        public const int RtmpChannel_Protocol = 2;
        public const int RtmpChannel_Invoke = 3;
        public const int RtmpChannel_Audio = 4;
        public const int RtmpChannel_Video = 5;
        public const int RtmpChannel_Data = 6;

        public const int DefaultChunkBodyLength = 128;
        public const int DefaultMaxChunkLength = 140;

        public const int DefaultWindowAckSize = 2500000;

        public const int AacAudio = 10;

        public const int H264Video = 7;
        public const int H265Video = 11;

        public static readonly int[] AacSampleRates = {
          96000, 88200, 64000, 48000,
          44100, 32000, 24000, 22050,
          16000, 12000, 11025, 8000,
          7350, 0, 0, 0
        };

        public static readonly int[] AacChannels = {
          0, 1, 2, 3, 4, 5, 6, 8
        };

        public static readonly string[] AudioCodecNames = {
          "",
          "ADPCM",
          "MP3",
          "LinearLE",
          "Nellymoser16",
          "Nellymoser8",
          "Nellymoser",
          "G711A",
          "G711U",
          "",
          "AAC",
          "Speex",
          "",
          "",
          "MP3-8K",
          "DeviceSpecific",
          "Uncompressed"
        };

        public static readonly int[] AudioSoundRates = {
          5512, 11025, 22050, 44100
        };

        public static readonly string[] VideoCodecNames = {
          "",
          "Jpeg",
          "Sorenson-H263",
          "ScreenVideo",
          "On2-VP6",
          "On2-VP6-Alpha",
          "ScreenVideo2",
          "H264",
          "",
          "",
          "",
          "",
          "H265"
        };


    }
}
