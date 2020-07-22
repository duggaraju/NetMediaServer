using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using AmfObject = System.ValueTuple<int, object>;

namespace RtmpCore
{
    /// <summary>
    /// An AMF0 Message decoder based on https://www.adobe.com/content/dam/acom/en/devnet/pdf/amf0-file-format-specification.pdf
    /// </summary>
    public static class Amf0Decoder
    {
        public delegate AmfObject DecoderFunction(ReadOnlySpan<byte> buffer);
        public delegate void EncodeFunction(Span<byte> buffer, object value);

        private static readonly Dictionary<Amf0DataType, DecoderFunction> _decodeFunctions =
            new Dictionary<Amf0DataType, DecoderFunction>()
        {
            { Amf0DataType.Number, DecodeNumber },
            { Amf0DataType.Boolean, DecodeBool },
            { Amf0DataType.String, DecodeShortString },
            { Amf0DataType.Object, DecodeObject },
            { Amf0DataType.Null, DecodeNull },
            { Amf0DataType.Undefined, DecodeNull },
            { Amf0DataType.LongString, DecodeLongString },
            { Amf0DataType.EcmaArray, DecodeEcmaArray },
            { Amf0DataType.StrictArray, DecodeArray }
        };

        public static int GetLength(object value)
        {
            var length = 0;
            if (value == null)
                length = 1;
            else if (value is double || value is int || value is long)
                length = 9;
            else if (value is string stringValue)
            {
                length += 3 + Encoding.UTF8.GetByteCount(stringValue);
            }
            else if (value is ExpandoObject dynamicObject)
            {
                length = 4;
                foreach (var property in dynamicObject)
                {
                    length += GetPropertyLength(property.Key, property.Value);
                }
            }
            else
            {
                length = 4;
                foreach (var property in value.GetType().GetProperties())
                {
                    length += GetPropertyLength(property.Name, property.GetValue(value));
                }
            }
            return length;
        }

        public static int GetPropertyLength(string name, object value)
        {
            return GetLength(name) + GetLength(value) - 1;
        }

        public static AmfObject Decode(ReadOnlySpan<byte> buffer)
        {
            var type = (Amf0DataType)buffer[0];
            var (length, value) =  _decodeFunctions[type](buffer.Slice(1));
            return (length + 1, value);
        }

        public static AmfObject DecodeNull(ReadOnlySpan<byte> buffer)
        {
            return (0, null);
        }

        public static AmfObject DecodeBool(ReadOnlySpan<byte> buffer)
        {
            return (1, buffer[0] != 0);
        }

        public static AmfObject DecodeNumber(ReadOnlySpan<byte> buffer)
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(buffer);
            return (8, BitConverter.Int64BitsToDouble(value));
        }

        public static AmfObject DecodeUnicodeString(ReadOnlySpan<byte> buffer)
        {
            var length = BinaryPrimitives.ReadInt16BigEndian(buffer);
            var data = Encoding.UTF8.GetString(buffer.Slice(2, length));
            return (length + 2, data);
        }

        public static AmfObject DecodeShortString(ReadOnlySpan<byte> buffer)
        {
            return DecodeUnicodeString(buffer);
        }

        public static AmfObject DecodeLongString(ReadOnlySpan<byte> buffer)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(buffer);
            var data =  Encoding.UTF8.GetString(buffer.Slice(2, length));
            return (length + 4, data);
        }

        public static AmfObject DecodeObject(ReadOnlySpan<byte> buffer)
        {
            var length = 0;
            var data = new ExpandoObject() as IDictionary<string, object>;
            while (buffer[0] != (byte) Amf0DataType.ObjectEnd)
            {
                var (keyLength, key) = DecodeUnicodeString(buffer);
                buffer = buffer.Slice(keyLength);
                length += keyLength;

                if (buffer[0] == (byte)Amf0DataType.ObjectEnd)
                    break;
                var (valueLength, value) = Decode(buffer);
                length += valueLength;
                data.Add(key as string, value);
                buffer = buffer.Slice(valueLength);
            }

            return (length + 1, data);
        }
        private static AmfObject DecodeEcmaArray(ReadOnlySpan<byte> buffer)
        {
            var(length, data) = DecodeObject(buffer.Slice(4));
            return (length + 4, data);
        }

        private static AmfObject DecodeArray(ReadOnlySpan<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public static int Encode(Span<byte> buffer, object value)
        {
            if (value == null)
                return EncodeNull(buffer);
            else if (value is double doubleValue)
                return EncodeNumber(buffer, doubleValue);
            else if (value is int intValue)
                return EncodeNumber(buffer, intValue);
            else if (value is bool boolValue)
                return EncodeBool(buffer, boolValue);
            else if (value is string strValue)
                return EncodeShortString(buffer, strValue);
            else if (value is ExpandoObject dynamicObject)
                return EncodeObject(buffer, dynamicObject);
            else if (value is object)
                return EncodeObject(buffer, value);
            throw new ArgumentException("Data type not support", nameof(value));
        }

        public static int EncodeNull(Span<byte>buffer)
        {
            buffer[0] = (byte)Amf0DataType.Null;
            return 1;
        }

        public static int EncodeBool(Span<byte> buffer, bool value)
        {
            buffer[0] = (byte)Amf0DataType.Boolean;
            buffer[1] = (byte) (value ? 1 : 0);
            return 2;
        }

        public static int EncodeNumber(Span<byte> buffer, double value)
        {
            buffer[0] = (byte) Amf0DataType.Number;
            BinaryPrimitives.WriteInt64BigEndian(
                buffer.Slice(1),
                BitConverter.DoubleToInt64Bits(value));
            return 9;
        }

        public static int EncodeUnicodeString(Span<byte> buffer, string value)
        {
            var length = (short)Encoding.UTF8.GetByteCount(value);
            BinaryPrimitives.WriteInt16BigEndian(buffer, length);
            Encoding.UTF8.GetBytes(value, buffer.Slice(2));
            return length + 2;
        }

        public static int EncodeShortString(Span<byte> buffer, string value)
        {
            buffer[0] = (byte)Amf0DataType.String;
            return EncodeUnicodeString(buffer.Slice(1), value) + 1;
        }

        public static int EncodeObject(Span<byte> buffer, ExpandoObject data)
        {
            var length = 4;
            buffer[0] = (byte)Amf0DataType.Object;
            buffer = buffer.Slice(1);
            foreach (var property in data as IDictionary<string, object>)
            {
                var keyLength = EncodeUnicodeString(buffer, property.Key);
                length += keyLength;
                buffer = buffer.Slice(keyLength);
                var valueLength = Encode(buffer, property.Value);
                length += valueLength;
                buffer = buffer.Slice(valueLength);
            }
            EncodeUnicodeString(buffer, string.Empty);
            buffer = buffer.Slice(2);
            buffer[0] = (byte)Amf0DataType.ObjectEnd;
            return length;
        }

        public static int EncodeObject(Span<byte> buffer, object data)
        {
            var length = 2;
            buffer[0] = (byte)Amf0DataType.Object;
            buffer = buffer.Slice(1);
            foreach (var property in data.GetType().GetProperties())
            {
                var keyLength = EncodeUnicodeString(buffer, property.Name);
                length += keyLength;
                buffer = buffer.Slice(keyLength);
                var valueLength = Encode(buffer, property.GetValue(data));
                length += valueLength;
                buffer = buffer.Slice(valueLength);
            }
            buffer[0] = (byte)Amf0DataType.ObjectEnd;
            return length;
        }
    }
}
