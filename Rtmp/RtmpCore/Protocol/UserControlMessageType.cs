
namespace RtmpCore
{
    public enum  UserControlMessageType : short
    {
        StreamBegin = 0,
        StreamEOF = 1,
        StreamDry =2 ,
        SetBufferLength = 3,
        StreamIsRecorded = 4,
        PingRequest = 5,
        PingResponse = 6
    }
}
