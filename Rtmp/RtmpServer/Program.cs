using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RtmpCore;

namespace RTMPServer
{
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
                    services.Configure<ServerConfiguration>(hostbuilder.Configuration.GetSection("rtmp"));
                    services.Configure<TransMuxerConfiguration>(hostbuilder.Configuration.GetSection("muxer"));
                    services.AddHostedService<RtmpServer>();
                    services.AddHostedService<TransMuxer>();
                    services.AddSingleton<RtmpContext>();
                })
                .UseConsoleLifetime()
                .Build();

            Console.WriteLine("Press Ctrl+C to exit...");
            await host.RunAsync();
        }
    }
}
