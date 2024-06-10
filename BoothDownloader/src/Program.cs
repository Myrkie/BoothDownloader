using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using BoothDownloader.Web;

namespace BoothDownloader;

internal static class BoothDownloader
{
    private static async Task<int> Main(string[] args)
    {
        Console.Title = $"BoothDownloader - V{typeof(BoothDownloader).Assembly.GetName().Version}";

        var rootCommand = new RootCommand("Booth Downloader");

        var configOption = new Option<string>(
            name: "--config",
            description: "Path to configuration file",
            getDefaultValue: () => "./BDConfig.json"
        );

        var boothOption = new Option<string?>(
            name: "--booth",
            description: "Booth IDs/URLs"
        );

        var outputDirectoryOption = new Option<string>(
            name: "--output-dir",
            description: "Output Directory",
            getDefaultValue: () => "./BoothDownloaderOut"
        );

        var maxRetriesOption = new Option<int>(
            name: "--max-retries",
            description: "maximum retries for downloading binary files",
            getDefaultValue: () => 3
        );

        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddOption(boothOption);
        rootCommand.AddOption(outputDirectoryOption);
        rootCommand.AddOption(maxRetriesOption);

        var cancellationTokenValueSource = new CancellationTokenValueSource();

        rootCommand.SetHandler(async (configFile, boothId, outputDirectory, maxRetries, cancellationToken) =>
        {
            BoothConfig.Setup(configFile);

            #region First Boot

            if (BoothConfig.Instance.Cookie == null)
            {
                Console.WriteLine("Please paste in your cookie from browser.\n");
                var cookie = Console.ReadLine();
                BoothConfig.Instance.Cookie = cookie ?? string.Empty;
                BoothConfig.ConfigInstance.Save();
                Console.WriteLine("Cookie set!\n");
            }

            #endregion

            if (string.IsNullOrEmpty(boothId))
            {
                Console.WriteLine("Enter the Booth ID or URL: ");
                boothId = Console.ReadLine();
            }

            #region Prep Booth Client
            await BoothHttpClientManager.Setup(cancellationToken);

            #endregion

            bool isLibraryPage = boothId?.Equals("https://accounts.booth.pm/library", StringComparison.OrdinalIgnoreCase) == true
                              || boothId?.Equals("library", StringComparison.OrdinalIgnoreCase) == true
                              || boothId?.Equals("libraries", StringComparison.OrdinalIgnoreCase) == true;

            bool isGiftPage = boothId?.Equals("https://accounts.booth.pm/library/gifts", StringComparison.OrdinalIgnoreCase) == true
                           || boothId?.Equals("gift", StringComparison.OrdinalIgnoreCase) == true
                           || boothId?.Equals("gifts", StringComparison.OrdinalIgnoreCase) == true;

            if (boothId?.Equals("https://accounts.booth.pm/orders", StringComparison.OrdinalIgnoreCase) == true
            || boothId?.Equals("orders", StringComparison.OrdinalIgnoreCase) == true
            || boothId?.Equals("order", StringComparison.OrdinalIgnoreCase) == true
            || boothId?.Equals("purchase", StringComparison.OrdinalIgnoreCase) == true
            || boothId?.Equals("purchases", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine("Orders Page now uses Library!");
                isLibraryPage = true;
            }

            if (boothId?.Equals("own", StringComparison.OrdinalIgnoreCase) == true
            || boothId?.Equals("owned", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine("Going to grab both Library Items and Gifts!");
                isLibraryPage = true;
                isGiftPage = true;
            }

            Dictionary<string, BoothItemAssets> items = [];

            if (isLibraryPage || isGiftPage)
            {
                if (BoothHttpClientManager.IsAnonymous)
                {
                    Console.WriteLine("Cannot download Paid Items with invalid cookie.\n");
                }
                else
                {
                    if (isLibraryPage)
                    {
                        Console.WriteLine("Grabbing all Paid Library Items!\n");
                        items = await BoothPageParser.GetPageItemsAsync("library", items, cancellationToken: cancellationToken);
                    }

                    if (isGiftPage)
                    {
                        Console.WriteLine("Grabbing all Paid Gifts!\n");
                        items = await BoothPageParser.GetPageItemsAsync("library/gifts", items, cancellationToken: cancellationToken);
                    }
                }
            }
            else
            {
                var boothIds = RegexStore.IdRegex.Matches(boothId!).Select(x => x.Groups[1].Value).Distinct();
                if (!boothIds.Any())
                {
                    Console.WriteLine("Could not parse booth IDs, assuming provided value is ID");
                    boothIds = [boothId!];
                }

                Console.WriteLine($"Grabbing the following booth Ids: {string.Join(';', boothIds)}\n");

                items = await BoothPageParser.GetItemsAsync(boothIds, items, cancellationToken: cancellationToken);
            }

            if (items.Count > 0)
            {
                await BoothBatchDownloader.DownloadAsync(items, outputDirectory, maxRetries, cancellationToken);
            }
            else
            {
                Console.WriteLine("No items found to download.");
            }
        }, configOption, boothOption, outputDirectoryOption, maxRetriesOption, cancellationTokenValueSource);

        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        var built = commandLineBuilder.Build();
        return await built.InvokeAsync(args);
    }
}