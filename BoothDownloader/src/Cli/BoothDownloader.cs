using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using BoothDownloader.Web;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.Cli;

internal static class BoothDownloader
{
    internal static async Task<int> RunAsync(BoothDownloaderOptions options, CancellationToken cancellationToken)
    {
        if (options.Debug)
        {
            LoggerHelper.GlobalLogger.LogInformation("Arguments:\n{Args}", Environment.CommandLine);
        }

        if (options.RegisterProtocol)
        {
            BoothDownloaderProtocol.RegisterContext();
            return 0;
        }

        if (options.UnregisterProtocol)
        {
            BoothDownloaderProtocol.UnregisterContext();
            return 0;
        }

        BoothConfig.Setup(options.ConfigFile);

        while (true)
        {
            if (string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie))
            {
                Console.WriteLine("Please paste in your cookie from browser, or press Enter for anonymous mode.\n");
                var cookie = await Console.In.ReadLineAsync(cancellationToken);
                BoothConfig.Instance.Cookie = string.IsNullOrWhiteSpace(cookie) ? BoothConfig.AnonymousCookie : cookie;
                BoothConfig.ConfigInstance.Save();
                LoggerHelper.GlobalLogger.LogInformation("Cookie set");
            }

            await BoothHttpClientManager.Setup(cancellationToken);
            if (!string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie))
            {
                break;
            }

            Console.WriteLine("BOOTH rejected that cookie. Copy the value of _plaza_session_nktz7u and try again.\n");
        }

        var boothInput = options.BoothInput;
        if (string.IsNullOrEmpty(boothInput))
        {
            Console.WriteLine("Enter the Booth ID or URL\nOr one of the following collections: owned, library, gifts");
            Console.Write("> ");
            boothInput = await Console.In.ReadLineAsync(cancellationToken);
        }

        var commands = boothInput?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var isLibraryPage = false;
        var isGiftPage = false;
        var boothIds = new List<string>();

        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            if (command.Equals("https://accounts.booth.pm/library", StringComparison.OrdinalIgnoreCase) ||
                command.Equals("library", StringComparison.OrdinalIgnoreCase) ||
                command.Equals("libraries", StringComparison.OrdinalIgnoreCase))
            {
                isLibraryPage = true;
            }
            else if (command.Equals("https://accounts.booth.pm/library/gifts", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("gift", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("gifts", StringComparison.OrdinalIgnoreCase))
            {
                isGiftPage = true;
            }
            else if (command.Equals("https://accounts.booth.pm/orders", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("orders", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("order", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("purchase", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("purchases", StringComparison.OrdinalIgnoreCase))
            {
                LoggerHelper.GlobalLogger.LogInformation("Orders Page now uses Library!");
                isLibraryPage = true;
            }
            else if (command.Equals("own", StringComparison.OrdinalIgnoreCase) ||
                     command.Equals("owned", StringComparison.OrdinalIgnoreCase))
            {
                isLibraryPage = true;
                isGiftPage = true;
            }
            else
            {
                var boothId = RegexStore.IdRegex.Matches(command).Select(x => x.Groups[1].Value).Distinct();
                var enumerable = boothId as string[] ?? boothId.ToArray();
                if (!enumerable.Any())
                {
                    LoggerHelper.GlobalLogger.LogError("Could not parse booth IDs, {Command}", command);
                }

                boothIds.AddRange(enumerable);
            }
        }

        Dictionary<string, BoothItemAssets> items = [];

        if (isLibraryPage || isGiftPage)
        {
            if (BoothHttpClientManager.IsAnonymous)
            {
                LoggerHelper.GlobalLogger.LogError("Cannot download Paid Items with invalid cookie");
                LoggerHelper.GlobalLogger.LogInformation("Continuing in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            else
            {
                if (isLibraryPage)
                {
                    LoggerHelper.GlobalLogger.LogInformation("Grabbing all Paid Library Items");
                    items = await BoothPageParser.GetPageItemsAsync("library", items, cancellationToken: cancellationToken);
                }

                if (isGiftPage)
                {
                    LoggerHelper.GlobalLogger.LogInformation("Grabbing all Paid Gifts");
                    items = await BoothPageParser.GetPageItemsAsync("library/gifts", items, cancellationToken: cancellationToken);
                }
            }
        }

        if (boothIds.Count > 0)
        {
            LoggerHelper.GlobalLogger.LogInformation("Grabbing the following booth Ids: {BoothIds}", string.Join(';', boothIds));
            items = await BoothPageParser.GetItemsAsync(boothIds, items, isGiftPage, cancellationToken: cancellationToken);
        }

        if (items.Count > 0)
        {
            await BoothBatchDownloader.DownloadAsync(items, options.OutputDirectory, options.MaxRetries, options.Debug, options.Force, cancellationToken);
        }
        else
        {
            LoggerHelper.GlobalLogger.LogError("No items found to download, exiting in 5 seconds");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        return 0;
    }
}