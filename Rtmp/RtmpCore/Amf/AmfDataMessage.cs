using System;
using System.Collections.Generic;

namespace RtmpCore.Amf
{
    public class AmfDataMessage : AmfMessage
    {
        private static readonly Dictionary<string, string[]> _commands = new Dictionary<string, string[]>
        {
            {  "@setDataFrame", new[] { "method", "dataObj" } },
            {  "onFI", new[] { "info" } },
            {  "onMetaData", new[] { "dataObj" } },
            {  "|RtmpSampleAccess", new[] {"bool1", "bool2" } },
        };

        protected override string[] GetProperties()
        {
            if (_commands.TryGetValue(Name, out var properties))
                return properties;
            throw new InvalidOperationException($"command {Name} not found");
        }
    }
}
