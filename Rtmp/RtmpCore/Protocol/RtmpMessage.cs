using System;
using System.Buffers;

namespace RtmpCore
{
    public sealed class RtmpMessage
    {
        private readonly IMemoryOwner<byte> _memoryOwner;

        public RtmpChunkHeader Header { get; }

        public RtmpMessageHeader Message { get; }

        public Memory<byte> Payload { get; }

        public RtmpMessage(RtmpChunkHeader header, RtmpMessageHeader message, IMemoryOwner<byte> memoryOwner)
            : this(header, message, memoryOwner.Memory)
        {
            _memoryOwner = memoryOwner;
        }

        public RtmpMessage(RtmpChunkHeader header, RtmpMessageHeader message, Memory<byte> payload)
        {
            Header = header;
            Message = message;
            Payload = payload.Slice(0, Message.Length);
        }

        public void Dispose()
        {
            if (_memoryOwner != null)
            {
                _memoryOwner.Dispose();
            }
        }
    }
}
