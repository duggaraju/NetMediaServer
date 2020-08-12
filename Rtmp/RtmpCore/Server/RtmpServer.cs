using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpServer : IHostedService
    {
        private readonly RtmpContext _context;
        private readonly IOptions<ServerConfiguration> _configuration;
        private TcpListener _listener;
        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private readonly ILogger _logger;

        public RtmpServer(RtmpContext context, IOptions<ServerConfiguration> configuration, ILoggerFactory loggerFactory)
        {
            RtmpLogging.Initialize(loggerFactory);
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = loggerFactory.CreateLogger<RtmpServer>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = TcpListener.Create(_configuration.Value.Port);
            _listener.Start();
            _ = Task.Run(async () => await RunAsync(_source.Token), _source.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _source.Cancel();
            _listener.Stop();
            return Task.CompletedTask;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _logger.LogInformation("Recevied connection from ", client.Client.RemoteEndPoint);
                var session = new ServerSession(_context, _configuration, client);
                _ = session.StartAsync(cancellationToken);
            }
        }
    }
}
