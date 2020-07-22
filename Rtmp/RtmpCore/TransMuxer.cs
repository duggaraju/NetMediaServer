using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class TransMuxer
    {
        private readonly RtmpContext _context;
        private readonly IOptions<RtmpConfiguration> _rtmpConfig;
        private readonly IOptions<TransMuxerConfiguration> _muxConfig;
        private CancellationTokenSource _source;

        public TransMuxer(RtmpContext context, IOptions<RtmpConfiguration> rtmpConfig, IOptions<TransMuxerConfiguration> muxConfig)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _rtmpConfig = rtmpConfig ?? throw new ArgumentNullException(nameof(rtmpConfig));
            _muxConfig = muxConfig ?? throw new ArgumentNullException(nameof(muxConfig));
        }

        public void Run(CancellationToken cancellationToken = default)
        {
            if (_source != null) throw new InvalidOperationException("Run already called");
            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _context.StreamPublished += OnStreamPublished;
            cancellationToken.Register(() =>
            {
                _context.StreamPublished -= OnStreamPublished;
            });
        }

        private void OnStreamPublished(object sender, RtmpContext.RtmpEventArgs args)
        {
            var session = new TransMuxSession(_rtmpConfig, _muxConfig);
            Task.Run(async () => await session.StartAsync(args, _source.Token));
        }
    }
}
