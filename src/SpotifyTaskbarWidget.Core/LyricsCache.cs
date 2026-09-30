using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpotifyTaskbarWidget.Core;

public sealed class LyricsCache(string directory)
{
    public static string KeyFor(TrackQuery query)
    {
        var seconds = (long)Math.Round(query.Duration.TotalSeconds);
        var text = $"{query.Artist.Trim().ToLowerInvariant()}|{query.Title.Trim().ToLowerInvariant()}|{seconds}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public LyricsResult? TryGet(TrackQuery query)
    {
        var path = PathFor(query);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<LyricsResult>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Put(TrackQuery query, LyricsResult result)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(PathFor(query), JsonSerializer.Serialize(result));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The cache only saves a network request; a locked or unwritable folder must not cost the lyrics.
        }
    }

    private string PathFor(TrackQuery query) => Path.Combine(directory, KeyFor(query) + ".json");
}
