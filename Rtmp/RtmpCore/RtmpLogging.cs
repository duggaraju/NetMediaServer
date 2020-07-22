using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace RtmpCore
{
    public static class RtmpLogging
    {
        private static ILoggerFactory _loggerFactory;

        public static void Initialize(ILoggerFactory loggerFactory)
        {
            if (Interlocked.Exchange(ref _loggerFactory, loggerFactory) != null)
                throw new InvalidOperationException("Duplicate log initalization");
        }

        public static ILoggerFactory LoggerFactory => _loggerFactory;
    }
}
