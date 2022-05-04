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
        foreach (string img in images)
        {
            Console.WriteLine(img);
            new WebClient().DownloadFile(img, dir + "\\" + BoothID + "_" + index + ".png");
            index++;
        }
        
        Console.WriteLine("Done!");
        Thread.Sleep(1000);
        
        Environment.Exit(0);
    }
}