# Spotify Taskbar Widget

![The widget on the Windows 11 taskbar, left of the Start button](docs/screenshot.png)

- Displays current song bottom left on task bar with lyrics. 
- Turn off widgets in task bar settings before install.
- installing the Windows Installer (https://github.com/melementi/SpotifyTaskbarWidget/releases/tag/v1.1.1)
and running it should work, if it dosnt follow the stepts below.
- its performance optimized, dosnt add any resource load on cpu, and dosnt require any maintance after innitial install. plug and play.

## Install

1. Download `SpotifyTaskbarWidget-win-x64.zip` from the
   [latest release](https://github.com/melementi/SpotifyTaskbarWidget/releases/latest).
2. Open **PowerShell** (not as administrator) and run:

   ```powershell
   Get-Process SpotifyTaskbarWidget -ErrorAction SilentlyContinue | Stop-Process -PassThru | Wait-Process; Start-Sleep 2
   $downloads = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path
   $zip = Get-ChildItem "$downloads\SpotifyTaskbarWidget-win-x64*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
   $dir = "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget"
   Expand-Archive $zip.FullName $dir -Force
   Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget "`"$dir\SpotifyTaskbarWidget.exe`""
   Start-Process "$dir\SpotifyTaskbarWidget.exe"
   ```

   This stops the widget if it is already running, takes the newest
   downloaded zip from your Downloads folder (even if that folder was moved,
   for example to OneDrive), unpacks it into your user folder, makes the widget
   start when you sign in, and starts it now. No administrator rights are
   needed. If your browser saved the zip somewhere else, replace the third
   line with `$zip = Get-Item 'D:\Somewhere\SpotifyTaskbarWidget-win-x64.zip'`.

3. The app is not code-signed, so Windows may show **"Windows protected your
   PC"** the first time. Click **More info → Run anyway**. If Smart App Control
   is on (Windows Security → App & browser control), Windows blocks unsigned
   apps like this one and offers no way to run it.
4. Turn off the weather widget so it does not show through underneath:
   **Settings → Personalization → Taskbar → Taskbar items → Widgets → Off**.

The download contains everything the widget needs; you do not need to install
.NET.

### Update

Download the new zip and run the same PowerShell commands again. They stop the
running widget first, then replace it. Your settings and cached lyrics are kept.

## Use

- **Click** the widget to open Spotify.
- **Right-click** the widget and choose **Exit** to close it. It starts again at
  your next sign-in.
- The widget hides itself while a fullscreen app or video is on the main
  monitor.

Lyric messages you may see:

| Message | Meaning |
|---|---|
| `No synced lyrics` | LRCLIB has no timed lyrics for this song. |
| `♪ Instrumental` | The song is marked instrumental. |
| `Lyrics unavailable` | LRCLIB could not be reached; the next song tries again. |
| `Spotify not playing` | Spotify is closed or has nothing loaded. |

## Settings

Create `%LOCALAPPDATA%\SpotifyTaskbarWidget\config.json` to change these, then
restart the widget:

```json
{ "lyricOffsetMs": 300, "maxWidth": 620 }
```

| Key | Default | Meaning |
|---|---|---|
| `lyricOffsetMs` | `0` | Positive values show lyrics earlier, negative values later. |
| `maxWidth` | `620` | Widget width in pixels at 100% display scale. The widget also shrinks automatically so it never covers the Start button. |

Downloaded lyrics are cached in `%LOCALAPPDATA%\SpotifyTaskbarWidget\lyrics`.
Delete that folder to download them again.

## Uninstall

In PowerShell:

```powershell
Get-Process SpotifyTaskbarWidget -ErrorAction SilentlyContinue | Stop-Process -PassThru | Wait-Process; Start-Sleep 2
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget
Remove-Item "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget", "$env:LOCALAPPDATA\SpotifyTaskbarWidget", "$env:TEMP\.net\SpotifyTaskbarWidget" -Recurse -ErrorAction SilentlyContinue
```

The last folder only exists if you ever ran version 1.0.0.

Turn the weather widget back on in Settings if you want it.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/melementi/SpotifyTaskbarWidget
cd SpotifyTaskbarWidget
dotnet test
dotnet publish src/SpotifyTaskbarWidget -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o artifacts/win-x64
```

The widget is written to `artifacts\win-x64\`: the exe plus a few native
libraries it loads from the same folder. Close a running widget first, or the
new one exits immediately because only one copy may run at a time.

Project layout:

- `src/SpotifyTaskbarWidget.Core` — lyric parsing and timing, LRCLIB client,
  cache and settings. No Windows dependencies; covered by the tests.
- `src/SpotifyTaskbarWidget` — the WPF widget: taskbar placement, Windows
  media session watcher and UI.
- `tests/SpotifyTaskbarWidget.Tests` — xUnit tests.

## How it works

Windows 11 has no API for adding widgets to the taskbar, so the widget is a
small transparent window placed over the empty left part of the taskbar and
kept above it. Spotify publishes what it is playing to Windows' media session
API; the widget reads the song and playback position from there and looks up
time-stamped lyrics on LRCLIB.

## Limits

- Spotify only, main monitor only.
- Stays hidden on right-to-left display languages, where the taskbar is
  mirrored.
- Songs LRCLIB does not have show no lyrics.
- The taskbar's right-click menu does not open over the widget's area.

## Disclaimer

This is an unofficial hobby project. It is not affiliated with, endorsed by or
connected to Spotify AB. Lyrics are provided by LRCLIB and its contributors.

## License

[MIT](LICENSE)
