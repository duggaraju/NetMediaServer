
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;

namespace RtmpCore
{
    public static class SequenceReaderExtensions
    {
        public static bool TryReadInt24BigEndian(ref this SequenceReader<byte> reader, out int value)
        {
            value = 0;
            if (reader.TryReadBigEndian(out short value1) && reader.TryRead(out byte value2))
            {
                value = (value1 << 8) + value2;
                return true;
            }

            return false;
        }

        public static bool TryReadInt24LittleEndian(ref this SequenceReader<byte> reader, out int value)
        {
            if (reader.TryReadInt24BigEndian(out value))
            {
                value = IPAddress.HostToNetworkOrder(value);
                return true;
            }

            return false;
        }

        public static void WriteInt24BigEndian(ref this Span<byte> buffer, int value)
        {
            var sValue = (short)(value >> 8);
            BinaryPrimitives.WriteInt16BigEndian(buffer, sValue);
            var bValue = (byte)value;
            buffer[2] = bValue;
        }
    }
}
