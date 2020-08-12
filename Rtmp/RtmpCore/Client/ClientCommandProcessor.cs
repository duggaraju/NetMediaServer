using RtmpCore.Amf;
using System;
using System.Threading.Tasks;

namespace RtmpCore.Client
{
    public class CommandArgs : EventArgs
    {
        public AmfCommandMessage Command { get; set; }
    }

    public class ClientCommandProcessor : RtmpCommandProcessor
    {
        private readonly ClientSession _session;

        public ClientCommandProcessor(ClientSession session) : base(session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public event EventHandler<CommandArgs> ResponseReceived;

        protected override Task ProcessCommandAsync(AmfCommandMessage command, RtmpMessage message)
        {
            switch (command.Name)
            {
                case "_result":
                case "onStatus":
                    HandleResponse(command, message);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command {command.Name} {command.CommandObject} ");
            }
            return Task.CompletedTask;
        }

        private void HandleResponse(AmfCommandMessage command, RtmpMessage message)
        {
            var result = new CommandArgs
            {
                Command = command
            };

            ResponseReceived?.Invoke(this, result);
        }
    }
}
