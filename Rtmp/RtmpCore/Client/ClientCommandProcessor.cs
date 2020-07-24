using RtmpCore.Amf;
using System;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class ClientCommandProcessor : RtmpCommandProcessor
    {
        private readonly ClientSession _session;

        public ClientCommandProcessor(ClientSession session) : base(session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        protected override Task ProcessCommandAsync(AmfCommandMessage command, RtmpMessage message)
        {
            switch (command.Name)
            {
                default:
                    throw new InvalidOperationException($"Unknown command {command.Name} {command.Data} ");
            }
        }
    }
}
