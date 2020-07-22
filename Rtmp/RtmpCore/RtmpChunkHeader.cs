using System;
using System.Buffers;

namespace RtmpCore
{
    public struct RtmpChunkHeader
    {
        public RtmpChunkHeader(RtmpChunkHeaderType messageType, int streamId)
        {
            HeaderType = messageType;
            StreamId = streamId;
        }

        public int StreamId { get; private set; }

        public RtmpChunkHeaderType HeaderType { get; private set; }

        public int Length => StreamId < 64 ? 1 : (StreamId < 320 ? 2 : 3);

        public bool TryParse(ref SequenceReader<byte> reader)
        {
            if (!reader.TryRead(out var value))
                return false;

            HeaderType = (RtmpChunkHeaderType)(value >> 6);
            StreamId = value & 0x3f;
            if (StreamId == 0)
            {
                if (!reader.TryRead(out value))
                    return false;
                StreamId = value + 64;
            }
            else if (StreamId == 1)
            {
                if (!reader.TryReadBigEndian(out short temp))
                    return false;
                StreamId = temp + 64;
            }

            return true;
        }

        public int Write(Span<byte> buffer)
        {
            buffer[0] = (byte)((byte)HeaderType << 6 | StreamId % 64);
            if (StreamId > 64)
            {
                buffer[1] = (byte)((StreamId - 64) % 256);
            }
            if (StreamId > 320)
            {
               buffer[2] = (byte)((StreamId - 64) >> 8);
            }

            return Length;
        }

        public override string ToString()
        {
            return $"Chunk Header Type:{HeaderType} Id:{StreamId}";
        }
    }
}
