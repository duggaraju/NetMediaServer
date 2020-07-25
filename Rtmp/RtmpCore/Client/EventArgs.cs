using System;

namespace RtmpCore.Client
{
    public class EventArgs<T> : EventArgs
    {
        public EventArgs(T data)
        {
            Data = data;
        }

        public T Data { get; }
    }
}
