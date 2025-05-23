﻿using System.Net;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.Web;

public static class BoothHttpClientManager
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";
    private const string UrlItemPage = "https://booth.pm/en/items";

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

        var httpClient = new HttpClient(HttpHandler)
        {
            DefaultRequestHeaders =
            {
                { "Cookie", $"adult=t{(string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie) ? string.Empty : $"; _plaza_session_nktz7u={BoothConfig.Instance.Cookie}")}" },
                { "User-Agent", BoothDownloader.UserAgent }
            }
        };

        var response = await httpClient.GetAsync(UrlAccountSettings, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            LoggerHelper.GlobalLogger.LogInformation("Cookie is valid! - Purchased file downloads will function.");
            /*
                the original request to the booth accounts page uses different cloudfare settings and returns a 302
                anything else after this accepting redirects results in redirect.location breaking for code later down the line.
                this is because `resp.Headers.Location!.ToString();` becomes null in BoothBatchDownloader.cs
            */
            var handlerWithNoRedirects = new HttpRetryMessageHandler(new HttpClientHandler { AllowAutoRedirect = false });
            HttpClient = new HttpClient(handlerWithNoRedirects)
            {
                DefaultRequestHeaders =
                {
                    { "Cookie", $"adult=t{(string.IsNullOrWhiteSpace(BoothConfig.Instance.Cookie) ? string.Empty : $"; _plaza_session_nktz7u={BoothConfig.Instance.Cookie}")}" },
                    { "User-Agent", BoothDownloader.UserAgent }
                }
            };
        }
        else
        {
            LoggerHelper.GlobalLogger.LogWarning("Cookie is not valid. File downloads will not function! Image downloads will still function. Update your cookie in the config file.");
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