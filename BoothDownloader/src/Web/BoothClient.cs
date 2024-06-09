using System.Net;
using BoothDownloader.Configuration;

namespace BoothDownloader.Web;

public class BoothClient
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";

    private static readonly HttpClientHandler HttpHandler = new() { AllowAutoRedirect = false };

    public class DownloadFailedException : Exception
    {
        public override string Message => "The order collection downloader has failed";
    }

    public HttpClient MakeHttpClient()
    {
        var httpClient = new HttpClient(HttpHandler);

        httpClient.DefaultRequestHeaders.Add("Cookie", $"adult=t; _plaza_session_nktz7u={BoothConfig.Instance.Cookie}");

        return httpClient;
    }

    public async Task<bool> IsCookieValidAsync(CancellationToken cancellationToken = default)
    {
        var client = MakeHttpClient();
        var response = await client.GetAsync(UrlAccountSettings, cancellationToken);

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<string> GetItemPageAsync(string id, CancellationToken cancellationToken = default)
    {
        var client = MakeHttpClient();
        var response = await client.GetAsync($"{UrlItemPage}/{id}", cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        return responseString;
    }
}