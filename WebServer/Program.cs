using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RtmpCore;
using System.Threading.Tasks;

namespace WebServer
{
    [MemoryDiagnoser]
    [NativeMemoryProfiler]
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // BenchmarkSwitcher.FromTypes(new[] { typeof(Program) }).Run(args);
            await RunAsync(args);
        }

        [Benchmark]
        public static async Task RunAsync(string[] args)
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                var webTask = host.RunAsync();
                var rtmpTask = RunRtmpServer(host);
                await Task.WhenAll(webTask, rtmpTask);
            }
        }

        public static async Task RunRtmpServer(IHost host)
        {
            RtmpLogging.Initialize(host.Services.GetService<ILoggerFactory>());
            var context = host.Services.GetService<RtmpContext>();
            var rtmpConfig = host.Services.GetService<IOptions<RtmpConfiguration>>();
            var transmux = new TransMuxer(
                context,
                rtmpConfig,
                host.Services.GetService<IOptions<TransMuxerConfiguration>>());
            transmux.Run();
            var server = new RtmpServer(
                context,
                rtmpConfig);
            await server.RunAsync();
        }

        public static IHostBuilder ConfigureRtmpServer(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<RtmpContext>();
                services.Configure<RtmpConfiguration>(context.Configuration.GetSection("rtmp"));
                services.Configure<TransMuxerConfiguration>(context.Configuration.GetSection("muxer"));
            });
            return hostBuilder;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureRtmpServer()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}
