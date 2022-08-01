using System.Net;

namespace BoothDownloader.web;

#pragma warning disable SYSLIB0014
public class ResponseUriWebClient : WebClient
{
    public Uri? ResponseUri { get; private set; }

    protected override WebResponse GetWebResponse(WebRequest request)
    {
        var response = base.GetWebResponse(request);
        ResponseUri = response.ResponseUri;
        return response;
    }
}
