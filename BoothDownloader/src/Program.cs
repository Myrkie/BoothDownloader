using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using BoothDownloader.config;
using BoothDownloader.misc;
using BoothDownloader.src.misc;
using BoothDownloader.web;
using ShellProgressBar;

namespace BoothDownloader;

internal static partial class BoothDownloader
{

    internal static JsonConfig? Configextern;

    private static readonly Regex ImageRegex = GetImageRegex();

    private static readonly Regex ImageGifRegex = GetImageGifRegex();

    private static readonly Regex IdRegex = GetIdRegex();

    private static readonly Regex GuidRegex = GetGuidRegex();

    private static readonly Regex ItemRegex = GetItemRegex();

    private static readonly Regex DownloadRegex = GetDownloadRegex();

    private static readonly Regex DownloadNameRegex = GetDownloadNameRegex();

    private static readonly Regex OrdersRegex = GetOrdersRegex();

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

        var cancellationTokenValueSource = new CancellationTokenValueSource();

        rootCommand.SetHandler(async (configFile, boothId, outputDirectory, maxRetries, cancellationToken) =>
        {
            var config = new JsonConfig(configFile);
            Configextern = config;
            
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
            var hasValidCookie = await client.IsCookieValidAsync(cancellationToken);

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
                        await Mainparsing(items.Id, outputDirectory, idFromArgument, config, client, hasValidCookie, maxRetries, cancellationToken);
                    }
                }
                else
                {
                    Console.WriteLine("Cannot download paid orders with invalid cookie.\n");
                    await Task.Delay(1500, cancellationToken);
                }
            }
            else
            {
                await Mainparsing(boothId, outputDirectory, idFromArgument, config, client, hasValidCookie, maxRetries, cancellationToken);
            }
        }, configOption, boothOption, outputDirectoryOption, maxRetriesOption, cancellationTokenValueSource);

        var clb = new CommandLineBuilder(rootCommand);
        clb.CancelOnProcessTermination();
        var built = clb.Build();
        return await built.InvokeAsync(args);
    }

    private static async Task Mainparsing(string? boothId, string outputDirectory, bool idFromArgument, JsonConfig config, BoothClient client, bool hasValidCookie, int maxRetries, CancellationToken cancellationToken = default)
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
        if (Directory.Exists(Path.Combine(outputDir.ToString(), boothId)))
        {
            Console.WriteLine("Directory already exists. Deleting...");
            Directory.Delete(Path.Combine(outputDir.ToString(), boothId),
                true);
        }

        var entryDir = Directory.CreateDirectory(Path.Combine(outputDir.ToString(), boothId));
        var binaryDir = Directory.CreateDirectory(Path.Combine(entryDir.ToString(), "Binary"));

        #endregion
        
        #region Parse Booth Item Page

        string html;
        try
        {
            html = await client.GetItemPageAsync(boothId, cancellationToken);
        }
        catch (System.Net.WebException webException)
        {
            Console.WriteLine($"A Web-exception was thrown and will be ignored for this item, Does the page still exist?\n {webException}");
            Directory.Delete(Path.Combine(binaryDir.ToString(), boothId), true);
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
                    .Contains("base_resized")
            ).ToArray();
        var gifCollection = ImageGifRegex.Matches(html)
            .Select(match => match.Value)
            .ToArray();
        var downloadCollection = hasValidCookie
            ? DownloadRegex.Matches(html)
                .Select(match => match.Value)
                .ToArray()
            : [];
        var ordersCollection = hasValidCookie
            ? OrdersRegex.Matches(html)
                .Select(match => match.Value)
                .ToArray()
            : [];

        // Thread safe container for collecting download urls
        var downloadBag = new ConcurrentBag<string>(downloadCollection);

        #endregion

        #region Parse Booth Order Pages

        try
        {
            if (ordersCollection.Length > 0)
            {
                Task.WaitAll(ordersCollection.Select(url => Task.Factory.StartNew(async () =>
                    {
                        var httpClient = client.MakeHttpClient();
                        Console.WriteLine("Building download collection for url: {0}", url);
                        var orderResponse = httpClient.GetAsync(url, cancellationToken);
                        var orderHtml = await orderResponse.Result.Content.ReadAsStringAsync(cancellationToken);

                        // Class separates the next item in the order list
                        var splitByItem = orderHtml.Split("\"u-d-flex\"");

                        foreach(var itemHtml in splitByItem)
                        {
                            var itemMatch = ItemRegex.Match(itemHtml);

                            if (itemMatch.Groups[1].Value != boothId) continue;
                            foreach (var downloadUrl in DownloadRegex.Matches(itemHtml)
                                         .Select(match => match.Value))
                            {
                                downloadBag.Add(downloadUrl);
                            }
                        }

                        Console.WriteLine("Finished building download collection for url: {0}", url);
                    }))
                    .ToArray(), cancellationToken);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            StringBuilder sb = new();
            sb.Append("Exception occured in order downloader.");
            sb.Append("Dumping orders collection: " + ordersCollection);
            sb.Append("Dumping orders urls downloadBag: " + downloadBag);
            Console.WriteLine(sb);
            throw new BoothClient.DownloadFailedException();
        }

        #endregion

        #region Download Processing
        var combinedCollection = imageCollection
            .Concat(gifCollection)
            .Concat(downloadBag)
            .ToList();
        
        var options = new ProgressBarOptions
        {
            BackgroundColor = ConsoleColor.White,
            ForegroundColor = ConsoleColor.Yellow,
            ProgressCharacter = '─',
            CollapseWhenFinished = false
        };
        var parentOptions = new ProgressBarOptions
        {
            BackgroundColor = ConsoleColor.White,
            ForegroundColor = ConsoleColor.Blue,
            ProgressCharacter = '─',
            CollapseWhenFinished = false
        };
        var childOptions = new ProgressBarOptions
        {
            BackgroundColor = ConsoleColor.White,
            ForegroundColor = ConsoleColor.Green,
            ProgressCharacter = '─',
            CollapseWhenFinished = true
        };
        
        var progressBar = new ProgressBar(combinedCollection.Count, "Overall Progress", options);
        
        var imageTaskMessage = "ImageTasks";
        int remainingImageDownloads = imageCollection.Length;
        var imageTaskBar = progressBar.Spawn(imageCollection.Length, imageTaskMessage, parentOptions);
        var imageTasks = imageCollection.Select(url => Task.Run(async () =>
        {
            var name = GuidRegex.Match(url).ToString().Split('/').Last();
            var child = imageTaskBar.Spawn(10000, name, childOptions);
            var childProgress = new ChildProgressBarProgress(child);
            
            await Utils.DownloadFileAsync(url, Path.Combine(entryDir.ToString(), name), childProgress, cancellationToken);
            
            imageTaskBar.Tick();
            progressBar.Tick();
            
            Interlocked.Decrement(ref remainingImageDownloads);
            imageTaskBar.Message = $"ImageTasks ({remainingImageDownloads} remaining)";
        })).ToArray();
        
        var gifTaskMessage = "GifTasks";
        int remainingGifDownloads = gifCollection.Length;
        var gifTaskBar = progressBar.Spawn(gifCollection.Length, gifTaskMessage, parentOptions);
        var gifTasks = gifCollection.Select(url => Task.Run(async () =>
        {
            var name = GuidRegex.Match(url).ToString().Split('/').Last();
            var child = gifTaskBar.Spawn(10000, name, childOptions);
            var childProgress = new ChildProgressBarProgress(child);
            
            await Utils.DownloadFileAsync(url, Path.Combine(entryDir.ToString(), name), childProgress, cancellationToken);
            
            gifTaskBar.Tick();
            progressBar.Tick();
            
            Interlocked.Decrement(ref remainingGifDownloads);
            gifTaskBar.Message = $"GifTasks ({remainingGifDownloads} remaining)";
        })).ToArray();


        var downloadTaskMessage = "DownloadTasks";
        int remainingDownloads = downloadBag.Count;
        var downloadTaskBar = progressBar.Spawn(downloadBag.Count, downloadTaskMessage, parentOptions);
        var downloadTasks = downloadBag.Select(url => Task.Run(async () =>
        {
            var httpClient = client.MakeHttpClient();
            var resp = await httpClient.GetAsync(url, cancellationToken);
            var redirectUrl = resp.Headers.Location!.ToString();
            var filename = DownloadNameRegex.Match(redirectUrl).Groups[1].Value;

            // @TODO:
            // This needs to be refactored. Since utilizing more async methods, the unique filename generation needs to be done in a more async friendly way.
            // Probably storing the list of filenames in a concurrent bag in app along with checking in directory.
            var uniqueFilename = Utils.GetUniqueFilename(binaryDir.ToString(), filename);

            var success = false;
            var retryCount = 0;
            var child = downloadTaskBar.Spawn(10000, uniqueFilename, childOptions);
            var childProgress = new ChildProgressBarProgress(child);
            while (!success && retryCount < maxRetries)
            {
                try
                {
                    child.Message = uniqueFilename;
                    await Utils.DownloadFileAsync(redirectUrl, Path.Combine(binaryDir.ToString(), uniqueFilename), childProgress, cancellationToken);
                    Interlocked.Decrement(ref remainingDownloads);
                    downloadTaskBar.Message = $"DownloadTasks ({remainingDownloads} remaining)";
                    success = true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    child.Message = $"Failed to download {url}. Retry attempt {retryCount}/{maxRetries}. Error: {ex.Message}";
                     await Task.Delay(5000, cancellationToken);
                }
            }
            if (retryCount < maxRetries)
            {
                success = false;
            }
            
            downloadTaskBar.Tick();
            progressBar.Tick();
            
            if (success) return;
            
            Console.ForegroundColor = ConsoleColor.Red;
            child.Message = $"Failed to download {url} after {maxRetries} attempts.";
            Console.ResetColor();
        })).ToArray();

        if(imageTasks.Length == 0)
            Console.WriteLine("No images found skipping downloader.");

        if(gifTasks.Length == 0)
            Console.WriteLine("No gifs found skipping downloader.");

        if(downloadTasks.Length == 0)
            Console.WriteLine("No downloads found skipping downloader.");

        var allTasks = imageTasks.Concat(gifTasks).Concat(downloadTasks).ToArray();
        await Task.WhenAll(allTasks);

        #endregion

        #region Compression

        if (config.Config.AutoZip)
        {
            
            progressBar.Dispose();
            if (File.Exists(entryDir + ".zip"))
            {
                Console.WriteLine("File already exists. Deleting...");
                File.Delete(entryDir + ".zip");
            }
            
            Console.WriteLine("Zipping!");
            ZipFile.CreateFromDirectory(entryDir.ToString(),entryDir + ".zip");
            Console.WriteLine("Zipped!");
            
            Directory.Delete(entryDir.ToString(), true);
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

    [GeneratedRegex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg|png)", RegexOptions.Compiled)]
    private static partial Regex GetImageRegex();

    [GeneratedRegex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)", RegexOptions.Compiled)]
    private static partial Regex GetImageGifRegex();

    [GeneratedRegex(@"[^/]+(?=/$|$)", RegexOptions.Compiled)]
    private static partial Regex GetIdRegex();

    [GeneratedRegex(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)", RegexOptions.Compiled)]
    private static partial Regex GetGuidRegex();

    [GeneratedRegex(@"booth\.pm\/items\/(\d+)", RegexOptions.Compiled)]
    private static partial Regex GetItemRegex();

    [GeneratedRegex(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}", RegexOptions.Compiled)]
    private static partial Regex GetDownloadRegex();

    [GeneratedRegex(@".*\/(.*)\?", RegexOptions.Compiled)]
    private static partial Regex GetDownloadNameRegex();

    [GeneratedRegex(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}", RegexOptions.Compiled)]
    private static partial Regex GetOrdersRegex();
}