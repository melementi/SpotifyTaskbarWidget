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
        if (result.Status is LyricsStatus.Synced or LyricsStatus.Instrumental) cache.Put(query, result);
        else if (result.Status == LyricsStatus.NotFound) notFound.Add(key);
        return result;
    }
}
