using System;

namespace RtmpCore
{
    public interface IAmfDecoder
    {
        public AmfCommandMessage Decode(ReadOnlySpan<byte> buffer);

        public void Encode(AmfCommandMessage command, Span<byte> buffer);
    }
}
