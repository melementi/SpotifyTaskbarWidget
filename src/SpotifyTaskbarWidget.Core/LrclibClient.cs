using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpotifyTaskbarWidget.Core;

public sealed class LrclibClient(HttpClient http)
{
    private const double DurationToleranceSeconds = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Track(double Duration, bool Instrumental, string? SyncedLyrics);

    public async Task<LyricsResult> FetchAsync(TrackQuery query, CancellationToken ct)
    {
        try
        {
            var match = query.Duration > TimeSpan.Zero ? await GetExactAsync(query, ct) : null;
            match ??= await SearchAsync(query.Title, query.Artist, query.Duration, ct);

            if (match is null)
            {
                var title = TitleCleaner.Clean(query.Title);
                var artist = TitleCleaner.PrimaryArtist(query.Artist);
                if (title != query.Title || artist != query.Artist)
                    match = await SearchAsync(title, artist, query.Duration, ct);
            }

            return match switch
            {
                null => new LyricsResult(LyricsStatus.NotFound),
                { Instrumental: true } => new LyricsResult(LyricsStatus.Instrumental),
                _ => new LyricsResult(LyricsStatus.Synced, match.SyncedLyrics),
            };
        }
        catch (HttpRequestException)
        {
            return new LyricsResult(LyricsStatus.Unavailable);
        }
        catch (JsonException)
        {
            return new LyricsResult(LyricsStatus.Unavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation.
            return new LyricsResult(LyricsStatus.Unavailable);
        }
    }

    private async Task<Track?> GetExactAsync(TrackQuery query, CancellationToken ct)
    {
        var seconds = ((int)Math.Round(query.Duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        var url = $"api/get?track_name={Escape(query.Title)}&artist_name={Escape(query.Artist)}&duration={seconds}";

        using var response = await http.GetAsync(url, ct);
        // LRCLIB answers 400 instead of 404 for inputs it will not match exactly, such as an empty artist
        // or an episode longer than an hour; the search can still find those.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return null;
        response.EnsureSuccessStatusCode();

        var track = await response.Content.ReadFromJsonAsync<Track>(Json, ct);
        return Usable(track) ? track : null;
    }

    private async Task<Track?> SearchAsync(string title, string artist, TimeSpan duration, CancellationToken ct)
    {
        var url = $"api/search?track_name={Escape(title)}&artist_name={Escape(artist)}";

        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var tracks = await response.Content.ReadFromJsonAsync<List<Track>>(Json, ct) ?? [];
        var seconds = duration.TotalSeconds;
        var candidates = tracks.Where(Usable);
        if (seconds > 0)
        {
            candidates = candidates
                .Where(track => Math.Abs(track.Duration - seconds) <= DurationToleranceSeconds)
                .OrderBy(track => Math.Abs(track.Duration - seconds));
        }

        var near = candidates.ToList();
        return near.FirstOrDefault(track => !track.Instrumental) ?? near.FirstOrDefault();
    }

    private static bool Usable(Track? track) =>
        track is not null && (track.Instrumental || !string.IsNullOrWhiteSpace(track.SyncedLyrics));

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
