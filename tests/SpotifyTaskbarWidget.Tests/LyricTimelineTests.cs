using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public class LyricTimelineTests
{
    private static readonly LyricTimeline Three = new(
    [
        new LyricLine(TimeSpan.FromSeconds(10), "A"),
        new LyricLine(TimeSpan.FromSeconds(20), "B"),
        new LyricLine(TimeSpan.FromSeconds(30), "C"),
    ]);

    [Fact]
    public void Before_first_line_only_next_is_set()
    {
        Assert.Equal(new LyricWindow("", "", "A"), Three.At(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void On_first_line_there_is_no_previous()
    {
        Assert.Equal(new LyricWindow("", "A", "B"), Three.At(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void Between_lines_shows_all_three()
    {
        Assert.Equal(new LyricWindow("A", "B", "C"), Three.At(TimeSpan.FromSeconds(25)));
    }

    [Fact]
    public void Exactly_on_a_timestamp_selects_that_line()
    {
        Assert.Equal(new LyricWindow("A", "B", "C"), Three.At(TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void After_last_line_there_is_no_next()
    {
        Assert.Equal(new LyricWindow("B", "C", ""), Three.At(TimeSpan.FromSeconds(99)));
    }

    [Fact]
    public void Single_line_file()
    {
        var single = new LyricTimeline([new LyricLine(TimeSpan.FromSeconds(5), "Only")]);

        Assert.Equal(new LyricWindow("", "", "Only"), single.At(TimeSpan.Zero));
        Assert.Equal(new LyricWindow("", "Only", ""), single.At(TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void No_lines_gives_an_empty_window()
    {
        Assert.Equal(new LyricWindow("", "", ""), new LyricTimeline([]).At(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Next_change_before_the_first_line_is_the_first_line()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), Three.NextChangeAfter(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Next_change_between_lines_is_the_following_line()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Three.NextChangeAfter(TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void Next_change_skips_lines_sharing_the_current_timestamp()
    {
        var timeline = new LyricTimeline(
        [
            new LyricLine(TimeSpan.FromSeconds(5), "A"),
            new LyricLine(TimeSpan.FromSeconds(5), "B"),
            new LyricLine(TimeSpan.FromSeconds(9), "C"),
        ]);

        Assert.Equal(TimeSpan.FromSeconds(9), timeline.NextChangeAfter(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void No_next_change_after_the_last_line()
    {
        Assert.Null(Three.NextChangeAfter(TimeSpan.FromSeconds(30)));
        Assert.Null(new LyricTimeline([]).NextChangeAfter(TimeSpan.Zero));
    }
}
