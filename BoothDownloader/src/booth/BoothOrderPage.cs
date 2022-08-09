using System.Text.RegularExpressions;
using BoothDownloader.Utils;
using BoothDownloader.web;

namespace BoothDownloader.booth;

public class BoothOrderPage : AbstractBoothPage
{
    private const string UrlOrderPage = "https://accounts.booth.pm/orders";

    private static readonly Regex IdRegex = new(@"[^/]+(?=/$|$)");

    public BoothOrderPage(BoothClient client, string id) : base(client, $"{UrlOrderPage}/{id}")
    {
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
                        $"//div{XPath.ClassMatcher("l-order-detail-by-shop")}//div{XPath.ClassMatcher("sheet")}")
                    .Where(sheet =>
                        sheet.SelectSingleNode($".//div{XPath.ClassMatcher("u-tpg-title4")}//b//a") != null
                    );

                foreach (var itemSheet in sheets)
                {
                    var itemUrl = itemSheet.SelectSingleNode(
                            $".//div{XPath.ClassMatcher("u-tpg-title4")}//b//a"
                        )
                        .Attributes["href"].Value;
                    var itemId = IdRegex.Match(itemUrl).Value;

                    var downloadLinks = itemSheet.SelectNodes(
                            $".//div{XPath.ClassMatcher("list")}//div{XPath.ClassMatcher("legacy-list-item")}//a{XPath.ClassMatcher("nav-reverse")}")
                        .Select(downloadNode => downloadNode.Attributes["href"].Value);

                    _itemDownloads.Add(itemId, downloadLinks);
                }
            }

            return _itemDownloads!;
        }
    }
}