using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public class TitleCleanerTests
{
    [Theory]
    [InlineData("Yellow - Remastered 2011", "Yellow")]
    [InlineData("Song (feat. Someone)", "Song")]
    [InlineData("Song [Live]", "Song")]
    [InlineData("Song (Live) - 2019 Mix", "Song")]
    [InlineData("Song", "Song")]
    [InlineData("(Intro)", "(Intro)")]
    public void Clean_removes_suffixes(string title, string expected)
    {
        Assert.Equal(expected, TitleCleaner.Clean(title));
    }

    [Theory]
    [InlineData("First, Second", "First")]
    [InlineData("First; Second", "First")]
    [InlineData("Solo", "Solo")]
    public void PrimaryArtist_keeps_the_first_name(string artist, string expected)
    {
        Assert.Equal(expected, TitleCleaner.PrimaryArtist(artist));
    }
}
