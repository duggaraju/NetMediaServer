using System;

namespace RtmpCore
{
    public class AudioCodecInfo
    {
        public int CodecId { get; set; }

        public string CodecName => RtmpConstants.AudioCodecNames[CodecId];

        public int Samplerate { get; set; }

        public int Channels { get; set; }

        public int Bitrate { get; set; }

        public Memory<byte> AacSequenceHeader { get; set; }

        public string ProfileName { get; internal set; }
    }
}
