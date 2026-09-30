using System.Text.Json;

namespace SpotifyTaskbarWidget.Core;

public sealed record AppConfig(int LyricOffsetMs = 0, int MaxWidth = AppConfig.DefaultMaxWidth)
{
    private const int DefaultMaxWidth = 620;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Json) ?? new AppConfig();
            return config.MaxWidth > 0 ? config : config with { MaxWidth = DefaultMaxWidth };
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
    }
}
