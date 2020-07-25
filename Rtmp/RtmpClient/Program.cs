using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RtmpCore;
using RtmpCore.Client;

namespace RtmpClient
{
    class RtmpClient : IHostedService
    {
        private CancellationTokenSource _source = new CancellationTokenSource();
        private readonly IOptions<ClientConfiguration> _configuration;
        private TcpClient _client;
        private ClientSession _session;
        private readonly ILogger _logger;

        public RtmpClient(IOptions<ClientConfiguration> configuration, ILoggerFactory loggerFactory)
        {
            RtmpLogging.Initialize(loggerFactory);
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<RtmpClient>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client = new TcpClient(_configuration.Value.Server.Host, _configuration.Value.Server.Port);
            _session = new ClientSession(_client, _configuration);
            _session.StateChanged += async(s, args) => await OnStateChange(s, args);
            _session.MediaReceived += OnMediaReceived;
            _session.Start();
            _ = Task.Run(async () => await RunAsync(_source.Token), _source.Token);
            return Task.CompletedTask;
        }

        private void OnMediaReceived(object sender, EventArgs<RtmpMessage> e)
        {
            _logger.LogInformation($"Received {e.Data.Message.MessageType} time:{e.Data.Message.Timestamp} length: {e.Data.Message.Length}");
        }

        private async Task OnStateChange(object sender, EventArgs args)
        {
            _logger.LogInformation("Client state changed to {0}", _session.State);
            switch (_session.State)
            {
                case ClientSessionState.Conntected:
                    var streamName = _configuration.Value.Server.AbsolutePath.Split('/')[2];
                    await _session.StartPlayback(streamName);
                    break;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _session.StopPlayback();
            await _session.DisconnectAsync();
            _session.Stop();
            _source.Cancel();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var appName = _configuration.Value.Server.AbsolutePath.Split('/')[1];
            var streamName = _configuration.Value.Server.AbsolutePath.Split('/')[2];
            await _session.ConnectAsync(appName);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(configure =>
                {
                    configure.AddJsonFile("appsettings.json");
                    configure.AddCommandLine(args);
                })
                .ConfigureLogging(configure => configure.AddConsole())
                .ConfigureServices((hostbuilder, services) => 
                {
                    services.Configure<ClientConfiguration>(hostbuilder.Configuration.GetSection("rtmp"));
                    services.AddHostedService<RtmpClient>();
                })
                .UseConsoleLifetime()
                .Build();

            Console.WriteLine("Press Ctrl+C to exit...");
            await host.RunAsync();
        }
    }
}
