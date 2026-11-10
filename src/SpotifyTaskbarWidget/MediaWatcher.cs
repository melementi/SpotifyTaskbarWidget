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
    private Attachment? attached;

    public event Action<TrackSnapshot?>? TrackChanged;

    // Everything known about one Spotify session. Windows raises its events on pool threads, and a call
    // can still be in flight when Spotify is replaced; work that belongs to an older attachment is dropped.
    private sealed class Attachment(MediaWatcher owner, Session session)
    {
        public Session Session => session;

        // null marks "first track after attaching": its timeline report is current, so the clock must not be reset.
        public string? TrackKey;
        public TimeSpan PublishedDuration;

        public void Subscribe()
        {
            session.MediaPropertiesChanged += OnMediaChanged;
            session.PlaybackInfoChanged += OnPlaybackChanged;
            session.TimelinePropertiesChanged += OnTimelineChanged;
        }

        public void Unsubscribe()
        {
            try
            {
                session.MediaPropertiesChanged -= OnMediaChanged;
                session.PlaybackInfoChanged -= OnPlaybackChanged;
                session.TimelinePropertiesChanged -= OnTimelineChanged;
            }
            catch (Exception)
            {
                // The session is already gone, and its events with it.
            }
        }

        private void OnMediaChanged(Session sender, MediaPropertiesChangedEventArgs args) => _ = owner.PublishAsync(this);

        private void OnPlaybackChanged(Session sender, PlaybackInfoChangedEventArgs args) => owner.OnPlaybackChanged(this);

        private void OnTimelineChanged(Session sender, TimelinePropertiesChangedEventArgs args) => owner.OnTimelineChanged(this);
    }

    public async Task StartAsync()
    {
        manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        manager.SessionsChanged += (_, _) => Attach();
        Attach();
    }

    private void Attach()
    {
        var found = FindSpotify();
        Attachment? current;
        lock (gate)
        {
            attached?.Unsubscribe();
            current = found is null ? null : new Attachment(this, found);
            attached = current;
        }

        if (current is null)
        {
            clock.SetPlayback(false, 1.0, DateTimeOffset.UtcNow);
            TrackChanged?.Invoke(null);
            return;
        }

        try
        {
            current.Subscribe();
            SyncPlayback(current.Session);
            SyncTimeline(current.Session);
        }
        catch (Exception)
        {
            // Spotify can be exiting while still listed; at startup this runs on the UI thread and would crash the app.
        }
        _ = PublishAsync(current);
    }

    private Session? FindSpotify()
    {
        try
        {
            return manager!.GetSessions().FirstOrDefault(candidate =>
                candidate.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // A session can disappear while the list is being read; SessionsChanged follows with the new list.
            return null;
        }
    }

    private bool IsCurrent(Attachment attachment)
    {
        lock (gate) return ReferenceEquals(attached, attachment);
    }

    private void OnPlaybackChanged(Attachment source)
    {
        if (!IsCurrent(source)) return;
        try
        {
            SyncPlayback(source.Session);
        }
        catch (Exception)
        {
            // Spotify can exit between raising the event and answering; SessionsChanged then reports the idle state.
        }
    }

    private void OnTimelineChanged(Attachment source)
    {
        if (!IsCurrent(source)) return;
        try
        {
            var duration = SyncTimeline(source.Session);
            bool changed;
            lock (gate) changed = duration != source.PublishedDuration;
            if (changed) _ = PublishAsync(source);
        }
        catch (Exception)
        {
            // As above: the session went away mid-call.
        }
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

    private async Task PublishAsync(Attachment source)
    {
        var changedAt = DateTimeOffset.UtcNow;
        await publishing.WaitAsync();
        try
        {
            if (!IsCurrent(source)) return;

            var properties = await source.Session.TryGetMediaPropertiesAsync();
            if (string.IsNullOrWhiteSpace(properties?.Title))
            {
                lock (gate)
                {
                    if (!ReferenceEquals(attached, source)) return;
                    source.TrackKey = "";
                }
                TrackChanged?.Invoke(null);
                return;
            }

            var key = $"{properties.Artist}|{properties.Title}";
            bool newTrack;
            lock (gate)
            {
                if (!ReferenceEquals(attached, source)) return;
                newTrack = source.TrackKey is not null && key != source.TrackKey;
                source.TrackKey = key;
            }
            if (newTrack) clock.Reset(changedAt);

            var duration = SyncTimeline(source.Session);
            lock (gate) source.PublishedDuration = duration;

            var thumbnail = await ReadAsync(properties.Thumbnail);
            if (!IsCurrent(source)) return;
            TrackChanged?.Invoke(new TrackSnapshot(
                properties.Title,
                properties.Artist,
                duration,
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
