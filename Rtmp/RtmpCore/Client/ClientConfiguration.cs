using System;

namespace RtmpCore
{
    public class ClientConfiguration
    {
        public Uri Server { get; set; }

        public int ChunkLength { get; set; }

        public string FlashVersion { get; set; }
    }
}
