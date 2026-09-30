using System.IO;
using SpotifyTaskbarWidget.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace SpotifyTaskbarWidget;

public sealed record TrackSnapshot(string Title, string Artist, TimeSpan Duration, byte[]? Thumbnail);

public sealed class MediaWatcher(PositionClock clock)
{
    private readonly object gate = new();
    private readonly SemaphoreSlim publishing = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private Session? session;
    private string? trackKey;
    private TimeSpan publishedDuration;

    public event Action<TrackSnapshot?>? TrackChanged;

    public async Task StartAsync()
    {
        manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        manager.SessionsChanged += (_, _) => Attach();
        Attach();
    }

    private void Attach()
    {
        Session? current;
        lock (gate)
        {
            if (session is not null)
            {
                session.MediaPropertiesChanged -= OnMediaChanged;
                session.PlaybackInfoChanged -= OnPlaybackChanged;
                session.TimelinePropertiesChanged -= OnTimelineChanged;
            }

            session = manager!.GetSessions().FirstOrDefault(candidate =>
                candidate.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
            current = session;

            if (current is not null)
            {
                current.MediaPropertiesChanged += OnMediaChanged;
                current.PlaybackInfoChanged += OnPlaybackChanged;
                current.TimelinePropertiesChanged += OnTimelineChanged;
            }
        }

        if (current is null)
        {
            trackKey = "";
            clock.SetPlayback(false, 1.0, DateTimeOffset.UtcNow);
            TrackChanged?.Invoke(null);
            return;
        }

        // null marks "first track after attaching": its timeline report is current, so the clock must not be reset.
        trackKey = null;
        try
        {
            SyncPlayback(current);
            SyncTimeline(current);
        }
        catch (Exception)
        {
            // Spotify can be exiting while still listed; at startup this runs on the UI thread and would crash the app.
        }
        _ = PublishAsync(current);
    }

    private void OnMediaChanged(Session sender, MediaPropertiesChangedEventArgs args) => _ = PublishAsync(sender);

    private void OnPlaybackChanged(Session sender, PlaybackInfoChangedEventArgs args) => SyncPlayback(sender);

    private void OnTimelineChanged(Session sender, TimelinePropertiesChangedEventArgs args)
    {
        var duration = SyncTimeline(sender);
        if (duration != publishedDuration) _ = PublishAsync(sender);
    }

    private void SyncPlayback(Session source)
    {
        var info = source.GetPlaybackInfo();
        var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        clock.SetPlayback(playing, info.PlaybackRate ?? 1.0, DateTimeOffset.UtcNow);
    }

    private TimeSpan SyncTimeline(Session source)
    {
        var timeline = source.GetTimelineProperties();
        var duration = timeline.EndTime - timeline.StartTime;
        // Players that never report a timeline leave LastUpdatedTime at the 1601 epoch.
        var reportedAt = timeline.LastUpdatedTime.Year < 2000 ? DateTimeOffset.UtcNow : timeline.LastUpdatedTime;
        clock.SyncTimeline(timeline.Position, reportedAt, duration);
        return duration;
    }

    private async Task PublishAsync(Session source)
    {
        var changedAt = DateTimeOffset.UtcNow;
        await publishing.WaitAsync();
        try
        {
            var properties = await source.TryGetMediaPropertiesAsync();
            if (string.IsNullOrWhiteSpace(properties?.Title))
            {
                trackKey = "";
                TrackChanged?.Invoke(null);
                return;
            }

            var key = $"{properties.Artist}|{properties.Title}";
            if (key != trackKey)
            {
                if (trackKey is not null) clock.Reset(changedAt);
                trackKey = key;
            }

            publishedDuration = SyncTimeline(source);
            var thumbnail = await ReadAsync(properties.Thumbnail);
            TrackChanged?.Invoke(new TrackSnapshot(
                properties.Title,
                properties.Artist,
                publishedDuration,
                thumbnail));
        }
        catch (Exception)
        {
            // Spotify can exit while a call is in flight; SessionsChanged then reports the idle state.
        }
        finally
        {
            publishing.Release();
        }
    }

    private static async Task<byte[]?> ReadAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;

        try
        {
            using var source = await reference.OpenReadAsync();
            using var stream = source.AsStreamForRead();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            return memory.ToArray();
        }
        catch (Exception)
        {
            // Unreadable artwork must not also drop the title and artist.
            return null;
        }
    }
}
