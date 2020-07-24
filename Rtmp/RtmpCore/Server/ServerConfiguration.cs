namespace RtmpCore
{
    public class ServerConfiguration
    {
        public int ChunkLength { get; set; }

        public int Port { get; set; } = 1935;

        public int PingTimeout { get; set; }
    }
}
