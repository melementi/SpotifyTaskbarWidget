using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parses_timestamped_lines()
    {
        var lines = LrcParser.Parse("[00:12.50] Hello\n[01:02.03] World");

        var expected = new[]
        {
            new LyricLine(TimeSpan.FromSeconds(12.5), "Hello"),
            new LyricLine(new TimeSpan(0, 0, 1, 2, 30), "World"),
        };
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Line_with_several_timestamps_yields_one_entry_per_timestamp()
    {
        var lines = LrcParser.Parse("[00:20.00][00:10.00] Chorus");

        var expected = new[]
        {
            new LyricLine(TimeSpan.FromSeconds(10), "Chorus"),
            new LyricLine(TimeSpan.FromSeconds(20), "Chorus"),
        };
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Skips_metadata_and_malformed_lines()
    {
        var lines = LrcParser.Parse("[ar:Someone]\nno timestamp\n[xx:yy] bad\n[00:01.00] ok");

        var only = Assert.Single(lines);
        Assert.Equal("ok", only.Text);
    }

    [Fact]
    public void Empty_text_becomes_a_note_symbol()
    {
        var only = Assert.Single(LrcParser.Parse("[00:05.00]"));

        Assert.Equal("♪", only.Text);
    }

    [Fact]
    public void Sorts_out_of_order_lines()
    {
        var lines = LrcParser.Parse("[00:30.00] C\n[00:10.00] A\n[00:20.00] B");

        Assert.Equal(new[] { "A", "B", "C" }, lines.Select(line => line.Text));
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var lines = LrcParser.Parse("[00:01.00] A\r\n[00:02.00] B");

        Assert.Equal(new[] { "A", "B" }, lines.Select(line => line.Text));
    }

    [Fact]
    public void Accepts_three_digit_and_missing_fractions()
    {
        var lines = LrcParser.Parse("[00:01.123] A\n[00:05] B");

        Assert.Equal(new TimeSpan(0, 0, 0, 1, 123), lines[0].Time);
        Assert.Equal(TimeSpan.FromSeconds(5), lines[1].Time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_input_gives_no_lines(string? lrc)
    {
        Assert.Empty(LrcParser.Parse(lrc));
    }
}
