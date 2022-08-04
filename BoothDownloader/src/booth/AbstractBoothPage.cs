using BoothDownloader.web;
using HtmlAgilityPack;

namespace BoothDownloader.booth;

public abstract class AbstractBoothPage
{
    private BoothClient Client { get; }
    private string Url { get; }

    private string? _html;

    protected string Html
    {
        get
        {
            if (_html == null)
            {
                using var client = Client.MakeWebClient();
                _html = client.DownloadString(Url);
            }

            return _html;
        }
    }

    private HtmlDocument? _document;

    protected HtmlDocument Document
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

    protected AbstractBoothPage(BoothClient client, string url)
    {
        Client = client;
        Url = url;
    }
}