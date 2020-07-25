using System;

namespace RtmpCore.Client
{
    public class ClientConfiguration
    {
        public Uri Server { get; set; }

        public int ChunkLength { get; set; }

        public string FlashVersion { get; set; }
    }
}
