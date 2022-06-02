using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;

namespace BoothDownloader
{
    internal static class BoothDownloader
    {
        private static bool _cookievalid;
        private static string? _html;
        private static string? _boothId;
        private const string Stdurl = "https://booth.pm/en/items/";
        private const string Stdurl2 = "https://accounts.booth.pm/settings";
        private const string Resized = "base_resized";
        private static string? _userinput = "";

        private static Task[] giftasks;
        private static Task[] imagetasks;
        private static Task[] downloadtasks;
        static void Main(string?[] args)
        {
            #region JsonConfig

            Console.Title = $"BoothDownloader - V{typeof(BoothDownloader).Assembly.GetName().Version}";
            JsonConfig.Configure.load();

            #endregion
            
            #region Regexs

            // thanks to https://github.com/Nekromateion for imageRegex
            var imageRegex =
                new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg)");
            // secondary regex if jpg is not found
            var imageRegexPng =
                new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png)");
            // third regex for gifs
            var imageRegexGif =
                new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)");
            // ID Regex
            var idRegex = new Regex(@"[^/]+(?=/$|$)");

            var guidRegex = new Regex(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)");

            var getDlRegex = new Regex(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}");

            var dlNameRegex = new Regex(@".*\/(.*)\?");

            #endregion

            #region ValidationCheck

            if (JsonConfig._config._firstboot)
            {
                Console.WriteLine("Please paste in your cookie from browser.");
                var cookie = Console.ReadLine();
                JsonConfig._config._Cookie = cookie!;
                JsonConfig._config._firstboot = false;
                JsonConfig.Configure.forcesave();
                Console.WriteLine("Cookie set!");
            }

            using var cookieclient = new Webclientsubclass();
            cookieclient.Headers.Add(HttpRequestHeader.Cookie, "_plaza_session_nktz7u=" + JsonConfig._config._Cookie);
            cookieclient.DownloadString(Stdurl2);
            // Checks if cookie is valid by checking if the page is not redirected to login
            if (cookieclient.ResponseUri!.ToString() == Stdurl2)
            {
                Console.WriteLine("Cookie is valid! - file downloads will function.");
                _cookievalid = true;
            }
            else
            {
                Console.WriteLine("Cookie is not valid file downloads will not function!\nImage downloads will still function\nUpdate your cookie in the config file.");
                JsonConfig._config._Cookie = "";
                _cookievalid = false;
                JsonConfig.Configure.forcesave();
            }

            #endregion
            
            #region Argument Check
            

            if (args.Length == 0)
            {
                Console.WriteLine("Enter the Booth ID or URL: ");
                _userinput = Console.ReadLine();
            }
            else
            {
                _userinput = args[0];
            }

            #endregion

            #region String page downloader

            // regex ID from input
            if (_userinput != null) _boothId = idRegex.Match(_userinput).Value;

            // force ID for all download strings for subdomain support
#pragma warning disable SYSLIB0014
            var webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.Cookie, "adult=t");
            if (_cookievalid) { webClient.Headers.Add(HttpRequestHeader.Cookie, "_plaza_session_nktz7u=" + JsonConfig._config._Cookie); }
            _html = webClient.DownloadString(Stdurl + _boothId);
            
#pragma warning restore SYSLIB0014

            #endregion

            #region Collections

            var imageCollection = imageRegex.Matches(_html);
            var downloadCollection = getDlRegex.Matches(_html);
            var gifCollection = imageRegexGif.Matches(_html);

            var downloadables = new HashSet<string>();
            var images = new HashSet<string>();
            var gifs = new HashSet<string>();

            // create image collection
            if (imageCollection.Count == 0)
            {
                Console.WriteLine("No images found. trying with regex png...");
                imageCollection = imageRegexPng.Matches(_html);
            }

            // create download collection
            if (downloadCollection.Count == 0)
            {
                Console.WriteLine("No downloadables found. skipping login...");
            }

            if (Directory.Exists(_boothId))
            {
                Console.WriteLine("Directory already exists. Deleting...");
                Directory.Delete(_boothId, true);
            }

            Console.WriteLine("Downloading ID: " + _boothId);

            // create download hashset
            if (_cookievalid)
            {
                foreach (Match downloadurl in downloadCollection)
                {
                    downloadables.Add(downloadurl.Value);
                }
            }else Console.WriteLine("Cookie is not valid. Skipping downloads...");
            
            // create gif hashset
            foreach (Match gifurl in gifCollection)
            {
                gifs.Add(gifurl.Value);
            }

            // create image hashset
            foreach (Match match in imageCollection)
            {
                images.Add(match.Value);
            }

            #endregion

            #region Directories

            var maindir = Directory.CreateDirectory("BoothDownloaderOut");
            var iddir = Directory.CreateDirectory(maindir + "/" + _boothId);
            var filedir = Directory.CreateDirectory(iddir + "/" + "Binary");

            #endregion

            #region TaskFactories
            
            // create gif task factory
            if (gifs.Count > 0)
            {
                giftasks = gifs.Select(url => Task.Factory.StartNew(state =>
                {
                    using var client = new Webclientsubclass();
                    client.Headers.Add(HttpRequestHeader.Cookie, "adult=t");
                    var urls = (string) state!;
                    Console.WriteLine("starting on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine("starting to download: {0}", urls);
                    var result = client.DownloadData(urls);
                    var name = guidRegex.Match(urls).ToString().Split('/').Last();
                    Console.WriteLine("name: " + name);
                    File.WriteAllBytesAsync(iddir + "/" + name, result);
                    Console.WriteLine("finished downloading: {0}", urls);
                    Console.WriteLine("finished downloading on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                }, url)).ToArray();
                
            }else Console.WriteLine("No gifs found skipping downloader.");

            // create image task factory
            if (images.Count > 0)
            {
                imagetasks = images.Select(url => Task.Factory.StartNew(state =>
                {
                    using var client = new Webclientsubclass();
                    client.Headers.Add(HttpRequestHeader.Cookie, "adult=t");
                    var urls = (string) state!;
                    Console.WriteLine("starting on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine("starting to download: {0}", urls);
                    var result = client.DownloadData(urls);
                    var name = guidRegex.Match(urls).ToString().Split('/').Last();
                    // only download low res images for speed and space
                    if (!name.Contains(Resized)) return;
                    File.WriteAllBytesAsync(iddir + "/" + name, result);
                    Console.WriteLine("finished downloading: {0}", urls);
                    Console.WriteLine("finished downloading on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                }, url)).ToArray();
                
            }else Console.WriteLine("No images found skipping downloader.");


            if (downloadables.Count > 0)
            {
                downloadtasks = downloadables.Select(url => Task.Factory.StartNew(state =>
                {
                    using var client = new Webclientsubclass();
                    client.Headers.Add(HttpRequestHeader.Cookie, "_plaza_session_nktz7u=" + JsonConfig._config._Cookie);
                    var urls = (string) state!;
                    Console.WriteLine("starting on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine("starting to download: {0}", urls);
                    var result = client.DownloadData(urls);
                    var filename = dlNameRegex.Match(client.ResponseUri!.ToString()).Groups[1].Value;
                    File.WriteAllBytesAsync(filedir + "/" + filename, result);
                    Console.WriteLine("finished downloading: {0}", urls);
                    Console.WriteLine("finished downloading on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                }, url)).ToArray();
                
            }else Console.WriteLine("No downloadables found skipping downloader.");
            

            #endregion

            
            #region Wait for all tasks to finish

            
            if (gifs.Count > 0)
            {
                Task.WaitAll(giftasks);
            }

            if (images.Count > 0)
            {
                Task.WaitAll(imagetasks);
            }

            if (downloadables.Count > 0)
            {
                Task.WaitAll(downloadtasks);
            }

            #endregion
            
            #region Compression

            Thread.Sleep(1500);

            if (JsonConfig._config._autozip)
            {
                if (File.Exists(iddir + ".zip"))
                {
                    Console.WriteLine("File already exists. Deleting...");
                    File.Delete(iddir + ".zip");
                }
            
                ZipFile.CreateFromDirectory(iddir.ToString(), iddir + ".zip"); 
                Directory.Delete(iddir.ToString(), true);
                Console.WriteLine("Zipped!");
            }
            
            Console.WriteLine("Done!");

            #endregion
            
            #region Exit Successfully
            
            if (args.Length != 0 && JsonConfig._config._autozip)
            {
                // used for standard output redirection for path to zip file with another process
                Console.WriteLine("ENVFilePATH: " + iddir + ".zip");
            }
            
            Environment.Exit(0);

            #endregion
        }
#pragma warning disable SYSLIB0014
        private class Webclientsubclass : WebClient
        {
            public Uri? ResponseUri { get; private set; }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                var response = base.GetWebResponse(request);
                ResponseUri = response.ResponseUri;
                return response;
            }
        }
    }
}