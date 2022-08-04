using Microsoft.Extensions.Logging;

namespace BoothDownloader.log;

public static class Log
{
    private static ILoggerFactory? _loggerFactory;

    public static ILoggerFactory Factory
    {
        get
        {
            if (_loggerFactory == null)
            {
                _loggerFactory = LoggerFactory.Create(builder => builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "yyyy-MM-dd hh:mm:ss ";
                    })
                );
            }

            return _loggerFactory;
        }
    }
}