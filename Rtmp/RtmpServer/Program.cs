using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RtmpCore;

namespace RTMPServer
{
    class Program
    {
        const int DefaultPort = 1935;

        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(configure => configure.AddConsole(configure => configure.LogToStandardErrorThreshold  = LogLevel.Error));
            RtmpLogging.Initialize(loggerFactory);
            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (e, args) => cts.Cancel();
                Console.WriteLine("Press Ctrl+C to exit...");
                await RunServerAsync(cts.Token);
            }
        }

        private static async Task RunServerAsync(CancellationToken cancellationToken)
        {
            var rtmpConfig = Options.Create(new RtmpConfiguration());
            var muxConfig = Options.Create(new TransMuxerConfiguration());
            var context = new RtmpContext();
            var server = new RtmpServer(context, rtmpConfig);
            var transmux = new TransMuxer(context, rtmpConfig, muxConfig);
            transmux.Run(cancellationToken);
            await server.RunAsync();
        }
    }
}
