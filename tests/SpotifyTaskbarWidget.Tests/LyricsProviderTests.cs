using System.Net;
using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public sealed class LyricsProviderTests : IDisposable
{
    private const string Hit = """{"duration":267.0,"instrumental":false,"syncedLyrics":"[00:01.00] Line"}""";
    private static readonly TrackQuery Query = new("Yellow", "Coldplay", TimeSpan.FromSeconds(267));
    private readonly string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private (LyricsProvider Provider, FakeHandler Handler) Create(Func<string, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.net/") };
        return (new LyricsProvider(new LyricsCache(directory), new LrclibClient(http)), handler);
    }

    [Fact]
    public async Task Fetched_lyrics_are_served_from_disk_afterwards()
    {
        var (first, _) = Create(_ => FakeHandler.Json(Hit));
        await first.GetAsync(Query, CancellationToken.None);

        var (second, handler) = Create(_ => throw new HttpRequestException("offline"));
        var result = await second.GetAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.Equal("[00:01.00] Line", result.SyncedLyrics);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Cache_write_failure_still_returns_fetched_lyrics()
    {
        File.WriteAllText(directory, "a file where the cache folder should be");
        try
        {
            var (provider, _) = Create(_ => FakeHandler.Json(Hit));

            var result = await provider.GetAsync(Query, CancellationToken.None);

            Assert.Equal(LyricsStatus.Synced, result.Status);
        }
        finally
        {
            File.Delete(directory);
        }
    }

    [Fact]
    public async Task Not_found_is_remembered_without_new_requests()
    {
        var (provider, handler) = Create(url => url.StartsWith("/api/get")
            ? FakeHandler.Json("{}", HttpStatusCode.NotFound)
            : FakeHandler.Json("[]"));

        await provider.GetAsync(Query, CancellationToken.None);
        var requestsAfterFirst = handler.Requests.Count;
        var result = await provider.GetAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.NotFound, result.Status);
        Assert.Equal(requestsAfterFirst, handler.Requests.Count);
    }

    [Fact]
    public async Task Unavailable_is_retried_on_the_next_call()
    {
        var online = false;
        var (provider, _) = Create(_ => online ? FakeHandler.Json(Hit) : throw new HttpRequestException("offline"));

        var failed = await provider.GetAsync(Query, CancellationToken.None);
        online = true;
        var recovered = await provider.GetAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Unavailable, failed.Status);
        Assert.Equal(LyricsStatus.Synced, recovered.Status);
    }
}
