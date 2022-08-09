using System.Text.RegularExpressions;
using BoothDownloader.Utils;
using BoothDownloader.web;

namespace BoothDownloader.booth;

public class BoothItemPage: AbstractBoothPage
{
    private const string UrlItemPage = "https://booth.pm/en/items";
    
    private const string Resized = "base_resized";

    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)");

    private static readonly Regex
        GuidRegex = new(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)");

    private static readonly Regex ImageRegex =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg|png)");

    private static readonly Regex ImageRegexGif =
        new(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)");

    private static readonly Regex OrdersRegex = new(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}");

    private static readonly Regex DownloadRegex = new(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}");

    public BoothItemPage(BoothClient client, string id): base(client, $"{UrlItemPage}/{id}")
    {
    }

    public IEnumerable<string> Images => ImageRegex.Matches(Html).Select(match => match.Value);
    public IEnumerable<string> ResizedImages => Images.Where(url =>
        // We only care for Resized Images to reduce download time and disk space
        GuidRegex.Match(url).ToString().Split('/').Last().Contains(Resized)
    );
    public IEnumerable<string> Gifs => ImageRegexGif.Matches(Html).Select(match => match.Value);
    public IEnumerable<string> Orders => OrdersRegex.Matches(Html).Select(match => match.Value);
    public IEnumerable<string> OrderIds => Orders.Select(orderUrl => IdRegex.Match(orderUrl).Value);
    public IEnumerable<string> Downloads => DownloadRegex.Matches(Html).Select(match => match.Value);

    public IEnumerable<string> Tags => Document.DocumentNode
        .SelectNodes($"//div{XPath.ClassMatcher("search-guide-tablet-label-inner")}").Select(node => node.InnerText);
}