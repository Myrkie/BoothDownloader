using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BoothDownloader.booth;
using BoothDownloader.config;
using BoothDownloader.log;
using BoothDownloader.web;
using Microsoft.Extensions.Logging;

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
            var logger = Log.Factory.CreateLogger("BaseCommand");

            var config = new JsonConfig(configFile);

            #region First Boot

            if (config.Config.FirstBoot)
            {
                Console.WriteLine("Please paste in your cookie from browser.");
                var cookie = Console.ReadLine();
                config.Config.Cookie = cookie!;
                config.Config.FirstBoot = false;
                config.Save();
                logger.Log(LogLevel.Information, "Cookie Set");
            }

            #endregion

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

            #region Prep Folders

            var outputDir = Directory.CreateDirectory(outputDirectory);
            if (Directory.Exists(outputDir + "/" + boothId))
            {
                logger.Log(LogLevel.Information, "Directory already exists. Deleting...");
                Directory.Delete(outputDir + "/" + boothId, true);
            }

            var entryDir = Directory.CreateDirectory(outputDir + "/" + boothId);
            var binaryDir = Directory.CreateDirectory(entryDir + "/" + "Binary");

            #endregion

            #region Prep Booth Client

            var client = new BoothClient(config.Config);
            var hasValidCookie = client.IsCookieValid();

            if (hasValidCookie)
            {
                logger.Log(LogLevel.Information, "Cookie is valid! - file downloads will function.");
            }
            else
            {
                logger.Log(LogLevel.Warning,
                    "Cookie is not valid file downloads will not function! Image downloads will still function. Update your cookie in the config file.");
                config.Config.Cookie = "";
                config.Save();
            }

            #endregion

            #region Parse Booth Item Page

            var boothItemPage = new BoothItemPage(client, boothId);

            #endregion

            #region Start Image Downloads

            var imageTasks = boothItemPage.ResizedImages.Distinct()
                .Select(url => client.StartDownloadImageTask(url, entryDir)).ToArray();
            var gifTasks = boothItemPage.Gifs.Distinct().Select(url => client.StartDownloadImageTask(url, entryDir))
                .ToArray();

            #endregion

            #region Parse Downloads

            var downloadCollection = hasValidCookie
                ? boothItemPage.Downloads.ToArray()
                : Array.Empty<string>();
            var ordersCollection = hasValidCookie
                ? boothItemPage.OrderIds.ToArray()
                : Array.Empty<string>();

            // Thread safe container for collecting download urls
            var downloadBag = new ConcurrentBag<string>(downloadCollection);

            if (ordersCollection.Length > 0)
            {
                var boothOrderPages =
                    ordersCollection.Distinct().Select(orderId => new BoothOrderPage(client, orderId));
                foreach (var boothOrderPage in boothOrderPages)
                {
                    foreach (var downloadUrl in boothOrderPage.ItemDownloads[boothId])
                    {
                        downloadBag.Add(downloadUrl);
                    }
                }
            }

            #endregion

            #region Download Processing

            var downloadTasks = downloadBag.Distinct().Select(url => client.StartDownloadBinaryTask(url, binaryDir))
                .ToArray();

            if (imageTasks.Length > 0)
            {
                Task.WaitAll(imageTasks);
                logger.Log(LogLevel.Information, "All image downloads completed.");
            }
            else logger.Log(LogLevel.Information, "No images found. Skipping downloader.");

            if (gifTasks.Length > 0)
            {
                Task.WaitAll(gifTasks);
                logger.Log(LogLevel.Information, "All gif downloads completed.");
            }
            else logger.Log(LogLevel.Information, "No gifs found. Skipping downloader.");

            if (downloadTasks.Length > 0)
            {
                Task.WaitAll(downloadTasks);
                logger.Log(LogLevel.Information, "All file downloads completed.");
            }
            else logger.Log(LogLevel.Information, "No file downloads found. Skipping downloader.");

            #endregion

            #region Compression

            Thread.Sleep(1500);

            if (config.Config.AutoZip)
            {
                if (File.Exists(entryDir + ".zip"))
                {
                    logger.Log(LogLevel.Information, "File already exists. Deleting...");
                    File.Delete(entryDir + ".zip");
                }

                ZipFile.CreateFromDirectory(entryDir.ToString(), entryDir + ".zip");
                Directory.Delete(entryDir.ToString(), true);
                logger.Log(LogLevel.Information, "Zipped!");
            }

            #endregion

            #region Exit Successfully

            logger.Log(LogLevel.Information, "Done!");

            if (idFromArgument && config.Config.AutoZip)
            {
                // used for standard output redirection for path to zip file with another process
                Console.WriteLine("ENVFilePATH: " + entryDir + ".zip");
            }

            #endregion
        }, _configOption, _boothOption, _outputDirectoryOption);
    }
}