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

namespace RtmpClient
{
    class RtmpClient : IHostedService
    {
        private CancellationTokenSource _source = new CancellationTokenSource();
        private readonly IOptions<ClientConfiguration> _configuration;
        private TcpClient _client;
        private ClientSession _session;

        public RtmpClient(IOptions<ClientConfiguration> configuration, ILoggerFactory loggerFactory)
        {
            RtmpLogging.Initialize(loggerFactory);
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client = new TcpClient(_configuration.Value.Server.Host, _configuration.Value.Server.Port);
            _session = new ClientSession(_client, _configuration);
            await _session.RunAsync(cancellationToken);
            _ = Task.Run(async () => await RunAsync(_source.Token), _source.Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _session.StopPlayback();
            await _session.DisconnectAsync();
            _source.Cancel();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var appName = _configuration.Value.Server.AbsolutePath.Split('/')[0];
            var streamName = _configuration.Value.Server.AbsolutePath.Split('/')[1];
            await _session.ConnectAsync(appName);
            await _session.StartPlayback(streamName);
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
