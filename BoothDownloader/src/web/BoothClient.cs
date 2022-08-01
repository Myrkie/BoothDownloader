using System.Net;
using BoothDownloader.config;

namespace BoothDownloader.web;

public class BoothClient
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";
    public Config Config { get; }

    public BoothClient(Config config)
    {
        Config = config;
        
        
    }

    public ResponseUriWebClient MakeWebClient()
    {
        var client = new ResponseUriWebClient();
        client.Headers.Add(HttpRequestHeader.Cookie, $"adult=t; _plaza_session_nktz7u={Config.Cookie}");

        return client;
    }

    public bool IsCookieValid()
    {
        var client = MakeWebClient();
        client.DownloadString(UrlAccountSettings);

        return client.ResponseUri!.ToString() == UrlAccountSettings;
    }

    public string GetItemPage(string id)
    {
        var client = MakeWebClient();
        return client.DownloadString($"{UrlItemPage}/{id}");
    }
}