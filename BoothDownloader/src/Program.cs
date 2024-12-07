using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using BoothDownloader.Web;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader;

internal static class BoothDownloader
{
    private static readonly string FormattedVersion = 
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Major}." +
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Minor}." +
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Build}";
    
    internal static readonly string UserAgent = $"BoothDownloader/{FormattedVersion} (+{GithubUrl})";
    
    private const string GithubUrl = "https://github.com/Myrkie/BoothDownloader";
    private static readonly string Version = $"Booth Downloader - V{FormattedVersion}";
    private static async Task<int> Main(string[] args)
    {
        Console.Title = Version;
        LoggerHelper.GlobalLogger.LogInformation("{versionString}", Version);
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        
#if WINDOWS_BUILD
        args = BoothDownloaderProtocol.HandleProtocol(args);
#endif
        var rootCommand = new RootCommand("Booth Downloader");

        var configOption = new Option<string>(
            name: "--config",
            description: "Path to configuration file",
            getDefaultValue: () => BoothConfig.DefaultPath
        );

        var boothOption = new Option<string?>(
            name: "--booth",
            description: "Booth IDs/URLs/Collections"
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

        var debugOption = new Option<bool>(
            name: "--debug",
            description: "Run in debug mode",
            getDefaultValue: () => false
        );

        var registerOption = new Option<bool>(
            name: "--register",
            description: "Register the booth downloader protocol. Application will close after completed.",
            getDefaultValue: () => false
        );

        var unregisterOption = new Option<bool>(
            name: "--unregister",
            description: "Unregister the booth downloader protocol. Application will close after completed.",
            getDefaultValue: () => false
        );

        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddOption(boothOption);
        rootCommand.AddOption(outputDirectoryOption);
        rootCommand.AddOption(maxRetriesOption);
        rootCommand.AddOption(debugOption);
        rootCommand.AddOption(registerOption);
        rootCommand.AddOption(unregisterOption);

        var cancellationTokenValueSource = new CancellationTokenValueSource();

        rootCommand.SetHandler(async (registerProtocol, unregisterProtocol, configFile, boothInput, outputDirectory, maxRetries, debug, cancellationToken) =>
        {
            if (debug)
            {
                LoggerHelper.GlobalLogger.LogInformation("Arguements:\n{args}", string.Join('\n', args));
            }

            if (registerProtocol)
            {
                BoothDownloaderProtocol.RegisterContext();
                return;
            }

            if (unregisterProtocol)
            {
                BoothDownloaderProtocol.UnregisterContext();
                return;
            }

            BoothConfig.Setup(configFile);

            #region First Boot

            if (string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie))
            {
                Console.WriteLine("Please paste in your cookie from browser.\n");
                var cookie = Console.ReadLine();
                BoothConfig.Instance.Cookie = string.IsNullOrWhiteSpace(cookie) ? BoothConfig.AnonymousCookie : cookie;
                BoothConfig.ConfigInstance.Save();
                LoggerHelper.GlobalLogger.LogInformation("Cookie set");
            }

            #endregion

            #region Prep Booth Client

            await BoothHttpClientManager.Setup(cancellationToken);

            #endregion

            if (string.IsNullOrEmpty(boothInput))
            {
                Console.WriteLine("Enter the Booth ID or URL\nOr one of the following collections: owned, library, gifts");
                Console.Write("> ");
                boothInput = Console.ReadLine();
            }

            var commands = boothInput?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            var isLibraryPage = false;
            var isGiftPage = false;
            var boothIds = new List<string>();

            if(commands != null && commands.Length > 0)
            {
                foreach (var command in commands)
                {
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        continue;
                    }
                    else if (command.Equals("https://accounts.booth.pm/library", StringComparison.OrdinalIgnoreCase) == true
                     || command.Equals("library", StringComparison.OrdinalIgnoreCase) == true
                     || command.Equals("libraries", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        isLibraryPage = true;
                    }
                    else if (command.Equals("https://accounts.booth.pm/library/gifts", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("gift", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("gifts", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        isGiftPage = true;
                    }
                    else if (command.Equals("https://accounts.booth.pm/orders", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("orders", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("order", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("purchase", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("purchases", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        LoggerHelper.GlobalLogger.LogInformation("Orders Page now uses Library!");
                        isLibraryPage = true;
                    }
                    else if (command.Equals("own", StringComparison.OrdinalIgnoreCase) == true
                          || command.Equals("owned", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        isLibraryPage = true;
                        isGiftPage = true;
                    }
                    else
                    {
                        var boothId = RegexStore.IdRegex.Matches(command).Select(x => x.Groups[1].Value).Distinct();
                        if (!boothId.Any())
                        {
                            LoggerHelper.GlobalLogger.LogError("Could not parse booth IDs, {command}", command);
                        }
                        boothIds.AddRange(boothId);
                    }
                }
            }

            Dictionary<string, BoothItemAssets> items = [];

            if (isLibraryPage || isGiftPage)
            {
                if (BoothHttpClientManager.IsAnonymous)
                {
                    LoggerHelper.GlobalLogger.LogError("Cannot download Paid Items with invalid cookie.");
                    LoggerHelper.GlobalLogger.LogInformation("Continuing in 5 seconds...");
                    Thread.Sleep(5000);
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
                LoggerHelper.GlobalLogger.LogInformation("Grabbing the following booth Ids: {boothIds}", string.Join(';', boothIds));

                items = await BoothPageParser.GetItemsAsync(boothIds, items, isGiftPage, cancellationToken: cancellationToken);
            }

            if (items.Count > 0)
            {
                await BoothBatchDownloader.DownloadAsync(items, outputDirectory, maxRetries, debug, cancellationToken);
            }
            else
            {
                LoggerHelper.GlobalLogger.LogError("No items found to download, exiting in 5 seconds");
                Thread.Sleep(5000);
            }
        }, registerOption, unregisterOption, configOption, boothOption, outputDirectoryOption, maxRetriesOption, debugOption, cancellationTokenValueSource);

        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        var built = commandLineBuilder.Build();
        return await built.InvokeAsync(args);
    }
}