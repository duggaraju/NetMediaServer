using System;
using System.Buffers;
using System.Buffers.Binary;

namespace RtmpCore
{
    public struct RtmpMessageHeader
    {
        public int Timestamp { get; private set; }

        public int Length { get; private set; }

        public RtmpMessageType MessageType { get; private set; }

        public int StreamId { get; private set; }

        public int? ExtendedTimestamp { get; private set; }

        public RtmpMessageHeader(int timestamp, int length, RtmpMessageType messageType, int streamId)
        {
            Timestamp = timestamp > 0xFFFFFF ? 0xFFFFFF : timestamp;
            Length = length;
            MessageType = messageType;
            StreamId = streamId;
            ExtendedTimestamp = timestamp > 0xFFFFFF ? timestamp : default(int?);
        }

        public RtmpMessageHeader(RtmpMessageHeader other)
        {
            Timestamp = other.Timestamp;
            Length = other.Length;
            MessageType = other.MessageType;
            StreamId = other.StreamId;
            ExtendedTimestamp = other.ExtendedTimestamp;
        }

        public override string ToString()
        {
            return $"Id: {MessageType} Time: {Timestamp} Length: {Length} StreamId: {StreamId}";
        }

        public bool TryParse(ref SequenceReader<byte> reader, RtmpChunkHeaderType headerType, RtmpMessageHeader? previous)
        {

            var extendedTimestamp = false;

            if (headerType < RtmpChunkHeaderType.Type3)
            {
                if (!reader.TryReadInt24BigEndian(out var time))
                    return false;
                if (time == 0xFFFFFF)
                    extendedTimestamp = true;
                else if (headerType == RtmpChunkHeaderType.Type0)
                    Timestamp = time;
                else
                    Timestamp = (previous?.Timestamp ?? 0) + time;
            }
            else
            {
                Timestamp = previous?.Timestamp ?? 0;
            }

            if (headerType < RtmpChunkHeaderType.Type2)
            {
                if (!reader.TryReadInt24BigEndian(out var length))
                    return false;
                Length = length;

                if (!reader.TryRead(out byte id))
                    return false;
                MessageType = (RtmpMessageType) id;
                if (!Enum.IsDefined(typeof(RtmpMessageType), id))
                    throw new Exception($"Unknown Message type {id}!");
            }
            else
            {
                Length = previous?.Length ?? 0;
                MessageType = previous?.MessageType ?? RtmpMessageType.None;
            }

            if (headerType == RtmpChunkHeaderType.Type0)
            {
                if (!reader.TryReadLittleEndian(out int streamId))
                    return false;
                StreamId = streamId;
            }
            else
            {
                StreamId = previous?.StreamId ?? 0;
            }

            if (extendedTimestamp)
            {
                if (!reader.TryReadBigEndian(out int time))
                    return false;
                Timestamp = time;
            }

            return true;
        }

        public void Write(Span<byte> buffer, RtmpChunkHeaderType headerType)
        {
            if (headerType < RtmpChunkHeaderType.Type3)
            {
                buffer.WriteInt24BigEndian(Timestamp > 0xFFFFF ? 0xFFFFFF : Timestamp);
                if (headerType < RtmpChunkHeaderType.Type2)
                {
                    buffer = buffer.Slice(3);
                    buffer.WriteInt24BigEndian(Length);
                    buffer[3] = (byte)MessageType;
                    buffer = buffer.Slice(4);
                }

                if (headerType < RtmpChunkHeaderType.Type1)
                {
                    buffer = buffer.Slice(3);
                    BinaryPrimitives.WriteInt32LittleEndian(buffer, StreamId);
                }
                if (Timestamp > 0xFFFFFF)
                    BinaryPrimitives.WriteInt32BigEndian(buffer, Timestamp);
            }
        }

        public static int GetHeaderLength(RtmpChunkHeaderType headerType)
        {
            switch (headerType)
            {
                case RtmpChunkHeaderType.Type0:
                    return 11;
                case RtmpChunkHeaderType.Type1:
                    return 7;
                case RtmpChunkHeaderType.Type2:
                    return 3;
                default:
                    return 0;
            }
        }

        public RtmpMessageHeader Translate(int streamId)
        {
            return new RtmpMessageHeader(
                Timestamp,
                Length,
                MessageType,
                streamId);
        }
    }
}
