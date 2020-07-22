using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace RtmpCore
{
    public class RtmpEventProcessor : IRtmpMessageProcessor
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<RtmpEventProcessor>();

        public Task ProcessMessageAsync(RtmpMessage message)
        {
            _logger.LogInformation("Received event for session");
            return Task.CompletedTask;
        }
    }
}
