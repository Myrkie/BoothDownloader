using BoothDownloader.Cli;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.Title = Constants.Version;
        LoggerHelper.GlobalLogger.LogInformation("{VersionString}", Constants.Version);

        Environment.CurrentDirectory = AppContext.BaseDirectory;

#if WINDOWS_BUILD
        args = BoothDownloaderProtocol.HandleProtocol(args);
#endif

        var rootCommand = BoothDownloaderOptions.CreateCommand();

        return await rootCommand.Parse(args).InvokeAsync();
    }
}