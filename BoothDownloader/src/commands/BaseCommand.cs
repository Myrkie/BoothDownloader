using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BoothDownloader.booth;
using BoothDownloader.config;
using BoothDownloader.web;

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
        AddGlobalOption(_configOption);
        AddOption(_boothOption);
        AddOption(_outputDirectoryOption);

        this.SetHandler((configFile, boothId, outputDirectory) =>
        {
            var config = new JsonConfig(configFile);

            #region First Boot

            if (config.Config.FirstBoot)
            {
                Console.WriteLine("Please paste in your cookie from browser.");
                var cookie = Console.ReadLine();
                config.Config.Cookie = cookie!;
                config.Config.FirstBoot = false;
                config.Save();
                Console.WriteLine("Cookie set!");
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
                Console.WriteLine("Directory already exists. Deleting...");
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
                Console.WriteLine("Cookie is valid! - file downloads will function.");
            }
            else
            {
                Console.WriteLine(
                    "Cookie is not valid file downloads will not function!\nImage downloads will still function\nUpdate your cookie in the config file."
                );
                config.Config.Cookie = "";
                config.Save();
            }

            #endregion

            #region Parse Booth Item Page

            var boothItemPage = new BoothItemPage(client, boothId);

            var imageCollection = boothItemPage.ResizedImages.ToArray();
            var gifCollection = boothItemPage.Gifs.ToArray();
            var downloadCollection = hasValidCookie
                ? boothItemPage.Downloads.ToArray()
                : Array.Empty<string>();
            var ordersCollection = hasValidCookie
                ? boothItemPage.OrderIds.ToArray()
                : Array.Empty<string>();

            // Thread safe container for collecting download urls
            var downloadBag = new ConcurrentBag<string>(downloadCollection);

            #endregion

            #region Parse Booth Order Pages

            if (ordersCollection.Length > 0)
            {
                var boothOrderPages = ordersCollection.Select(orderId => new BoothOrderPage(client, orderId));
                foreach (var boothOrderPage in boothOrderPages)
                {
                    foreach (var downloadUrl in boothOrderPage.Downloads)
                    {
                        downloadBag.Add(downloadUrl);
                    }
                }
            }

            #endregion

            #region Download Processing

            var imageTasks = imageCollection.Select(url => client.StartDownloadImageTask(url, entryDir)).ToArray();
            var gifTasks = gifCollection.Select(url => client.StartDownloadImageTask(url, entryDir)).ToArray();
            var downloadTasks = downloadBag.Select(url => client.StartDownloadBinaryTask(url, binaryDir)).ToArray();

            if (imageTasks.Length > 0)
            {
                Task.WaitAll(imageTasks);
            }
            else Console.WriteLine("No images found skipping downloader.");

            if (gifTasks.Length > 0)
            {
                Task.WaitAll(gifTasks);
            }
            else Console.WriteLine("No gifs found skipping downloader.");

            if (downloadTasks.Length > 0)
            {
                Task.WaitAll(downloadTasks);
            }
            else Console.WriteLine("No downloads found skipping downloader.");

            #endregion

            #region Compression

            Thread.Sleep(1500);

            if (config.Config.AutoZip)
            {
                if (File.Exists(entryDir + ".zip"))
                {
                    Console.WriteLine("File already exists. Deleting...");
                    File.Delete(entryDir + ".zip");
                }

                ZipFile.CreateFromDirectory(entryDir.ToString(), entryDir + ".zip");
                Directory.Delete(entryDir.ToString(), true);
                Console.WriteLine("Zipped!");
            }

            #endregion

            #region Exit Successfully

            Console.WriteLine("Done!");

            if (idFromArgument && config.Config.AutoZip)
            {
                // used for standard output redirection for path to zip file with another process
                Console.WriteLine("ENVFilePATH: " + entryDir + ".zip");
            }

            #endregion
        }, _configOption, _boothOption, _outputDirectoryOption);
    }
}