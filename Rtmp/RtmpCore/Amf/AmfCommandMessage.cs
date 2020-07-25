using System;
using System.Collections.Generic;

namespace RtmpCore.Amf
{
    public class AmfCommandMessage : AmfMessage
    {
        public int TransactionId { get; set; }

        public dynamic CommandObject { get; set; }

        private static readonly Dictionary<string, string[]> _commands = new Dictionary<string, string[]>
        {
            { "_result", new[] { "transId", "cmdObj", "info"} },
            { "_error", new[] { "transId", "cmdObj", "info", "streamId"} }, // Info / Streamid are optional
            { "onStatus", new[] { "transId", "cmdObj", "info"} },
            { "releaseStream", new[] { "transId", "cmdObj", "streamName"} },
            { "getStreamLength", new[] { "transId", "cmdObj", "streamId"} },
            { "getMovLen", new[] { "transId", "cmdObj", "streamId"} },
            { "FCPublish", new[] { "transId", "cmdObj", "streamName"} },
            { "FCUnpublish", new[] { "transId", "cmdObj", "streamName"} },
            { "FCSubscribe", new[] { "transId", "cmdObj", "streamName"} },
            { "onFCPublish", new[] { "transId", "cmdObj", "info"} },
            { "connect", new[] { "transId", "cmdObj", "args"} },
            { "call", new[] { "transId", "cmdObj", "args"} },
            { "createStream", new[] { "transId", "cmdObj"} },
            { "close", new[] { "transId", "cmdObj"} },
            { "play", new[] { "transId", "cmdObj", "streamName", "start", "duration", "reset"} },
            { "play2", new[] { "transId", "cmdObj", "params"} },
            { "deleteStream", new[] { "transId", "cmdObj", "streamId"} },
            { "closeStream", new[] { "transId", "cmdObj"} },
            { "receiveAudio", new[] { "transId", "cmdObj", "bool"} },
            { "receiveVideo", new[] { "transId", "cmdObj", "bool"} },
            { "publish", new[] { "transId", "cmdObj", "streamName", "type"} },
            { "seek", new[] { "transId", "cmdObj", "ms"} },
            { "pause", new[] { "transId", "cmdObj", "pause", "ms" } }
        };

        protected override int GetPropertyCount()
        {
            if (_commands.TryGetValue(Name, out var properties))
                return properties.Length;
            throw new InvalidOperationException($"command {Name} not found");
        }

        protected override void SetProperty(int index, object value)
        {
            if (index == 0)
                TransactionId = (int)(double)value;
            else if (index == 1)
                CommandObject = value;
            else
                base.SetProperty(index, value);
        }

        protected override object GetProperty(int index)
        {
            if (index == 0)
                return TransactionId;
            if (index == 1)
                return CommandObject;
            return base.GetProperty(index - 2);
        }
    }
}
