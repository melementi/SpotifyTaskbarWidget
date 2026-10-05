# Spotify Taskbar Widget

![The widget on the Windows 11 taskbar, left of the Start button](docs/screenshot.png)

- Displays current song bottom left on task bar with lyrics. 
- Turn off widgets in task bar settings before install.
- installing the Windows Installer (https://github.com/melementi/SpotifyTaskbarWidget/releases/tag/v1.1.1)
and running it should work, if it dosnt follow the stepts below.
- its performance optimized, dosnt add any resource load on cpu, and dosnt require any maintance after innitial install.

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

## To uninstall paste this into powerShell:

```powershell
Get-Process SpotifyTaskbarWidget -ErrorAction SilentlyContinue | Stop-Process -PassThru | Wait-Process; Start-Sleep 2
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget
Remove-Item "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget", "$env:LOCALAPPDATA\SpotifyTaskbarWidget", "$env:TEMP\.net\SpotifyTaskbarWidget" -Recurse -ErrorAction SilentlyContinue
```

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
