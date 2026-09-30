using System.Text.RegularExpressions;

namespace SpotifyTaskbarWidget.Core;

public static partial class TitleCleaner
{
    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Bracketed();

    public static string Clean(string title)
    {
        var dash = title.IndexOf(" - ", StringComparison.Ordinal);
        var head = dash > 0 ? title[..dash] : title;
        var cleaned = Bracketed().Replace(head, "").Trim();
        return cleaned.Length == 0 ? title : cleaned;
    }

    public static string PrimaryArtist(string artist)
    {
        var cut = artist.IndexOfAny([',', ';']);
        return cut > 0 ? artist[..cut].Trim() : artist;
    }
}
