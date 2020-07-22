
namespace RtmpCore
{
    public enum Amf0DataType : byte
    {
        Number = 0x00,
        Boolean = 0x01,
        String = 0x02,
        Object = 0x03,
        Movieclip = 0x04,     // reserved, not supported
        Null = 0x05,
        Undefined = 0x06,     // Not implemented
        Reference = 0x07,     // Not implemented
        EcmaArray = 0x08,
        ObjectEnd = 0x09,
        StrictArray = 0x0A,
        Date = 0x0B,     // Not implemented
        LongString = 0x0C,     // Not implemented
        Unsupported = 0x0D,     // Not implemented
        Recordset = 0x0E,     // ; reserved, not supported
        XmlDocument = 0x0F,     // Not implemented
        TypedObject = 0x10,     // Not implemented
    };
}
