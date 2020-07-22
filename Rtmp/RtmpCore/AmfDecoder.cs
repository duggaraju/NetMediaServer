using System;
using Amf0Data = System.ValueTuple<int, object>;

namespace RtmpCore
{
    public abstract class AmfDecoder
    {
        /// <summary>
        /// Gets the number of bytes needed to encode this object.
        /// </summary>
        public abstract int GetLength(object value);

        public abstract void Encode();

        public abstract Amf0Data Decode(ReadOnlySpan<byte> buffer);
    }
}
