using System.Net;
using BoothDownloader.config;

namespace BoothDownloader.web;

public class BoothClient
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";
    private Config Config { get; }

    private static readonly HttpClientHandler HttpHandler = new HttpClientHandler { AllowAutoRedirect = false };

    public BoothClient(Config config)
    {
        Config = config;
        
        
    }
    public class DownloadFailedException : Exception
    {
        public override string Message => "The order collection downloader has failed";
    }

    public ResponseUriWebClient MakeWebClient()
    {
        var client = new ResponseUriWebClient();
        client.Headers.Add(HttpRequestHeader.Cookie, $"adult=t; _plaza_session_nktz7u={Config.Cookie}");

        return client;
    }

    public HttpClient MakeHttpClient()
    {
        var httpClient = new HttpClient(HttpHandler);

        httpClient.DefaultRequestHeaders.Add("Cookie", $"adult=t; _plaza_session_nktz7u={Config.Cookie}");

        return httpClient;
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