using System.Net;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoothDownloader.Web;

public static class BoothHttpClientManager
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";
    private const string SessionCookieName = "_plaza_session_nktz7u";
    private static readonly Uri BoothCookieUri = new("https://accounts.booth.pm");

    private static HttpRetryMessageHandler HttpHandler => new(new HttpClientHandler { AllowAutoRedirect = true });

    public static HttpClient AnonymousHttpClient { get; } = new(HttpHandler)
    {
        DefaultRequestHeaders =
        {
            { "Cookie", "adult=t" },
            { "User-Agent", BoothDownloader.UserAgent }
        }
    };
    public static HttpClient HttpClient { get; private set; } = AnonymousHttpClient;
    public static bool IsAnonymous => HttpClient == AnonymousHttpClient;

    public static async Task Setup(CancellationToken cancellationToken)
    {
        if (BoothConfig.Instance.Cookie == BoothConfig.AnonymousCookie)
        {
            LoggerHelper.GlobalLogger.LogWarning("Using anonymous cookie - Purchased file downloads will not function.");
            return;
        }

        var cookieContainer = new CookieContainer();
        // BOOTH uses adult=t as its age-gate preference. It is not an authentication cookie.
        cookieContainer.Add(new Cookie("adult", "t", "/", ".booth.pm"));
        cookieContainer.Add(new Cookie(SessionCookieName, BoothConfig.Instance.Cookie, "/", ".booth.pm"));

        /*
            Account validation follows redirects because BOOTH may refresh the session while loading
            the settings page. The CookieContainer retains those refreshed cookies for later requests.

            Download requests use a second handler with redirects disabled so BoothBatchDownloader can
            read the Location header and derive the actual CDN filename. Both handlers share the same
            CookieContainer so the authenticated session is not lost between those two behaviors.
        */
        using var validationClient = new HttpClient(new HttpRetryMessageHandler(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            CookieContainer = cookieContainer
        }))
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", BoothDownloader.UserAgent }
            }
        };

        using var response = await validationClient.GetAsync(UrlAccountSettings, cancellationToken);
        var finalUri = response.RequestMessage?.RequestUri;
        var reachedAccountSettings = response.StatusCode == HttpStatusCode.OK
                                     && finalUri != null
                                     && finalUri.Host.Equals("accounts.booth.pm", StringComparison.OrdinalIgnoreCase)
                                     && finalUri.AbsolutePath.TrimEnd('/').Equals("/settings", StringComparison.OrdinalIgnoreCase);

        if (reachedAccountSettings)
        {
            LoggerHelper.GlobalLogger.LogInformation("Cookie is valid! - Purchased file downloads will function.");
            var activeSessionCookie = cookieContainer.GetCookies(BoothCookieUri)[SessionCookieName]?.Value;
            if (!string.IsNullOrWhiteSpace(activeSessionCookie)
                && !string.Equals(activeSessionCookie, BoothConfig.Instance.Cookie, StringComparison.Ordinal))
            {
                BoothConfig.Instance.Cookie = activeSessionCookie;
                BoothConfig.ConfigInstance.Save();
                LoggerHelper.GlobalLogger.LogInformation("Saved refreshed BOOTH session cookie.");
            }

            var handlerWithNoRedirects = new HttpRetryMessageHandler(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = cookieContainer
            });
            HttpClient = new HttpClient(handlerWithNoRedirects)
            {
                DefaultRequestHeaders =
                {
                    { "User-Agent", BoothDownloader.UserAgent }
                }
            };
        }
        else
        {
            LoggerHelper.GlobalLogger.LogWarning("Cookie is not valid. BOOTH redirected the account request to {redirectUri}.", finalUri);
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

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType == null
            || (!mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
        {
            throw new HttpRequestException(
                $"BOOTH returned {mediaType ?? "an unknown content type"} instead of JSON for item {id}.",
                null,
                response.StatusCode);
        }

        try
        {
            JToken.Parse(responseString);
        }
        catch (JsonReaderException exception)
        {
            throw new HttpRequestException(
                $"BOOTH returned an invalid JSON payload for item {id}.",
                exception,
                response.StatusCode);
        }

        return responseString;
    }

    public class DownloadFailedException : Exception
    {
        public override string Message => "The order collection downloader has failed";
    }
}
