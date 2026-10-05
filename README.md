# Spotify Top Bar Lyrics (Ubuntu / GNOME)

![The song in the Ubuntu top bar: cover, title and the current lyric line](docs/screenshot.png)

*Shown with placeholder song and lyric text.*

> This branch is the Ubuntu / GNOME version. The Windows 11 taskbar widget is on the
> [`main` branch](https://github.com/melementi/SpotifyTaskbarWidget/tree/main).

A GNOME Shell extension that puts the song Spotify is playing into the Ubuntu top bar:

- the album cover and the song title
- beside them, one line of the song's lyrics: the line being sung right now, synced to the music

It needs no Spotify login and no API key. The song comes from Spotify's own media controls (MPRIS), and the
lyrics come from [LRCLIB](https://lrclib.net), a free public lyrics database.

Click it for the artist and album, previous / play-pause / next buttons, **Open Spotify** and **Settings**.

![The menu with artist, album and playback buttons](docs/menu.png)

## Requirements

- Ubuntu 23.10 or newer with the standard Ubuntu desktop, or any Linux desktop running GNOME Shell 45 to 49.
  Ubuntu 24.04 LTS (GNOME 46), 24.10, 25.04 and 25.10 all qualify. Ubuntu 22.04 (GNOME 42) is too old.
- The Spotify desktop app, from the App Center (Snap), Flathub or Spotify's own `.deb` repository.
- An internet connection for lyrics and covers.

## Install

1. Download this branch: on GitHub choose **Code → Download ZIP** while viewing the
   `Ubuntu-Gnome-top-bar-version` branch, and unzip it. Or with git:

   ```sh
   git clone -b Ubuntu-Gnome-top-bar-version https://github.com/melementi/SpotifyTaskbarWidget
   ```

2. In a terminal, in that folder, run:

   ```sh
   sh install.sh
   ```

   This builds the extension, installs it into `~/.local/share/gnome-shell/extensions` and turns it on.
   No administrator rights are needed.

3. **Log out and back in.** GNOME only loads newly installed extensions at login.

Start Spotify and play a song; it appears at the left of the top bar, next to the workspace indicator. The item
hides itself whenever Spotify is closed or has nothing loaded.

### Update

Download the new version and run `sh install.sh` again, then log out and back in. Your settings and cached
lyrics are kept.

## Settings

Open them from the item's menu (**Settings**), or from the **Extensions** app.

| Setting | Default | Meaning |
|---|---|---|
| Top bar section | Left | Left (after the workspace indicator), center (beside the clock) or right (beside the system menu). |
| Place within the section | -1 | 0 puts the song first in its section, -1 last. |
| Widest song title | 200 | In pixels. Longer titles end in "…". |
| Widest lyric line | 420 | In pixels. Longer lines end in "…". |
| Lyric offset | 0 | In milliseconds. Positive values show lyrics earlier, negative values later. |

Lyric messages you may see in place of a line:

| Message | Meaning |
|---|---|
| `No synced lyrics` | LRCLIB has no timed lyrics for this song. |
| `♪ Instrumental` | The song is marked instrumental. |
| `Lyrics unavailable` | LRCLIB could not be reached; the next song tries again. |

Downloaded lyrics are cached in `~/.cache/spotify-top-bar-lyrics/lyrics`. Delete that folder to download them
again.

## Uninstall

```sh
gnome-extensions uninstall spotify-top-bar-lyrics@melementi.github.io
rm -rf ~/.cache/spotify-top-bar-lyrics
```

## Develop

The extension is plain JavaScript (GJS, ES modules); there is nothing to compile.

- `extension/extension.js` — entry point: creates the model and puts the item in the top bar.
- `extension/lib/nowPlaying.js` — follows Spotify over D-Bus (MPRIS), keeps the playback clock and picks the
  lyric line for the moment.
- `extension/lib/indicator.js` — the top bar item and its menu.
- `extension/lib/services.js` — HTTP (libsoup), the lyrics cache on disk and cover downloads.
- `extension/lib/lyrics.js`, `lrclib.js`, `clock.js`, `metadata.js` — LRC parsing, LRCLIB matching, playback
  timing and metadata reading. They have no GNOME imports, so the tests run them under Node.
- `extension/prefs.js` and `extension/schemas/` — the settings window and its keys.

Run the tests (Node.js 20 or newer):

```sh
npm test
```

To try a change, run `sh install.sh` and log out and back in. To watch the extension's log:

```sh
journalctl -f -o cat /usr/bin/gnome-shell
```

## How it works

Spotify on Linux publishes what it plays on the session D-Bus as `org.mpris.MediaPlayer2.spotify`. The
extension watches that name for the title, artist, length, cover address and play state. MPRIS does not
announce the playback position as it advances, so the extension asks for it on track changes, pauses and seeks,
and every few seconds while lyrics are showing, and counts forward in between. The line changes are timed to
the millisecond rather than polled.

Lyrics are looked up on LRCLIB by title, artist and length; when there is no exact match it searches, then
retries with the title stripped of suffixes such as "- Remastered 2011" and with only the first artist.

## Privacy

- No telemetry, no account, no API key.
- LRCLIB receives the title, artist and length of each new song, over HTTPS.
- Covers are downloaded from Spotify's image server (`i.scdn.co`), the address Spotify itself reports.
- Lyrics found are cached on disk under `~/.cache/spotify-top-bar-lyrics`; covers are only kept in memory.

## Limits

- Spotify only.
- Shows on the main monitor's top bar only.
- Songs LRCLIB does not have show no lyrics.
- It is hidden on the lock screen.

## Disclaimer

This is an unofficial hobby project. It is not affiliated with, endorsed by or connected to Spotify AB. Lyrics
are provided by LRCLIB and its contributors.

## License

[MIT](LICENSE)
