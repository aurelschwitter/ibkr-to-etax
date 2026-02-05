using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace IbkrToEtax.Tests
{
    public static class TestLoggerFactory
    {
        public static ILoggerFactory Create()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddNLog();
            });
        }
    }
}
