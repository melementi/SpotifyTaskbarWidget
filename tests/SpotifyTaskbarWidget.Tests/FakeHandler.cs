using System.Net;
using System.Text;

namespace SpotifyTaskbarWidget.Tests;

internal sealed class FakeHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.PathAndQuery;
        Requests.Add(url);
        return Task.FromResult(respond(url));
    }
}
