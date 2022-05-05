using System.Text.RegularExpressions;
using System.Net;

class BoothDownloader
{
    private static string html;
    private static string BoothID;
    private static string str = "https://booth.pm/en/items/";
    private static string resized = "base_resized";
    static void Main(string[] args)
    {
        // thanks to https://github.com/Nekromateion for imageRegex
        Regex imageRegex = new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg)");
        // ID Regex
        Regex IDRegex = new Regex(@"[^/]+(?=/$|$)");

        Regex guid = new Regex(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg)");
        
        Console.WriteLine("Enter the Booth ID or URL:");
        var userinput = Console.ReadLine();
        
        
        BoothID = IDRegex.Match(userinput).Value;
        
        // force ID for all download strings for subdomain support
        html = new WebClient().DownloadString(str + BoothID);

        MatchCollection matches = imageRegex.Matches(html);
        HashSet<string> images = new HashSet<string>();
        
        if(matches.Count == 0)
        {
            Console.WriteLine("No images found. trying with regex png...");
            Regex imageregexpng = new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png)");
            matches = imageregexpng.Matches(html);
        }
        
        Console.WriteLine("Downloading ID: " + BoothID);

        // create hashset
        foreach (Match match in matches)
        {
            images.Add(match.Value);
        }
        
        var dir = Directory.CreateDirectory(BoothID);
        // create image taskfactory
        var tasks = images.Select(url => Task.Factory.StartNew(state =>
        {
            using var client = new WebClient();
            var urls = (string)state;
            Console.WriteLine("starting to download {0}", urls);
            var result = client.DownloadData(urls);
            var name = guid.Match(urls).ToString().Split('/').Last();
            // only download low res images for speed and space
            if (name.Contains(resized))
            {
                File.WriteAllBytesAsync(dir + "\\" + name, result);
                Console.WriteLine("finished downloading {0}", urls);
            }
        }, url)).ToArray();

        Task.WaitAll(tasks);
        
        Console.WriteLine("Done!");
        Thread.Sleep(1500);
        
        Environment.Exit(0);
    }
}