using System.Text.RegularExpressions;
using BoothDownloader.web;

namespace BoothDownloader.booth;

public class BoothItemPage
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
    private BoothClient Client { get; }
    private string Id { get; }

    private string? _html;

    private string Html
    {
        get
        {
            if (_html == null)
            {
                using var client = Client.MakeWebClient();
                _html = client.DownloadString($"{UrlItemPage}/{Id}");
            }

            return _html;
        }
    }

    public BoothItemPage(BoothClient client, string id)
    {
        Client = client;
        Id = id;
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
}