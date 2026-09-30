# Spotify Taskbar Widget

A small Windows 11 widget that sits at the left end of the taskbar, where the
weather widget normally is, and shows:

- the album art, title and artist of the song Spotify is playing
- the song's lyrics, synced to the music: the previous line, the current line
  (highlighted) and the next line

It needs no Spotify login and no API key. The song comes from Windows' own
media controls, and the lyrics come from [LRCLIB](https://lrclib.net), a free
public lyrics database.

## Requirements

- Windows 11, with the taskbar at the bottom and the taskbar icons **centered**
  (the default). The widget uses the empty space left of the Start button, so
  it hides itself on a left-aligned taskbar.
- The Spotify desktop app, from either the Microsoft Store or spotify.com.
- An internet connection for lyrics.

## Install

1. Download `SpotifyTaskbarWidget-win-x64.zip` from the
   [latest release](https://github.com/melementi/SpotifyTaskbarWidget/releases/latest).
2. Open **PowerShell** (not as administrator) and run:

   ```powershell
   $dir = "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget"
   Expand-Archive "$env:USERPROFILE\Downloads\SpotifyTaskbarWidget-win-x64.zip" $dir -Force
   Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget "`"$dir\SpotifyTaskbarWidget.exe`""
   Start-Process "$dir\SpotifyTaskbarWidget.exe"
   ```

   This unpacks the widget into your user folder, makes it start when you sign
   in, and starts it now. No administrator rights are needed. If your browser
   saved the zip somewhere other than Downloads, change the path in the second
   line.

3. The exe is not code-signed, so Windows may show **"Windows protected your
   PC"** the first time. Click **More info → Run anyway**.
4. Turn off the weather widget so it does not show through underneath:
   **Settings → Personalization → Taskbar → Taskbar items → Widgets → Off**.

The download is a single self-contained exe; you do not need to install .NET.

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

Right-click the widget → **Exit**, then in PowerShell:

```powershell
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget
Remove-Item "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget", "$env:LOCALAPPDATA\SpotifyTaskbarWidget" -Recurse
```

Turn the weather widget back on in Settings if you want it.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/melementi/SpotifyTaskbarWidget
cd SpotifyTaskbarWidget
dotnet test
dotnet publish src/SpotifyTaskbarWidget -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/win-x64
```

The exe is written to `artifacts\win-x64\`. Close a running widget first, or
the new one exits immediately because only one copy may run at a time.

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
- Songs LRCLIB does not have show no lyrics.
- The taskbar's right-click menu does not open over the widget's area.

## Disclaimer

This is an unofficial hobby project. It is not affiliated with, endorsed by or
connected to Spotify AB. Lyrics are provided by LRCLIB and its contributors.

## License

[MIT](LICENSE)
