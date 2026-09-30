using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public class PositionClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Length = TimeSpan.FromSeconds(200);

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    [Fact]
    public void Paused_clock_returns_the_reported_position()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Seconds(60), clock.Now(T0 + Seconds(30)));
    }

    [Fact]
    public void Playing_clock_interpolates_from_the_report_time()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Seconds(65), clock.Now(T0 + Seconds(5)));
    }

    [Fact]
    public void Old_report_at_startup_is_extrapolated_to_now()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0 - Seconds(30), Length);

        Assert.Equal(Seconds(90), clock.Now(T0));
    }

    [Fact]
    public void Position_is_clamped_to_the_track_duration()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Length, clock.Now(T0 + TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Unknown_duration_does_not_clamp()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, TimeSpan.Zero);

        Assert.Equal(Seconds(360), clock.Now(T0 + Seconds(300)));
    }

    [Fact]
    public void Offset_is_added()
    {
        var clock = new PositionClock(TimeSpan.FromMilliseconds(500));
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Seconds(65.5), clock.Now(T0 + Seconds(5)));
    }

    [Fact]
    public void Negative_offset_never_gives_a_negative_position()
    {
        var clock = new PositionClock(TimeSpan.FromSeconds(-5));
        clock.SyncTimeline(Seconds(1), T0, Length);

        Assert.Equal(TimeSpan.Zero, clock.Now(T0));
    }

    [Fact]
    public void Pause_freezes_the_position()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);
        clock.SetPlayback(false, 1.0, T0 + Seconds(5));

        Assert.Equal(Seconds(65), clock.Now(T0 + Seconds(50)));
    }

    [Fact]
    public void Resume_ignores_a_timeline_report_already_applied()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);
        clock.SetPlayback(false, 1.0, T0 + Seconds(5));

        clock.SetPlayback(true, 1.0, T0 + Seconds(100));
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Seconds(67), clock.Now(T0 + Seconds(102)));
    }

    [Fact]
    public void Reset_restarts_from_zero_and_ignores_the_previous_tracks_report()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);

        clock.Reset(T0 + Seconds(10));
        clock.SyncTimeline(Seconds(60), T0, Length);

        Assert.Equal(Seconds(2), clock.Now(T0 + Seconds(12)));
    }

    [Fact]
    public void New_report_after_reset_is_applied()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);
        clock.Reset(T0 + Seconds(10));

        clock.SyncTimeline(Seconds(30), T0 + Seconds(11), Length);

        Assert.Equal(Seconds(31), clock.Now(T0 + Seconds(12)));
    }

    [Fact]
    public void Report_for_the_new_track_applied_before_the_reset_is_kept()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 1.0, T0);
        clock.SyncTimeline(Seconds(60), T0, Length);
        var changedAt = T0 + Seconds(10);

        clock.SyncTimeline(Seconds(0), changedAt + TimeSpan.FromMilliseconds(50), Length);
        clock.Reset(changedAt);

        Assert.Equal(Seconds(1.95), clock.Now(T0 + Seconds(12)));
    }

    [Fact]
    public void Playback_rate_scales_elapsed_time()
    {
        var clock = new PositionClock(TimeSpan.Zero);
        clock.SetPlayback(true, 2.0, T0);
        clock.SyncTimeline(Seconds(10), T0, Length);

        Assert.Equal(Seconds(20), clock.Now(T0 + Seconds(5)));
    }
}
