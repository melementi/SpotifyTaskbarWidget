using System.Net;
using System.Text.Json;
using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public class LrclibClientTests
{
    private static readonly TrackQuery Query = new("Yellow", "Coldplay", TimeSpan.FromSeconds(267));

    private static HttpResponseMessage NotFound => FakeHandler.Json("{}", HttpStatusCode.NotFound);

    private static string Track(double duration, string? synced, bool instrumental = false) =>
        JsonSerializer.Serialize(new { duration, instrumental, syncedLyrics = synced });

    private static (LrclibClient Client, FakeHandler Handler) Create(Func<string, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.net/") };
        return (new LrclibClient(http), handler);
    }

    [Fact]
    public async Task Exact_match_returns_synced_lyrics_in_one_request()
    {
        var (client, handler) = Create(_ => FakeHandler.Json(Track(267, "[00:01.00] First line")));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.Equal("[00:01.00] First line", result.SyncedLyrics);
        Assert.Equal("/api/get?track_name=Yellow&artist_name=Coldplay&duration=267", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task Missing_exact_match_falls_back_to_search_and_picks_closest_duration()
    {
        var (client, handler) = Create(url => url.StartsWith("/api/get")
            ? NotFound
            : FakeHandler.Json($"[{Track(300, "[00:01.00] Far")},{Track(269, "[00:01.00] Near")},{Track(267, null)}]"));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.Equal("[00:01.00] Near", result.SyncedLyrics);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/api/search?track_name=Yellow&artist_name=Coldplay", handler.Requests[1]);
    }

    [Fact]
    public async Task Search_prefers_the_closest_duration_over_the_first_in_tolerance()
    {
        var query = new TrackQuery("Yellow", "Coldplay", TimeSpan.FromSeconds(268));
        var (client, _) = Create(url => url.StartsWith("/api/get")
            ? NotFound
            : FakeHandler.Json($"[{Track(265, "[00:01.00] Edge")},{Track(269, "[00:01.00] Closest")}]"));

        var result = await client.FetchAsync(query, CancellationToken.None);

        Assert.Equal("[00:01.00] Closest", result.SyncedLyrics);
    }

    [Fact]
    public async Task Search_results_outside_three_seconds_are_rejected()
    {
        var (client, handler) = Create(url => url.StartsWith("/api/get")
            ? NotFound
            : FakeHandler.Json($"[{Track(300, "[00:01.00] Far")}]"));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.NotFound, result.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Retries_search_with_cleaned_title_and_primary_artist()
    {
        var query = new TrackQuery("Yellow - Remastered 2011", "Coldplay, Someone", TimeSpan.FromSeconds(267));
        var (client, handler) = Create(url =>
            url.StartsWith("/api/get") ? NotFound
            : url.Contains("Remastered") ? FakeHandler.Json("[]")
            : FakeHandler.Json($"[{Track(267, "[00:01.00] Found")}]"));

        var result = await client.FetchAsync(query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("/api/search?track_name=Yellow&artist_name=Coldplay", handler.Requests[2]);
    }

    [Fact]
    public async Task Instrumental_track_is_reported_as_instrumental()
    {
        var (client, _) = Create(_ => FakeHandler.Json(Track(267, null, instrumental: true)));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Instrumental, result.Status);
    }

    [Fact]
    public async Task Exact_match_with_plain_lyrics_only_falls_back_to_search()
    {
        var (client, handler) = Create(url => url.StartsWith("/api/get")
            ? FakeHandler.Json(Track(267, null))
            : FakeHandler.Json($"[{Track(267, "[00:01.00] Synced")}]"));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Server_error_is_unavailable()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Network_failure_is_unavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("offline"));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Invalid_json_is_unavailable()
    {
        var (client, _) = Create(_ => FakeHandler.Json("not json"));

        var result = await client.FetchAsync(Query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Zero_duration_skips_exact_lookup_and_duration_filter()
    {
        var query = new TrackQuery("Yellow", "Coldplay", TimeSpan.Zero);
        var (client, handler) = Create(_ => FakeHandler.Json($"[{Track(180, "[00:01.00] Any")}]"));

        var result = await client.FetchAsync(query, CancellationToken.None);

        Assert.Equal(LyricsStatus.Synced, result.Status);
        Assert.StartsWith("/api/search", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task Escapes_reserved_characters()
    {
        var query = new TrackQuery("Rock & Roll", "AC/DC", TimeSpan.FromSeconds(267));
        var (client, handler) = Create(_ => FakeHandler.Json(Track(267, "[00:01.00] Line")));

        await client.FetchAsync(query, CancellationToken.None);

        Assert.Equal("/api/get?track_name=Rock%20%26%20Roll&artist_name=AC%2FDC&duration=267", handler.Requests[0]);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        var (client, _) = Create(_ => FakeHandler.Json(Track(267, "[00:01.00] Line")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchAsync(Query, cancelled.Token));
    }
}
