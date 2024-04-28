using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using BoothDownloader.config;
using BoothDownloader.misc;
using BoothDownloader.web;

namespace BoothDownloader;

internal static class BoothDownloader
{
    internal static JsonConfig? _configextern;
    
    private const string Resized = "base_resized";

    private static readonly Regex ImageRegex =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg|png)", RegexOptions.Compiled);

    private static readonly Regex ImageRegexGif =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)", RegexOptions.Compiled);

    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)", RegexOptions.Compiled);

    private static readonly Regex GuidRegex = new(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)", RegexOptions.Compiled);

    private static readonly Regex ItemRegex = new(@"booth\.pm\/items\/(\d+)", RegexOptions.Compiled);

    private static readonly Regex DownloadRegex = new(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}", RegexOptions.Compiled);

    private static readonly Regex DownloadNameRegex = new(@".*\/(.*)\?", RegexOptions.Compiled);

    private static readonly Regex OrdersRegex = new(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}", RegexOptions.Compiled);

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

        var maxRetriesOption = new Option<int>(
            name: "--max-retries",
            description: "maximum retries for downloading binary files",
            getDefaultValue: () => 3
        );
        
        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddOption(boothOption);
        rootCommand.AddOption(outputDirectoryOption);
        rootCommand.AddOption(maxRetriesOption);

        rootCommand.SetHandler((configFile, boothId, outputDirectory, maxRetries) =>
        {
            var config = new JsonConfig(configFile);
            _configextern = config;
            
            #region First Boot

            if (config.Config.FirstBoot)
            {
                Console.WriteLine("Please paste in your cookie from browser.\n");
                var cookie = Console.ReadLine();
                config.Config.Cookie = cookie!;
                config.Config.FirstBoot = false;
                config.Save();
                Console.WriteLine("Cookie set!\n");
            }

            #endregion
            
            
            var idFromArgument = true;
            if (boothId == null)
            {
                idFromArgument = false;
                Console.WriteLine("Enter the Booth ID or URL: ");
                boothId = Console.ReadLine();
            }   
            
            #region Prep Booth Client

            var client = new BoothClient(config.Config);
            var hasValidCookie = client.IsCookieValid();

            if (hasValidCookie)
            {
                Console.WriteLine("Cookie is valid! - file downloads will function.\n");
            }
            else
            {
                Console.WriteLine(
                    "Cookie is not valid file downloads will not function!\nImage downloads will still function\nUpdate your cookie in the config file.\n"
                );
                config.Config.Cookie = "";
                config.Save();
            }

            #endregion

            if (boothId == "https://accounts.booth.pm/orders" | boothId?.ToLower() == "orders")
            {
                if (hasValidCookie)
                {
                    Console.WriteLine("Downloading all Paid Orders!\n");
                    var list = BoothOrders.Ordersloop();
                    Console.WriteLine($"Orders to download: {list.Count}\nthis may be more than expected as this doesnt account for invalid or deleted items\n");

                    foreach (var items in list)
                    {
                        Console.WriteLine($"Downloading {items.Id}\n");
                        mainparsing(items.Id, outputDirectory, idFromArgument, config, client, hasValidCookie, maxRetries);
                    }
                }
                else Console.WriteLine("Cannot download paid orders with invalid cookie.\n"); Thread.Sleep(1500);
            }else mainparsing(boothId, outputDirectory, idFromArgument, config, client, hasValidCookie, maxRetries);
            
            
        }, configOption, boothOption, outputDirectoryOption, maxRetriesOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static void mainparsing(string? boothId, string outputDirectory, bool idFromArgument, JsonConfig config, BoothClient client, bool hasValidCookie, int maxRetries)
    {
        #region Prep Booth ID

        boothId = IdRegex.Match(boothId!)
            .Value;

        #endregion
        
        Console.WriteLine($"max-retries set to {maxRetries}");
        Console.WriteLine($"requested booth id {boothId}");

        #region Prep Folders

        if (boothId == "")
        {
            Console.WriteLine("Booth ID is invalid"); 
            return;
        }
        var outputDir = Directory.CreateDirectory(outputDirectory);
        if (Directory.Exists(outputDir + "/" + boothId))
        {
            Console.WriteLine("Directory already exists. Deleting...");
            Directory.Delete(outputDir + "/" + boothId,
                true);
        }

        var entryDir = Directory.CreateDirectory(outputDir + "/" + boothId);
        var binaryDir = Directory.CreateDirectory(entryDir + "/" + "Binary");

        #endregion
        
        #region Parse Booth Item Page

        string html;
        try
        {
            html = client.GetItemPage(boothId);
        }
        catch (System.Net.WebException webException)
        {
            Console.WriteLine($"A Web-exception was thrown and will be ignored for this item, Does the page still exist?\n {webException}");
            Directory.Delete(binaryDir + "/" + boothId, true);
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An unexpected exception occurred\n {e}");
            return; 
        }

        var imageCollection = ImageRegex.Matches(html)
            .Select(match => match.Value)
            .Where(url =>
                // We only care for Resized Images to reduce download time and disk space
                GuidRegex.Match(url)
                    .ToString()
                    .Split('/')
                    .Last()
                    .Contains(Resized)
            );
        var gifCollection = ImageRegexGif.Matches(html)
            .Select(match => match.Value)
            .ToArray();
        var downloadCollection = hasValidCookie
            ? DownloadRegex.Matches(html)
                .Select(match => match.Value)
                .ToArray()
            : Array.Empty<string>();
        var ordersCollection = hasValidCookie
            ? OrdersRegex.Matches(html)
                .Select(match => match.Value)
                .ToArray()
            : Array.Empty<string>();

        // Thread safe container for collecting download urls
        var downloadBag = new ConcurrentBag<string>(downloadCollection);

        #endregion

        #region Parse Booth Order Pages

        try
        {
            if (ordersCollection.Length > 0)
            {
                Task.WaitAll(ordersCollection.Select(url => Task.Factory.StartNew(() =>
                    {
                        using var webClient = client.MakeWebClient();
                        Console.WriteLine("starting on thread: {0}",
                            Environment.CurrentManagedThreadId);
                        Console.WriteLine("starting to grab order downloads: {0}",
                            url);
                        var orderHtml = webClient.DownloadString(url);

                        // Class seperates the next item in the order list
                        var splitByItem = orderHtml.Split("\"u-d-flex\"");

                        foreach(var itemHtml in splitByItem)
                        {
                            var itemMatch = ItemRegex.Match(itemHtml);

                            if (itemMatch?.Groups[1].Value == boothId.ToString())
                            {
                                foreach (var downloadUrl in DownloadRegex.Matches(itemHtml)
                                     .Select(match => match.Value))
                                {
                                    downloadBag.Add(downloadUrl);
                                }
                            }
                        }

                        Console.WriteLine("finished grabbing: {0}",
                            url);
                        Console.WriteLine("finished grabbing on thread: {0}",
                            Environment.CurrentManagedThreadId);
                    }))
                    .ToArray());
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            StringBuilder sb = new StringBuilder();
            sb.Append("exception occured in order downloader");
            sb.Append("dumping orders collection " + ordersCollection);
            sb.Append("dumping orders urls downloadBag" + downloadBag);
            Console.WriteLine(sb);
            throw new BoothClient.DownloadFailedException();
        }

        #endregion

        #region Download Processing

        var imageTasks = imageCollection.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}",
                    Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}",
                    url);
                var name = GuidRegex.Match(url)
                    .ToString()
                    .Split('/')
                    .Last();
                webClient.DownloadFile(url,
                    entryDir + "/" + name);
                Console.WriteLine("finished downloading: {0}",
                    url);
                Console.WriteLine("finished downloading on thread: {0}",
                    Environment.CurrentManagedThreadId);
            }))
            .ToArray();

        var gifTasks = gifCollection.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}",
                    Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}",
                    url);
                var name = GuidRegex.Match(url)
                    .ToString()
                    .Split('/')
                    .Last();
                webClient.DownloadFile(url,
                    entryDir + "/" + name);
                Console.WriteLine("finished downloading: {0}",
                    url);
                Console.WriteLine("finished downloading on thread: {0}",
                    Environment.CurrentManagedThreadId);
            }))
            .ToArray();

        var downloadTasks = downloadBag.Select(url => Task.Factory.StartNew(() =>
            {
                using var webClient = client.MakeWebClient();
                Console.WriteLine("starting on thread: {0}",
                    Environment.CurrentManagedThreadId);
                Console.WriteLine("starting to download: {0}",
                    url);
                var httpClient = client.MakeHttpClient();
                var resp = httpClient.GetAsync(url)
                    .GetAwaiter()
                    .GetResult();
                var redirectUrl = resp.Headers.Location;
                if (redirectUrl != null)
                {
                    var filename = DownloadNameRegex.Match(redirectUrl.ToString())
                        .Groups[1]
                        .Value;
                    
                    var newFilename = Utils.GetUniqueFilename(binaryDir.ToString(), filename);

                    // network conditions can lead to this being a requirement.
                    // eat a d*ck spectrum
                    bool downloadSuccess = false;
                    int retryCount = 0;
                    while (!downloadSuccess && retryCount < maxRetries)
                    {
                        try
                        { 
                            webClient.DownloadFile(redirectUrl, binaryDir + "/" + newFilename);
                            downloadSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            Console.WriteLine($"Failed to download {url}. Retry attempt {retryCount}/{maxRetries}. Error: {ex.Message}");
                        }
                    }

                    if (!downloadSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed to download {url} after {maxRetries} attempts.");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine("finished downloading: {0}",
                    url);
                Console.WriteLine("finished downloading on thread: {0}",
                    Environment.CurrentManagedThreadId);
            }))
            .ToArray();

        if (imageTasks.Length > 0)
        {
            Task.WaitAll(imageTasks);
        }
        else
            Console.WriteLine("No images found skipping downloader.");

        if (gifTasks.Length > 0)
        {
            Task.WaitAll(gifTasks);
        }
        else
            Console.WriteLine("No gifs found skipping downloader.");

        if (downloadTasks.Length > 0)
        {
            Task.WaitAll(downloadTasks);
        }
        else
            Console.WriteLine("No downloads found skipping downloader.");

        #endregion

        #region Compression

        Thread.Sleep(1500);

        if (config.Config.AutoZip)
        {
            Console.WriteLine("Zipping!");
            if (File.Exists(entryDir + ".zip"))
            {
                Console.WriteLine("File already exists. Deleting...");
                File.Delete(entryDir + ".zip");
            }

            ZipFile.CreateFromDirectory(entryDir.ToString(),
                entryDir + ".zip");
            Directory.Delete(entryDir.ToString(),
                true);
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
    }
}