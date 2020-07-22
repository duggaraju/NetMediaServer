namespace RtmpCore
{
    public class RtmpConfiguration
    {
        public int ChunkLength { get; set; }

        public int Port { get; set; } = 1935;

        public int PingTimeout { get; set; }
    }
}
