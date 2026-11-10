using System.IO;

namespace SpotifyTaskbarWidget;

// The widget has no console and no window to report into; without this file a failure leaves no trace.
internal static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpotifyTaskbarWidget",
        "error.log");

    public static void Write(string where, Exception exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var file = new FileInfo(Path);
                if (file.Exists && file.Length > MaxBytes) file.Delete();
                File.AppendAllText(Path, $"{DateTimeOffset.Now:u} {where}: {exception}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging is the last resort; if it fails too there is nowhere left to report to.
        }
    }
}
