using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Discord.Common.Helpers
{
    public static class LoggerHelper
    {
        public static readonly Microsoft.Extensions.Logging.ILogger GlobalLogger = BuildLoggerFactory().CreateLogger("BoothDownloader");

        public static ILoggerFactory BuildLoggerFactory(string? sourceContext = null)
        {
            return new LoggerFactory().AddSerilog(logger: GetSerilogConfiguration(sourceContext).CreateLogger());
        }

        public static ILoggingBuilder AddCommonSerilog(this ILoggingBuilder builder, string? sourceContext = null)
        {
            return builder.AddSerilog(logger: GetSerilogConfiguration(sourceContext).CreateLogger());
        }

        public static LoggerConfiguration GetSerilogConfiguration(string? sourceContext = null)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Information)
                .WriteTo.File("BoothDownloader.log", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] " + (sourceContext ?? "{SourceContext}") + ": {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] " + (sourceContext ?? "{SourceContext}") + ": {Message:lj}{NewLine}{Exception}");
        }
    }
}
