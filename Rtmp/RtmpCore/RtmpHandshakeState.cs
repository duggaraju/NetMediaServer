namespace RtmpCore
{
    enum RtmpHandshakeState
    {
        Uninitialized = 0,
        VersionSent = 1,
        AckSent = 2,
        HandshakeDone = 3
    }
}
