using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            using var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        public static IHostBuilder ConfigureRtmpServer(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<RtmpContext>();
                services.Configure<ServerConfiguration>(context.Configuration.GetSection("rtmp"));
                services.Configure<TransMuxerConfiguration>(context.Configuration.GetSection("muxer"));
                services.AddHostedService<RtmpServer>();
                services.AddHostedService<TransMuxer>();
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
