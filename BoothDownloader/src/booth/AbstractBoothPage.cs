using BoothDownloader.web;
using HtmlAgilityPack;

namespace BoothDownloader.booth;

public abstract class AbstractBoothPage
{
    private BoothClient Client { get; }
    private string Url { get; }
    
    private readonly Task<string> _html;

    protected AbstractBoothPage(BoothClient client, string url)
    {
        Client = client;
        Url = url;
        _html = LoadHtml();
    }

    private async Task<string> LoadHtml()
    {
        var response = await Client.GetClient().GetAsync(Url);

        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task<string> GetHtml()
    {
        return await _html;
    }

    protected async Task<HtmlDocument> GetDocument()
    {
        var html = await GetHtml();
        
        var document = new HtmlDocument
        {
            OptionEmptyCollection = true,
        };
        document.LoadHtml(html);
        
        return document;
    }
}