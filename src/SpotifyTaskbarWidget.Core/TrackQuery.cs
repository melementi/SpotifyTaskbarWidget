namespace SpotifyTaskbarWidget.Core;

public sealed record TrackQuery(string Title, string Artist, TimeSpan Duration);

public enum LyricsStatus
{
    Synced,
    Instrumental,
    NotFound,
    Unavailable,
}

public sealed record LyricsResult(LyricsStatus Status, string? SyncedLyrics = null);
