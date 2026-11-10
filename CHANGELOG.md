# Changelog

## 1.2.0

- The widget no longer closes on unexpected errors, such as album art it cannot decode. Errors are written to `%LOCALAPPDATA%\SpotifyTaskbarWidget\error.log` instead.
- "Lyrics unavailable" is retried after 5, 15, 30 and 60 seconds while the same song is playing.
- A locked or unreadable `config.json` no longer stops the widget from starting. Quoted numbers, comments and trailing commas in it are now accepted.
- A locked or unreadable lyrics cache file is downloaded again instead of leaving the lyrics blank.
- Lyrics found without a known song length are no longer cached, so a wrong match is not kept for good.
- The widget also hides for a fullscreen window on the main monitor when the focus is on another monitor.
- More robust handling of Spotify closing or restarting mid-song.
- Releases are now built, tested and published from the source by GitHub Actions, with a SHA-256 hash for the zip.

## 1.1.1

- Stop polling the Windows theme, and reuse the window class name buffer, to lower CPU and memory use.

## 1.1.0

- Lower resource use, more robust install and taskbar placement.

## 1.0.0

- First release: current Spotify song and synced lyrics from LRCLIB on the Windows 11 taskbar.
