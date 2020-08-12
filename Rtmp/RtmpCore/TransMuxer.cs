using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
        private readonly ILogger _logger;
        private readonly Dictionary<string, TransMuxSession> _sessions = new Dictionary<string, TransMuxSession>();

        public TransMuxer(RtmpContext context, IOptions<ServerConfiguration> rtmpConfig, IOptions<TransMuxerConfiguration> muxConfig, ILogger<TransMuxer> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _rtmpConfig = rtmpConfig ?? throw new ArgumentNullException(nameof(rtmpConfig));
            _muxConfig = muxConfig ?? throw new ArgumentNullException(nameof(muxConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken _)
        {
            _context.StreamPublished += OnStreamPublished;
            _context.StreamUnpublished += _context_StreamUnpublished;
            return Task.CompletedTask;
        }

        private void _context_StreamUnpublished(object sender, RtmpContext.RtmpEventArgs e)
        {
            if (_sessions.Remove(e.StreamPath, out var session))
                session.Stop();
        }

        public Task StopAsync(CancellationToken _)
        {
            _source.Cancel();
            _context.StreamPublished -= OnStreamPublished;
            foreach (var session in _sessions.Values)
                session.Stop();
            return Task.CompletedTask;
        }

        private void OnStreamPublished(object sender, RtmpContext.RtmpEventArgs args)
        {
            _logger.LogWarning($"Starting transmuxing for stream {args.StreamPath}");
            var session = new TransMuxSession(_rtmpConfig, _muxConfig);
            _sessions.Add(args.StreamPath, session);
            Task.Run(() => session.Start(args, _source.Token));
        }
    }
}
