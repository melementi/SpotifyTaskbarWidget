namespace SpotifyTaskbarWidget.Core;

public sealed class LyricsProvider(LyricsCache cache, LrclibClient client)
{
    private readonly HashSet<string> notFound = [];

    public async Task<LyricsResult> GetAsync(TrackQuery query, CancellationToken ct)
    {
        if (cache.TryGet(query) is { } cached) return cached;

        var key = LyricsCache.KeyFor(query);
        if (notFound.Contains(key)) return new LyricsResult(LyricsStatus.NotFound);

        var result = await client.FetchAsync(query, ct);
        // Without a duration the search cannot tell versions of a song apart, so its pick is a guess
        // that must not be kept for good.
        var reliable = query.Duration > TimeSpan.Zero;
        if (result.Status is LyricsStatus.Synced or LyricsStatus.Instrumental)
        {
            if (reliable) cache.Put(query, result);
        }
        else if (result.Status == LyricsStatus.NotFound)
        {
            notFound.Add(key);
        }
        return result;
    }
}
