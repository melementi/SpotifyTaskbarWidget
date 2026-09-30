using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget;

public partial class WidgetWindow : Window
{
    // Spotify raises several events per track change, and the first can still carry the old duration.
    private static readonly TimeSpan LookupDelay = TimeSpan.FromMilliseconds(400);

    // Lands a tick just after a line's timestamp, so rounding never shows the old line again.
    private static readonly TimeSpan LineMargin = TimeSpan.FromMilliseconds(15);

    private readonly PositionClock clock;
    private readonly LyricsProvider lyrics;
    private readonly TaskbarHost host;

    private readonly DispatcherTimer lyricTimer = new();
    private readonly DispatcherTimer hostTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private LyricTimeline? timeline;
    private string message = "";
    private LyricWindow shown;
    private string lyricsKey = "";
    private CancellationTokenSource? lookup;

    public event Action? ExitRequested;

    public WidgetWindow(AppConfig config, PositionClock clock, MediaWatcher watcher, LyricsProvider lyrics)
    {
        InitializeComponent();
        this.clock = clock;
        this.lyrics = lyrics;
        host = new TaskbarHost(this, config.MaxWidth);

        SourceInitialized += (_, _) =>
        {
            host.Attach();
            ApplyTheme();
        };
        hostTimer.Tick += (_, _) =>
        {
            host.Update();
            ApplyTheme();
        };
        lyricTimer.Tick += (_, _) => ShowLyrics();
        clock.Changed += () => Dispatcher.InvokeAsync(ShowLyrics);
        watcher.TrackChanged += track => Dispatcher.InvokeAsync(() => ShowTrack(track));
        MouseLeftButtonUp += (_, _) => OpenSpotify();

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        ContextMenu = new ContextMenu { Items = { exit } };

        ShowTrack(null);
        hostTimer.Start();
    }

    private void ShowTrack(TrackSnapshot? track)
    {
        if (track is null)
        {
            TitleText.Text = "Spotify not playing";
            ArtistText.Text = "";
            ArtistText.Visibility = Visibility.Collapsed;
            ArtBrush.ImageSource = null;
            lyricsKey = "";
            lookup?.Cancel();
            SetLyrics(null, "");
            return;
        }

        TitleText.Text = track.Title;
        ArtistText.Text = track.Artist;
        ArtistText.Visibility = Visibility.Visible;

        var key = $"{track.Artist}|{track.Title}|{Math.Round(track.Duration.TotalSeconds)}";
        if (track.Thumbnail is not null || key != lyricsKey) ArtBrush.ImageSource = ToImage(track.Thumbnail);
        if (key == lyricsKey) return;

        lyricsKey = key;
        _ = LoadLyricsAsync(new TrackQuery(track.Title, track.Artist, track.Duration));
    }

    private async Task LoadLyricsAsync(TrackQuery query)
    {
        lookup?.Cancel();
        lookup = new CancellationTokenSource();
        var ct = lookup.Token;
        SetLyrics(null, "");

        try
        {
            await Task.Delay(LookupDelay, ct);
            var result = await lyrics.GetAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            switch (result.Status)
            {
                case LyricsStatus.Synced:
                    SetLyrics(new LyricTimeline(LrcParser.Parse(result.SyncedLyrics)), "");
                    break;
                case LyricsStatus.Instrumental:
                    SetLyrics(null, "♪ Instrumental");
                    break;
                case LyricsStatus.NotFound:
                    SetLyrics(null, "No synced lyrics");
                    break;
                default:
                    SetLyrics(null, "Lyrics unavailable");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SetLyrics(LyricTimeline? newTimeline, string newMessage)
    {
        timeline = newTimeline;
        message = newMessage;
        ShowLyrics();
    }

    private void ShowLyrics()
    {
        var now = DateTimeOffset.UtcNow;
        var position = clock.Now(now);
        ScheduleNextLine(position, now);

        var window = timeline is null ? new LyricWindow("", message, "") : timeline.At(position);
        if (window == shown) return;

        var lineChanged = window.Current != shown.Current;
        shown = window;
        PreviousText.Text = window.Previous;
        CurrentText.Text = window.Current;
        NextText.Text = window.Next;

        if (lineChanged)
            LyricsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void ScheduleNextLine(TimeSpan position, DateTimeOffset now)
    {
        lyricTimer.Stop();
        // While paused nothing moves; the clock's Changed event wakes the widget on resume or seek.
        if (timeline?.NextChangeAfter(position) is not { } next || clock.Until(next, now) is not { } wait) return;
        lyricTimer.Interval = wait + LineMargin;
        lyricTimer.Start();
    }

    private static void OpenSpotify()
    {
        try
        {
            // The spotify: protocol is registered by both the Microsoft Store and the spotify.com installer.
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // Spotify is not installed; there is nothing to open.
        }
    }

    private static BitmapImage? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException)
        {
            // Players can hand over artwork WPF cannot decode; the song and lyrics still matter more.
            return null;
        }
    }

    private void ApplyTheme()
    {
        var light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme",
            0) is 1;
        Foreground = light ? Brushes.Black : Brushes.White;
    }
}
