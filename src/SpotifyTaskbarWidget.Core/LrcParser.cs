using System.Text.RegularExpressions;

namespace SpotifyTaskbarWidget.Core;

public static partial class LrcParser
{
    [GeneratedRegex(@"\G\s*\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex Timestamp();

    public static IReadOnlyList<LyricLine> Parse(string? lrc)
    {
        var lines = new List<LyricLine>();
        if (string.IsNullOrEmpty(lrc)) return lines;

        foreach (var raw in lrc.Split('\n'))
        {
            var row = raw.TrimEnd('\r');
            var stamps = Timestamp().Matches(row);
            if (stamps.Count == 0) continue;

            var last = stamps[^1];
            var text = row[(last.Index + last.Length)..].Trim();
            if (text.Length == 0) text = "♪";

            foreach (Match stamp in stamps)
                lines.Add(new LyricLine(ToTime(stamp), text));
        }

        return lines.OrderBy(line => line.Time).ToList();
    }

    private static TimeSpan ToTime(Match stamp)
    {
        var minutes = int.Parse(stamp.Groups[1].Value);
        var seconds = int.Parse(stamp.Groups[2].Value);
        var milliseconds = stamp.Groups[3].Success ? int.Parse(stamp.Groups[3].Value.PadRight(3, '0')) : 0;
        return new TimeSpan(0, 0, minutes, seconds, milliseconds);
    }
}
