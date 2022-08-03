using System.Text.RegularExpressions;
using BoothDownloader.web;
using HtmlAgilityPack;

namespace BoothDownloader.booth;

public class BoothAccountOrders
{
    private const string UrlAccountOrders = "https://accounts.booth.pm/orders";

    private static readonly Regex PageRegex = new(@".*page=(\d+).*");

    private static readonly Regex OrdersRegex = new(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}");

    private BoothClient Client { get; }

    private int Page { get; }

    private string? _html;

    private string Html
    {
        get
        {
            if (_html == null)
            {
                using var client = Client.MakeWebClient();
                _html = client.DownloadString($"{UrlAccountOrders}?page={Page}");
            }

            return _html;
        }
    }

    private HtmlDocument? _document;

    private HtmlDocument Document
    {
        get
        {
            if (_document == null)
            {
                _document = new HtmlDocument();
                _document.LoadHtml(Html);
            }

            return _document;
        }
    }

    public BoothAccountOrders(BoothClient client, int page = 1)
    {
        Client = client;
        Page = page;
    }

    public int LastPage
    {
        get
        {
            var lastPageUrl = Document.DocumentNode.SelectSingleNode(
                    "//div[contains(concat(' ', normalize-space(@class), ' '), ' pager ')]//a[contains(concat(' ', normalize-space(@class), ' '), ' last-page ')][contains(concat(' ', normalize-space(@class), ' '), ' nav-item ')]"
                )
                .Attributes["href"].Value;

            return int.Parse(PageRegex.Match(lastPageUrl).Value);
        }
    }

    public IEnumerable<string> Orders => OrdersRegex.Matches(Html).Select(match => match.Value);
}