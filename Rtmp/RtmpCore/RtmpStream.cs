using System;

namespace RtmpCore
{
    public class RtmpStream
    {
        public string Name { get; }

        public int Id { get; }

        public string Path { get; set; }

        public RtmpStream(int id, string name, string path)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

    }
}
