namespace RtmpCore
{
    public enum RtmpMessageType : byte
    {
        None = 0x0,

        //
        // Protocol Control Messages
        //
        SetChunkSize = 0x1,
        AbortMessage = 0x2,
        Acknowledgement = 0x3,
        WindowAcknowledgementSize = 0x5,
        SetPeerBandwidth = 0x6,

        //
        // User Control Message
        //
        UserCtrlMessage = 0x4,

        //
        // Command Message
        //
        CommandAMF0 = 0x14,
        CommandAMF3 = 0x11,

        //
        // Data Message
        //
        DataAMF0 = 0x12,
        DataAMF3 = 0xF,

        //
        // Shared Object Message
        //
        SharedObjectAMF0 = 0x13,
        SharedObjectAMF3 = 0x10,

        //
        // Media Payload
        //
        Audio = 0x8,
        Video = 0x9,

        //
        // Aggregate Message
        //
        Aggregate = 0x16
    }
}
