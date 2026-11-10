# Spotify Taskbar Widget

<img width="998" height="165" alt="The widget on the Windows 11 taskbar, left of the Start button" src="https://github.com/user-attachments/assets/dabdaf85-e0ae-44d7-98dc-75e8a667f86e" />

- Shows the current Spotify song at the bottom left of the taskbar, with synced lyrics.
- No Spotify login and no API key needed.
- Performance optimized (0%-0.2% CPU, 40-55 MB RAM) and needs no maintenance after the install.

## Requirements

- Windows 11 (64-bit) with the taskbar icons **centered** (the default). The widget uses the empty space left of the Start button and hides itself when there is not enough of it.
- The Spotify desktop app, from the Microsoft Store or spotify.com.
- An internet connection for lyrics.

## Install

1. Turn off the weather widget so it does not show through underneath: **Settings > Personalization > Taskbar > Taskbar items > Widgets > Off**.
2. Download `SpotifyTaskbarWidget-win-x64.zip` from the [latest release](https://github.com/melementi/SpotifyTaskbarWidget/releases/latest). There is no installer to run; the commands in the next step do the install.
3. Open **PowerShell** (not as administrator) and paste this:

   ```powershell
   Get-Process SpotifyTaskbarWidget -ErrorAction SilentlyContinue | Stop-Process -PassThru | Wait-Process; Start-Sleep 2
   $downloads = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path
   $zip = Get-ChildItem "$downloads\SpotifyTaskbarWidget-win-x64*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
   $dir = "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget"
   Expand-Archive $zip.FullName $dir -Force
   Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget "`"$dir\SpotifyTaskbarWidget.exe`""
   Start-Process "$dir\SpotifyTaskbarWidget.exe"
   ```

   This takes the newest downloaded zip from your Downloads folder, unpacks it into your user folder, makes the widget start when you sign in, and starts it now. If your browser saved the zip somewhere else, replace the third line with `$zip = Get-Item 'D:\Somewhere\SpotifyTaskbarWidget-win-x64.zip'`.

4. The app is not code-signed, so Windows may show **"Windows protected your PC"** the first time. Click **More info > Run anyway**. If Smart App Control is on (Windows Security > App & browser control), Windows blocks unsigned apps like this one and offers no way to run it.

The download contains everything the widget needs; you do not need to install .NET. Each release is built from the source by GitHub Actions and lists the zip's SHA-256 hash, so you can check your download with `Get-FileHash`.

**To update:** download the new zip and run the same commands again. They stop the running widget first, then replace it. Your settings and cached lyrics are kept.

## Use

- **Click** the widget to open Spotify.
- **Right-click** the widget and choose **Exit** to close it. It starts again at your next sign-in.
- The widget hides itself while a fullscreen app or video is on the main monitor.

| Message | Meaning |
| :--- | :--- |
| `No synced lyrics` | LRCLIB has no timed lyrics for this song. |
| `♪ Instrumental` | The song is marked instrumental. |
| `Lyrics unavailable` | LRCLIB could not be reached. The widget tries again a few times while the song plays. |
| `Spotify not playing` | Spotify is closed or has nothing loaded. |

## Settings (optional)

Create `%LOCALAPPDATA%\SpotifyTaskbarWidget\config.json` to change these, then restart the widget:

```json
{ "lyricOffsetMs": 300, "maxWidth": 620 }
```

| Key | Default | Meaning |
| :--- | :--- | :--- |
| `lyricOffsetMs` | `0` | Positive values show lyrics earlier, negative values later. |
| `maxWidth` | `620` | Widget width in pixels at 100% display scale. The widget also shrinks automatically so it never covers the Start button. |

Downloaded lyrics are cached in `%LOCALAPPDATA%\SpotifyTaskbarWidget\lyrics`. Delete that folder to download them again.

If the widget misbehaves, unexpected errors are written to `%LOCALAPPDATA%\SpotifyTaskbarWidget\error.log`. The file stays on your PC and is never sent anywhere.

## Uninstall

Paste this into PowerShell:

```powershell
Get-Process SpotifyTaskbarWidget -ErrorAction SilentlyContinue | Stop-Process -PassThru | Wait-Process; Start-Sleep 2
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' SpotifyTaskbarWidget
Remove-Item "$env:LOCALAPPDATA\Programs\SpotifyTaskbarWidget", "$env:LOCALAPPDATA\SpotifyTaskbarWidget", "$env:TEMP\.net\SpotifyTaskbarWidget" -Recurse -ErrorAction SilentlyContinue
```

Turn the weather widget back on in Settings if you want it.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/melementi/SpotifyTaskbarWidget
cd SpotifyTaskbarWidget
dotnet test
dotnet publish src/SpotifyTaskbarWidget -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o artifacts/win-x64
```

The widget is written to `artifacts\win-x64\`. Close a running widget first, or the new one exits immediately because only one copy may run at a time.

## How it works

Windows 11 has no API for adding widgets to the taskbar, so the widget is a small transparent window placed over the empty left part of the taskbar and kept above it. Spotify publishes what it is playing to Windows' media session API; the widget reads the song and playback position from there and looks up time-stamped lyrics on [LRCLIB](https://lrclib.net).

## Limits

- Spotify only, main monitor only.
- Stays hidden on a left-aligned taskbar, and on right-to-left display languages, where the taskbar is mirrored.
- Songs LRCLIB does not have show no lyrics.
- The taskbar's right-click menu does not open over the widget's area.

## Disclaimer

This is an unofficial hobby project. It is not affiliated with, endorsed by or connected to Spotify AB. Lyrics are provided by LRCLIB and its contributors.

## Changes and security

- What changed in each release: [CHANGELOG.md](CHANGELOG.md).
- Found a security problem? See [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE)
