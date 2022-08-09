using System.CommandLine;
using System.Text.RegularExpressions;
using BoothDownloader.config;
using BoothDownloader.processes;

namespace BoothDownloader.commands;

public class BaseCommand : RootCommand
{
    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)");

    private readonly Option<string> _configOption = new(
        name: "--config",
        description: "Path to configuration file",
        getDefaultValue: () => "./BDConfig.json"
    );

    private readonly Option<string?> _boothOption = new(
        name: "--booth",
        description: "Booth ID/URL"
    );

    private readonly Option<string> _outputDirectoryOption = new(
        name: "--output-dir",
        description: "Output Directory",
        getDefaultValue: () => "./BoothDownloaderOut"
    );

    public BaseCommand() : base(description: "Booth Downloader")
    {
        _configOption.AddAlias("-c");
        _boothOption.AddAlias("-b");
        _outputDirectoryOption.AddAlias("-o");

        AddGlobalOption(_configOption);
        AddOption(_boothOption);
        AddOption(_outputDirectoryOption);

        this.SetHandler((configFile, boothId, outputDirectory) =>
        {
            var config = new JsonConfig(configFile);

            InitializationProcess.Run(config);

            #region Prep Booth ID

            var idFromArgument = true;
            if (boothId == null)
            {
                idFromArgument = false;
                Console.WriteLine("Enter the Booth ID or URL: ");
                boothId = Console.ReadLine();
            }

            boothId = IdRegex.Match(boothId!).Value;

            #endregion

            ItemDownloadProcess.Run(config, boothId, outputDirectory, idFromArgument);
        }, _configOption, _boothOption, _outputDirectoryOption);
    }
}