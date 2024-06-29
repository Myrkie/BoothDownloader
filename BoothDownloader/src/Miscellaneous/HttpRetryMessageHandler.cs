using Polly;

namespace BoothDownloader.Miscellaneous;

public class HttpRetryMessageHandler : DelegatingHandler
{
    public HttpRetryMessageHandler(HttpClientHandler handler) : base(handler) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(x => x.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(8, retryAttempt => TimeSpan.FromSeconds(1 * retryAttempt))
            .ExecuteAsync(() => base.SendAsync(request, cancellationToken));
}
