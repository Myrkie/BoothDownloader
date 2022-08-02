using System.Text.RegularExpressions;
using BoothDownloader.web;

namespace BoothDownloader.booth;

public class BoothOrderPage
{
    private const string UrlOrderPage = "https://accounts.booth.pm/orders";

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
                _html = client.DownloadString($"{UrlOrderPage}/{Id}");
            }

            return _html;
        }
    }

    public BoothOrderPage(BoothClient client, string id)
    {
        Client = client;
        Id = id;
    }

    public string[] Downloads => DownloadRegex.Matches(Html).Select(match => match.Value).ToArray();
}