using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class TransMuxer : IHostedService
    {
        private readonly RtmpContext _context;
        private readonly IOptions<ServerConfiguration> _rtmpConfig;
        private readonly IOptions<TransMuxerConfiguration> _muxConfig;
        private readonly CancellationTokenSource _source = new CancellationTokenSource();

        public TransMuxer(RtmpContext context, IOptions<ServerConfiguration> rtmpConfig, IOptions<TransMuxerConfiguration> muxConfig)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _rtmpConfig = rtmpConfig ?? throw new ArgumentNullException(nameof(rtmpConfig));
            _muxConfig = muxConfig ?? throw new ArgumentNullException(nameof(muxConfig));
        }

        public Task StartAsync(CancellationToken _)
        {
            _context.StreamPublished += OnStreamPublished;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken _)
        {
            _source.Cancel();
            _context.StreamPublished -= OnStreamPublished;
            return Task.CompletedTask;
        }

        private void OnStreamPublished(object sender, RtmpContext.RtmpEventArgs args)
        {
            var session = new TransMuxSession(_rtmpConfig, _muxConfig);
            Task.Run(async () => await session.StartAsync(args, _source.Token));
        }
    }
}
