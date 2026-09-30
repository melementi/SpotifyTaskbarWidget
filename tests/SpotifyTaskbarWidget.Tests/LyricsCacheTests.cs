using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public sealed class LyricsCacheTests : IDisposable
{
    private static readonly TrackQuery Query = new("Yellow", "Coldplay", TimeSpan.FromSeconds(267));
    private readonly string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void Missing_entry_is_null()
    {
        Assert.Null(new LyricsCache(directory).TryGet(Query));
    }

    [Fact]
    public void Stored_result_round_trips()
    {
        var cache = new LyricsCache(directory);
        var stored = new LyricsResult(LyricsStatus.Synced, "[00:01.00] Line");

        cache.Put(Query, stored);

        Assert.Equal(stored, new LyricsCache(directory).TryGet(Query));
    }

    [Fact]
    public void Key_ignores_case_whitespace_and_sub_second_duration()
    {
        var other = new TrackQuery(" yellow ", "COLDPLAY", TimeSpan.FromSeconds(267.4));

        Assert.Equal(LyricsCache.KeyFor(Query), LyricsCache.KeyFor(other));
    }

    [Fact]
    public void Key_differs_for_another_track()
    {
        var other = new TrackQuery("Clocks", "Coldplay", TimeSpan.FromSeconds(267));

        Assert.NotEqual(LyricsCache.KeyFor(Query), LyricsCache.KeyFor(other));
    }

    [Fact]
    public void Corrupt_cache_file_is_a_miss()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, LyricsCache.KeyFor(Query) + ".json"), "{ not json");

        Assert.Null(new LyricsCache(directory).TryGet(Query));
    }
}
