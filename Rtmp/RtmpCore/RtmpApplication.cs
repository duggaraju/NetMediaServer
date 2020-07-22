using System;
using System.Collections.Generic;

namespace RtmpCore
{
    public class RtmpApplication
    {
        private readonly Dictionary<string, RtmpNetStream> _streams = new Dictionary<string, RtmpNetStream>();
        
        public IReadOnlyDictionary<string, RtmpNetStream> Streams => _streams;

        public string Name { get; }

        public int StreamCount => _streams.Count;

        public RtmpApplication(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public bool TryAddStream(RtmpNetStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (_streams.ContainsKey(stream.Name))
                return false;
            _streams[stream.Name] = stream;
            return true;
        }

        public void RemoveStream(RtmpNetStream stream)
        {
            _streams.Remove(stream.Name);
        }
    }
}
