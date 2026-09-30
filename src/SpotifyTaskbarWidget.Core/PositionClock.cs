namespace SpotifyTaskbarWidget.Core;

public sealed class PositionClock(TimeSpan offset)
{
    private readonly object gate = new();
    private TimeSpan position;
    private TimeSpan duration;
    private DateTimeOffset reportedAt;
    private DateTimeOffset? appliedReport;
    private bool playing;
    private double rate = 1.0;

    public void SetPlayback(bool playing, double rate, DateTimeOffset now)
    {
        lock (gate)
        {
            if (playing == this.playing && rate == this.rate) return;
            position = RawAt(now);
            reportedAt = now;
            this.playing = playing;
            this.rate = rate;
        }
    }

    public void SyncTimeline(TimeSpan position, DateTimeOffset reportedAt, TimeSpan duration)
    {
        lock (gate)
        {
            this.duration = duration;
            if (reportedAt == appliedReport) return;
            appliedReport = reportedAt;
            this.position = position;
            this.reportedAt = reportedAt;
        }
    }

    public void Reset(DateTimeOffset changedAt)
    {
        lock (gate)
        {
            // A report newer than the track change already belongs to the new track.
            if (appliedReport > changedAt) return;
            position = TimeSpan.Zero;
            duration = TimeSpan.Zero;
            reportedAt = changedAt;
        }
    }

    public TimeSpan Now(DateTimeOffset now)
    {
        lock (gate)
        {
            var shifted = RawAt(now) + offset;
            return shifted < TimeSpan.Zero ? TimeSpan.Zero : shifted;
        }
    }

    private TimeSpan RawAt(DateTimeOffset now)
    {
        var elapsed = playing && now > reportedAt ? (now - reportedAt) * rate : TimeSpan.Zero;
        var raw = position + elapsed;
        return duration > TimeSpan.Zero && raw > duration ? duration : raw;
    }
}
