using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget.Tests;

public sealed class AppConfigTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    public void Dispose() => File.Delete(path);

    [Fact]
    public void Missing_file_gives_defaults()
    {
        Assert.Equal(new AppConfig(0, 620), AppConfig.Load(path));
    }

    [Fact]
    public void Reads_both_values()
    {
        File.WriteAllText(path, """{ "lyricOffsetMs": -250, "maxWidth": 500 }""");

        Assert.Equal(new AppConfig(-250, 500), AppConfig.Load(path));
    }

    [Fact]
    public void Missing_property_keeps_its_default()
    {
        File.WriteAllText(path, """{ "lyricOffsetMs": 300 }""");

        Assert.Equal(new AppConfig(300, 620), AppConfig.Load(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Non_positive_max_width_falls_back_to_default(int maxWidth)
    {
        File.WriteAllText(path, $$"""{ "lyricOffsetMs": 100, "maxWidth": {{maxWidth}} }""");

        Assert.Equal(new AppConfig(100, 620), AppConfig.Load(path));
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        File.WriteAllText(path, "{ lyricOffsetMs: oops");

        Assert.Equal(new AppConfig(0, 620), AppConfig.Load(path));
    }
}
