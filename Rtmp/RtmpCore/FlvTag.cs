using System;
using System.Buffers.Binary;

namespace RtmpCore
{
    public class FlvTag
    {
        public RtmpMessageType HeaderType { get; }

        public int Timestamp { get; }

        public Memory<byte> Payload { get; }

        public int Length => 11 + Payload.Length;

        public FlvTag(int timestamp, RtmpMessageType headerType, Memory<byte> payload)
        {
            Timestamp = timestamp;
            HeaderType = headerType;
            Payload = payload;
        }

        public void Write(Span<byte> buffer)
        {
            buffer[0] = (byte)HeaderType;
            buffer = buffer.Slice(1);
            buffer.WriteInt24BigEndian(Payload.Length);
            buffer = buffer.Slice(3);
            buffer.WriteInt24BigEndian(Timestamp);
            buffer[3] = (byte)(Timestamp >> 24);
            buffer = buffer.Slice(4);
            buffer.WriteInt24BigEndian(0);
            buffer = buffer.Slice(3);
            BinaryPrimitives.WriteInt32BigEndian(buffer, Length);
            Payload.Span.CopyTo(buffer);
        }
    }
}
