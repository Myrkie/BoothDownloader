using System.Net;
using BoothDownloader.Configuration;

namespace BoothDownloader.Web;

public static class BoothHttpClientManager
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";

    private static HttpClientHandler HttpHandler => new() { AllowAutoRedirect = false };

    public static HttpClient AnonymousHttpClient { get; } = new(HttpHandler)
    {
        DefaultRequestHeaders =
        {
            { "Cookie", "adult=t" }
        }
    };
    public static HttpClient HttpClient { get; private set; } = AnonymousHttpClient;
    public static bool IsAnonymous => HttpClient == AnonymousHttpClient;

    public static async Task Setup(CancellationToken cancellationToken)
    {
        var httpClient = new HttpClient(HttpHandler);
        httpClient.DefaultRequestHeaders.Add("Cookie", $"adult=t{(string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie) ? string.Empty : $"; _plaza_session_nktz7u={BoothConfig.Instance.Cookie}")}");

        var response = await httpClient.GetAsync(UrlAccountSettings, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Console.WriteLine("Cookie is valid! - Purchased file downloads will function.\n");
            HttpClient = httpClient;
        }
        else
        {
            Console.WriteLine("Cookie is not valid file downloads will not function!\nImage downloads will still function\nUpdate your cookie in the config file.\n");
            BoothConfig.Instance.Cookie = string.Empty;
            BoothConfig.ConfigInstance.Save();
        }
    }

    public static async Task<string> GetItemPageAsync(string id, bool asAnonymous = false, CancellationToken cancellationToken = default)
    {
        var httpClient = asAnonymous ? AnonymousHttpClient : HttpClient;
        var response = await httpClient.GetAsync($"{UrlItemPage}/{id}", cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        return responseString;
    }

    public static async Task<string> GetItemJsonAsync(string id, bool asAnonymous = false, CancellationToken cancellationToken = default)
    {
        var httpClient = asAnonymous ? AnonymousHttpClient : HttpClient;
        var response = await httpClient.GetAsync($"{UrlItemPage}/{id}.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        return responseString;
    }

    public class DownloadFailedException : Exception
    {
        public override string Message => "The order collection downloader has failed";
    }
}