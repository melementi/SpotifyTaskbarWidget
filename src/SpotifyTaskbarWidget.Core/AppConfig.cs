using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpotifyTaskbarWidget.Core;

public sealed record AppConfig(int LyricOffsetMs = 0, int MaxWidth = AppConfig.DefaultMaxWidth)
{
    private const int DefaultMaxWidth = 620;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // Hand-edited files often quote numbers or leave a trailing comma; neither should reset every setting.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Json) ?? new AppConfig();
            return config.MaxWidth > 0 ? config : config with { MaxWidth = DefaultMaxWidth };
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A broken, locked or unreadable settings file must not stop the widget from starting.
            return new AppConfig();
        }
    }
}
