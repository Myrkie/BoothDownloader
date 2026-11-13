using System.CommandLine;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.Cli;

internal sealed class BoothDownloaderOptions
{
    public string ConfigFile { get; init; } = Configuration.BoothConfig.DefaultPath;
    public string? BoothInput { get; init; }
    public string OutputDirectory { get; init; } = "./BoothDownloaderOut";
    public int MaxRetries { get; init; } = 3;
    public bool Debug { get; init; }
    public bool Force { get; init; }
    public bool RegisterProtocol { get; init; }
    public bool UnregisterProtocol { get; init; }
    private bool About { get; init; }

    public static RootCommand CreateCommand()
    {
        var rootCommand = new RootCommand("Booth Downloader");

        var configOption = new Option<string>("--config")
        {
            Description = "Path to configuration file",
            DefaultValueFactory = _ => Configuration.BoothConfig.DefaultPath
        };

        var boothOption = new Option<string?>("--booth")
        {
            Description = "Booth IDs/URLs/Collections"
        };

        var outputDirectoryOption = new Option<string>("--output-dir")
        {
            Description = "Output Directory",
            DefaultValueFactory = _ => "./BoothDownloaderOut"
        };

        var maxRetriesOption = new Option<int>("--max-retries")
        {
            Description = "Maximum retries for downloading binary files",
            DefaultValueFactory = _ => 3
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Run in debug mode",
            DefaultValueFactory = _ => false
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Force every selected item to be downloaded again. Avoid using this for large libraries.",
            DefaultValueFactory = _ => false
        };

        var registerOption = new Option<bool>("--register")
        {
            Description = "Register the booth downloader protocol. Application will close after completed.",
            DefaultValueFactory = _ => false
        };

        var unregisterOption = new Option<bool>("--unregister")
        {
            Description = "Unregister the booth downloader protocol. Application will close after completed.",
            DefaultValueFactory = _ => false
        };
        
        var aboutOption = new Option<bool>("--about")
        {
            Description = "Show information about Booth Downloader",
            DefaultValueFactory = _ => false
        };

        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(boothOption);
        rootCommand.Options.Add(outputDirectoryOption);
        rootCommand.Options.Add(maxRetriesOption);
        rootCommand.Options.Add(debugOption);
        rootCommand.Options.Add(forceOption);
        rootCommand.Options.Add(registerOption);
        rootCommand.Options.Add(unregisterOption);
        rootCommand.Options.Add(aboutOption);
        

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new BoothDownloaderOptions
            {
                ConfigFile = parseResult.GetValue(configOption),
                BoothInput = parseResult.GetValue(boothOption),
                OutputDirectory = parseResult.GetValue(outputDirectoryOption),
                MaxRetries = parseResult.GetValue(maxRetriesOption),
                Debug = parseResult.GetValue(debugOption),
                Force = parseResult.GetValue(forceOption),
                RegisterProtocol = parseResult.GetValue(registerOption),
                UnregisterProtocol = parseResult.GetValue(unregisterOption),
                About = parseResult.GetValue(aboutOption)
            };

            try
            {
                if (!options.About)
                    return await BoothDownloader.RunAsync(options, cancellationToken);

                await AboutScreen.ShowAsync();
                return 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LoggerHelper.GlobalLogger.LogInformation("Operation cancelled by user");
                return 130;
            }
        });

        return rootCommand;
    }
}