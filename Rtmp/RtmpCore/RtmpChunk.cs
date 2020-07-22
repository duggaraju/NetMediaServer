
using System;
using System.Buffers;

namespace RtmpCore
{
    public class RtmpChunk
    {
        public const int DefaultChunkBodyLength = 128;
        public const int MaxHeaderLength = 12;

        public RtmpChunkHeader Header { get; }

        public RtmpMessageHeader Message { get; }

        public ReadOnlySequence<byte> Payload { get; private set; }

        public int PayloadLength => (int)Payload.Length;

        public int Length => Header.Length + RtmpMessageHeader.GetHeaderLength(Header.HeaderType) + PayloadLength;

        public RtmpChunk(RtmpChunkHeader header, RtmpMessageHeader message)
        {
            Header = header;
            Message = message;
        }

        public RtmpChunk(RtmpChunkHeader header, RtmpMessageHeader message, Memory<byte> payload) :
            this(header, message)
        {
            Payload = new ReadOnlySequence<byte>(payload);
        }

        public RtmpChunk(RtmpChunkHeader header, RtmpMessageHeader message, ReadOnlySequence<byte> payload) :
            this(header, message)
        {
            Payload = payload;
        }

        public bool TryParseBody(ref SequenceReader<byte> reader, int length)
        {
            if (reader.Remaining < length)
            {
                return false;
            }
            Payload = reader.Sequence.Slice(reader.Position, length);
            reader.Advance(length);
            return true;
        }

        public static bool TryParseHeader(ref SequenceReader<byte> reader, out RtmpChunk chunk)
        {
            chunk = null;
            var header = new RtmpChunkHeader();
            if (!header.TryParse(ref reader))
                return false;

            var message = new RtmpMessageHeader();
            if (!message.TryParse(ref reader, header.HeaderType, previous: null))
                return false;

            chunk = new RtmpChunk(header, message);
            return true;
        }

        public void Write(Span<byte> buffer)
        {
            Header.Write(buffer);
            buffer = buffer.Slice(Header.Length);
            Message.Write(buffer, Header.HeaderType);
            buffer = buffer.Slice(RtmpMessageHeader.GetHeaderLength(Header.HeaderType));
            var reader = new SequenceReader<byte>(Payload);
            reader.TryCopyTo(buffer);
        }
    }
}
