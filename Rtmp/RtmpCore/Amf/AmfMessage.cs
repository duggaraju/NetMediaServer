using System;
using System.Collections.Generic;
using System.Dynamic;

namespace RtmpCore.Amf
{
    abstract public class AmfMessage
    {
        public string Name { get; set; }

        public dynamic Data { get; set; }

        public AmfEncodingType EncodingType { get; set; } = AmfEncodingType.Amf0;

        protected abstract string[] GetProperties();

        public void Decode(ReadOnlySpan<byte> buffer)
        {
            var (length, commandName) = Amf0Decoder.Decode(buffer);
            Name = commandName as string;
            Data = new ExpandoObject();
            var data = Data as IDictionary<string, object>;
            var properties = GetProperties();

            foreach (var property in properties)
            {
                buffer = buffer.Slice(length);
                if (buffer.Length == 0)
                    break;
                object value;
                (length, value) = Amf0Decoder.Decode(buffer);
                data.Add(property, value);
            }
        }

        public void Encode(Span<byte> buffer)
        {
            object data = Data;
            var length = Amf0Decoder.Encode(buffer, Name);
            buffer = buffer.Slice(length);
            var properties = GetProperties();
            if (Data is ExpandoObject dynamicObject)
                length += Encode(buffer, dynamicObject, properties);
            else
                length += Encode(buffer, data, properties);
        }

        public static int Encode(Span<byte> buffer, ExpandoObject data, string[] properties)
        {
            IDictionary<string, object> dict = data;
            var length = 0;
            foreach (var property in properties)
            {
                if (dict.TryGetValue(property, out var value))
                {
                    var propLength = Amf0Decoder.Encode(buffer, value);
                    buffer = buffer.Slice(propLength);
                    length += propLength;
                }
            }
            return length;
        }

        public static int Encode(Span<byte> buffer, object data, string[] properties)
        {
            var type = data.GetType();
            var length = 0;
            foreach (var property in properties)
            {
                var propInfo = type.GetProperty(property);
                if (propInfo != null)
                {
                    var value = propInfo.GetValue(data);
                    var propLength = Amf0Decoder.Encode(buffer, value);
                    length += propLength;
                    buffer = buffer.Slice(propLength);
                }
            }
            return length;
        }

        public int GetLength()
        {
            var length = Amf0Decoder.GetLength(Name);
            var properties = GetProperties();
            if (Data is ExpandoObject dynamicObject)
            {
                length += GetPropertyLength(dynamicObject, properties);
            }
            else
            {
                length += GetPropertyLength(Data, properties);
            }
            return length;
        }

        public static int GetPropertyLength(object data, string[] properties)
        {
            var type = data.GetType();
            int length = 0;
            foreach (var property in properties)
            {
                var propInfo = type.GetProperty(property);
                if (propInfo != null)
                    length += Amf0Decoder.GetLength(propInfo.GetValue(data));
            }
            return length;
        }

        public static int GetPropertyLength(ExpandoObject data, string[] properties)
        {
            IDictionary<string, object> dict = data;
            int length = 0;
            foreach (var property in properties)
            {
                if (dict.TryGetValue(property, out var value))
                    length += Amf0Decoder.GetLength(value);
            }
            return length;
        }
    }
}
