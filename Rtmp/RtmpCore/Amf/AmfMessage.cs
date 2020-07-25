using System;
using System.Collections.Generic;
using System.Linq;

namespace RtmpCore.Amf
{
    abstract public class AmfMessage
    {
        public string Name { get; set; }

        public AmfEncodingType EncodingType { get; set; } = AmfEncodingType.Amf0;

        public IList<object> AdditionalArguments { get; } = new List<object>();

        public void Decode(ReadOnlySpan<byte> buffer)
        {
            var (length, commandName) = Amf0Decoder.Decode(buffer);
            Name = commandName as string;

            foreach (var index in Enumerable.Range(0, GetPropertyCount()))
            {
                buffer = buffer.Slice(length);
                if (buffer.Length == 0)
                    break;
                object value;
                (length, value) = Amf0Decoder.Decode(buffer);
                SetProperty(index, value);
            }
        }

        protected abstract int GetPropertyCount();

        protected virtual void SetProperty(int index, object value)
        {
            AdditionalArguments.Add(value);
        }

        protected virtual object GetProperty(int index)
        {
            return index < AdditionalArguments.Count ? AdditionalArguments[index] : null;
        }


        public void Encode(Span<byte> buffer)
        {
            var length = Amf0Decoder.Encode(buffer, Name);
            buffer = buffer.Slice(length);
            foreach (var index in Enumerable.Range(0, GetPropertyCount()))
            {
                object value = GetProperty(index);
                var propertyLength = Amf0Decoder.Encode(buffer, value);
                length += propertyLength;
                buffer = buffer.Slice(propertyLength);
            }
        }

        public int GetLength()
        {
            var length = Amf0Decoder.GetLength(Name);
            foreach (var index in Enumerable.Range(0, GetPropertyCount()))
                length += Amf0Decoder.GetLength(GetProperty(index));
            return length;
        }
    }
}
