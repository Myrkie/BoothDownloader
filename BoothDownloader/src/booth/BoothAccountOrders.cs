using System.Text.RegularExpressions;
using BoothDownloader.Utils;
using BoothDownloader.web;

namespace BoothDownloader.booth;

public class BoothAccountOrders : AbstractBoothPage
{
    private const string UrlAccountOrders = "https://accounts.booth.pm/orders";

    private static readonly Regex PageRegex = new(@".*page=(\d+).*");

    private static readonly Regex OrdersRegex = new(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}");

    public BoothAccountOrders(BoothClient client, int page = 1) : base(client, $"{UrlAccountOrders}?page={page}")
    {
    }

    public int LastPage
    {
        get
        {
            var lastPageUrl = Document.DocumentNode.SelectSingleNode(
                    $"//div{XPath.ClassMatcher("pager")}//a{XPath.ClassMatcher("last-page")}{XPath.ClassMatcher("nav-item")}"
                )
                .Attributes["href"].Value;

            return int.Parse(PageRegex.Match(lastPageUrl).Value);
        }
    }

    public IEnumerable<string> Orders => OrdersRegex.Matches(Html).Select(match => match.Value);
}