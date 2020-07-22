using Microsoft.Extensions.Options;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpServer
    {
        private readonly RtmpContext _context;
        private readonly IOptions<RtmpConfiguration> _configuration;

        public RtmpServer(RtmpContext context, IOptions<RtmpConfiguration> configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var listener = TcpListener.Create(_configuration.Value.Port);
            listener.Start();
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync();
                var session = new RtmpSession(_context, client);
                session.StartAsync(cancellationToken);
            }
        }
    }
}
