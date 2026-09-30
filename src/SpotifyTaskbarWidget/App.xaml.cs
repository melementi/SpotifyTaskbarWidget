using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SpotifyTaskbarWidget.Core;

namespace SpotifyTaskbarWidget;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    private bool exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // The widget is a small layered window; GPU rendering would copy every frame back to system memory
        // and keep the graphics driver loaded, which costs more than drawing it on the CPU.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

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
        var version = typeof(App).Assembly.GetName().Version!.ToString(3);
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"SpotifyTaskbarWidget/{version} (https://github.com/melementi/SpotifyTaskbarWidget)");
        var provider = new LyricsProvider(new LyricsCache(Path.Combine(dataFolder, "lyrics")), new LrclibClient(http));
        var watcher = new MediaWatcher(clock);

        var window = new WidgetWindow(config, clock, watcher, provider);
        // Exit, taskkill and sign-out all ask the window to close first; only a window destroyed
        // without being asked (its taskbar owner went away) needs the widget to start again.
        window.Closing += (_, _) => exiting = true;
        window.Closed += (_, _) =>
        {
            if (!exiting) Relaunch();
        };
        window.ExitRequested += () =>
        {
            exiting = true;
            Shutdown();
        };
        // Showing is left to the host timer, which first checks that the widget fits and nothing is fullscreen.
        new WindowInteropHelper(window).EnsureHandle();
        await watcher.StartAsync();
    }

    // Some Windows builds destroy owned windows together with the taskbar when explorer restarts.
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
