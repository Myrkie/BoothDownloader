using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BoothDownloader.config;
using BoothDownloader.web;

namespace BoothDownloader;

internal static class BoothDownloader
{
    private const string Resized = "base_resized";

    private static readonly Regex ImageRegex =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg|png)");

    private static readonly Regex ImageRegexGif =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)");

    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)");

    private static readonly Regex
        GuidRegex = new(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)");

    private static readonly Regex DownloadRegex = new(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}");

    private static readonly Regex DownloadNameRegex = new(@".*\/(.*)\?");

    private static readonly Regex OrdersRegex = new(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}");

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
            description: "Booth ID/URL"
        );

        var outputDirectoryOption = new Option<string>(
            name: "--output-dir",
            description: "Output Directory",
            getDefaultValue: () => "./BoothDownloaderOut"
        );

        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddOption(boothOption);
        rootCommand.AddOption(outputDirectoryOption);

        rootCommand.SetHandler((configFile, boothId, outputDirectory) =>
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

            var html = client.GetItemPage(boothId);

            var imageCollection = ImageRegex.Matches(html).Select(match => match.Value).Where(url =>
                // We only care for Resized Images to reduce download time and disk space
                GuidRegex.Match(url).ToString().Split('/').Last().Contains(Resized)
            );
            var gifCollection = ImageRegexGif.Matches(html).Select(match => match.Value).ToArray();
            var downloadCollection = hasValidCookie
                ? DownloadRegex.Matches(html).Select(match => match.Value).ToArray()
                : Array.Empty<string>();
            var ordersCollection = hasValidCookie
                ? OrdersRegex.Matches(html).Select(match => match.Value).ToArray()
                : Array.Empty<string>();

            // Thread safe container for collecting download urls
            var downloadBag = new ConcurrentBag<string>(downloadCollection);

            #endregion

            #region Parse Booth Order Pages

            if (ordersCollection.Length > 0)
            {
                Task.WaitAll(ordersCollection.Select(url => Task.Factory.StartNew(() =>
                {
                    using var webClient = client.MakeWebClient();
                    Console.WriteLine("starting on thread: {0}", Environment.CurrentManagedThreadId);
                    Console.WriteLine("starting to grab order downloads: {0}", url);
                    var orderHtml = webClient.DownloadString(url);
                    foreach (var downloadUrl in DownloadRegex.Matches(orderHtml).Select(match => match.Value))
                    {
                        downloadBag.Add(downloadUrl);
                    }

                    Console.WriteLine("finished grabbing: {0}", url);
                    Console.WriteLine("finished grabbing on thread: {0}", Environment.CurrentManagedThreadId);
                })).ToArray());
            }

            #endregion

            #region Download Processing

            var imageTasks = imageCollection.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}", Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}", url);
                var result = webClient.DownloadData(url);
                var name = GuidRegex.Match(url).ToString().Split('/').Last();
                File.WriteAllBytesAsync(entryDir + "/" + name, result);
                Console.WriteLine("finished downloading: {0}", url);
                Console.WriteLine("finished downloading on thread: {0}", Environment.CurrentManagedThreadId);
            })).ToArray();

            var gifTasks = gifCollection.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}", Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}", url);
                var result = webClient.DownloadData(url);
                var name = GuidRegex.Match(url).ToString().Split('/').Last();
                Console.WriteLine("name: " + name);
                File.WriteAllBytesAsync(entryDir + "/" + name, result);
                Console.WriteLine("finished downloading: {0}", url);
                Console.WriteLine("finished downloading on thread: {0}", Environment.CurrentManagedThreadId);
            })).ToArray();

            var downloadTasks = downloadBag.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}", Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}", url);
                var result = webClient.DownloadData(url);
                var filename = DownloadNameRegex.Match(webClient.ResponseUri!.ToString()).Groups[1].Value;
                File.WriteAllBytesAsync(binaryDir + "/" + filename, result);
                Console.WriteLine("finished downloading: {0}", url);
                Console.WriteLine("finished downloading on thread: {0}", Environment.CurrentManagedThreadId);
            })).ToArray();

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
        }, configOption, boothOption, outputDirectoryOption);

        return await rootCommand.InvokeAsync(args);
    }
}