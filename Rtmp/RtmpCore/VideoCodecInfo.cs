
using System;

namespace RtmpCore
{
    public class VideoCodecInfo
    {
        public int CodecId { get; set; }

        public string CodecName => RtmpConstants.VideoCodecNames[CodecId];

        public int Width { get; set; }

        public int Height { get; set; }

        public int Framerate { get; set; }

        public int Bitrate { get; set; }

        public Memory<byte> AvcSequenceHeader { get; set; }

        public string ProfileName { get;  set; }

        public int Level { get; set; }
    }
}
