using System.CommandLine;
using BoothDownloader.commands;

namespace BoothDownloader;

internal static class BoothDownloader
{
    private static async Task<int> Main(string[] args)
    {
        Console.Title = $"BoothDownloader - V{typeof(BoothDownloader).Assembly.GetName().Version}";

        var baseCommand = new BaseCommand();

        return await baseCommand.InvokeAsync(args);
    }
}