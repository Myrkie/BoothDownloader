using System.Text.RegularExpressions;
using BoothDownloader.web;
using HtmlAgilityPack;

namespace BoothDownloader.booth;

public class BoothOrderPage
{
    private const string UrlOrderPage = "https://accounts.booth.pm/orders";

    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)");
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

    public BoothOrderPage(BoothClient client, string id)
    {
        Client = client;
        Id = id;
    }

    private Dictionary<string, IEnumerable<string>>? _itemDownloads;

    public Dictionary<string, IEnumerable<string>> ItemDownloads
    {
        get
        {
            if (_itemDownloads == null)
            {
                _itemDownloads = new Dictionary<string, IEnumerable<string>>();

                var sheets = Document.DocumentNode.SelectNodes(
                        "//div[contains(concat(' ', normalize-space(@class), ' '), ' l-order-detail-by-shop ')]//div[contains(concat(' ', normalize-space(@class), ' '), ' sheet ')]")
                    .Where(sheet =>
                        sheet.SelectSingleNode(
                            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' u-tpg-title4 ')]//b//a") !=
                        null);

                foreach (var itemSheet in sheets)
                {
                    var itemUrl = itemSheet.SelectSingleNode(
                            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' u-tpg-title4 ')]//b//a")
                        .Attributes["href"].Value;
                    var itemId = IdRegex.Match(itemUrl).Value;

                    var downloadLinks = itemSheet.SelectNodes(
                            ".//div[contains(concat(' ', normalize-space(@class), ' '), ' list ')]//div[contains(concat(' ', normalize-space(@class), ' '), ' legacy-list-item ')]//a[contains(concat(' ', normalize-space(@class), ' '), ' nav-reverse ')]")
                        .Select(downloadNode => downloadNode.Attributes["href"].Value);

                    _itemDownloads.Add(itemId, downloadLinks);
                }
            }

            return _itemDownloads!;
        }
    }
}