using System.Text.RegularExpressions;
using System.Net;

class BoothDownloader
{
    private static string html;
    private static string BoothID;
    private static string str = "https://booth.pm/en/items/";
    static void Main(string[] args)
    {
        // thanks to https://github.com/Nekromateion for imageRegex
        Regex imageRegex = new Regex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png)");
        // ID Regex
        Regex IDRegex = new Regex(@"[^/]+(?=/$|$)");
        
        Console.WriteLine("Enter the Booth ID or URL:");
        var userinput = Console.ReadLine();
        
        
        BoothID = IDRegex.Match(userinput).Value;
        
        // force ID for all download strings for subdomain support
        html = new WebClient().DownloadString(str + BoothID);
        
        Console.WriteLine("Downloading ID: " + BoothID);

        MatchCollection matches = imageRegex.Matches(html);
        HashSet<string> images = new HashSet<string>();

        // create hashset
        foreach (Match match in matches)
        {
            images.Add(match.Value);
        }
        
        var dir = Directory.CreateDirectory(BoothID);
        var index = 1;
        // create image taskfactory
        var tasks = images.Select(url => Task.Factory.StartNew(state =>
        {
            using var client = new WebClient();
            var urls = (string)state;
            Console.WriteLine("starting to download {0}", urls);
            var result = client.DownloadData(urls);
            File.WriteAllBytesAsync(dir + "\\" + BoothID + "_" + index + ".png", result);
            Console.WriteLine("finished downloading {0}", urls);
            index++;
        }, url)).ToArray();

        Task.WaitAll(tasks);
        
        Console.WriteLine("Done!");
        Thread.Sleep(2000);
        
        Environment.Exit(0);
    }
}