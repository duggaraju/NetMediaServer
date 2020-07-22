using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RtmpCore;

namespace RtmpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: RtmpClient <server> [port]");
            }
            var server = args[0];
            var port = args.Length >= 2 ? int.Parse(args[1]) : 1935;
            var loggerFactory = LoggerFactory.Create(configure => configure.AddConsole());
            RtmpLogging.Initialize(loggerFactory);
            var context = new RtmpContext();
            var client = new TcpClient(server, port);
            var session = new RtmpSession(context, client);
            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (e, args) => cts.Cancel();
                Console.WriteLine("Press Ctrl+C to exit...");
                await session.StartAsync(cts.Token);
            }

        }
    }
}
