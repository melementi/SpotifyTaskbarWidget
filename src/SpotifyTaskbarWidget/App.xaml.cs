using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    private bool exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        instance = new Mutex(initiallyOwned: true, @"Local\SpotifyTaskbarWidget", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        // Signing out closes the window too; that must not count as "explorer restarted".
        SessionEnding += (_, _) => exiting = true;

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpotifyTaskbarWidget");
        Directory.CreateDirectory(dataFolder);
        var config = AppConfig.Load(Path.Combine(dataFolder, "config.json"));

        // At login, and after an explorer restart, the taskbar may not exist yet.
        while (NativeMethods.FindWindow("Shell_TrayWnd", null) == IntPtr.Zero)
            await Task.Delay(500);

        var clock = new PositionClock(TimeSpan.FromMilliseconds(config.LyricOffsetMs));
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://lrclib.net/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SpotifyTaskbarWidget/1.0 (https://github.com/melementi/SpotifyTaskbarWidget)");
        var provider = new LyricsProvider(new LyricsCache(Path.Combine(dataFolder, "lyrics")), new LrclibClient(http));
        var watcher = new MediaWatcher(clock);

        var window = new WidgetWindow(config, clock, watcher, provider);
        window.Closed += (_, _) =>
        {
            if (!exiting) Relaunch();
        };
        window.ExitRequested += () =>
        {
            exiting = true;
            Shutdown();
        };
        window.Show();
        await watcher.StartAsync();
    }

    // Windows destroys the widget window together with its owner when explorer restarts.
    private void Relaunch()
    {
        exiting = true;
        instance!.ReleaseMutex();
        // The new process treats an existing mutex as "already running", so the handle must be gone before it starts.
        instance.Dispose();
        Process.Start(Environment.ProcessPath!);
        Shutdown();
    }
}
