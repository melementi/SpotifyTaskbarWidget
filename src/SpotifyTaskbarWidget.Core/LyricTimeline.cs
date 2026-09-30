namespace SpotifyTaskbarWidget.Core;

public readonly record struct LyricWindow(string Previous, string Current, string Next);

public sealed class LyricTimeline(IReadOnlyList<LyricLine> lines)
{
    public LyricWindow At(TimeSpan position)
    {
        var index = IndexAt(position);
        return new LyricWindow(TextAt(index - 1), TextAt(index), TextAt(index + 1));
    }

    private int IndexAt(TimeSpan position)
    {
        int low = 0, high = lines.Count - 1, found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (lines[middle].Time <= position)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }
        return found;
    }

    private string TextAt(int index) => index >= 0 && index < lines.Count ? lines[index].Text : "";
}
