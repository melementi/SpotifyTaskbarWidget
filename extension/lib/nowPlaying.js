// What Spotify is playing, read over MPRIS (D-Bus), and the lyric line for the current moment.

import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Shell from 'gi://Shell';

import {EventEmitter} from 'resource:///org/gnome/shell/misc/signals.js';

import {PositionClock} from './clock.js';
import {LrclibClient, LyricsProvider} from './lrclib.js';
import {LyricTimeline, parseLrc} from './lyrics.js';
import {trackFromMetadata} from './metadata.js';
import {Http, LyricsCache, loadArtwork} from './services.js';

Gio._promisify(Gio.DBusConnection.prototype, 'call');

// The Spotify desktop app owns this name whether it came from the .deb, the Snap or the Flatpak.
const BUS_NAME = 'org.mpris.MediaPlayer2.spotify';
const OBJECT_PATH = '/org/mpris/MediaPlayer2';
const PLAYER_INTERFACE = 'org.mpris.MediaPlayer2.Player';
const DESKTOP_IDS = ['spotify.desktop', 'spotify_spotify.desktop', 'com.spotify.Client.desktop'];

const PlayerProxy = Gio.DBusProxy.makeProxyWrapper(`
<node>
  <interface name="${PLAYER_INTERFACE}">
    <method name="PlayPause"/>
    <method name="Next"/>
    <method name="Previous"/>
    <signal name="Seeked"><arg name="Position" type="x"/></signal>
    <property name="PlaybackStatus" type="s" access="read"/>
    <property name="Rate" type="d" access="read"/>
    <property name="Metadata" type="a{sv}" access="read"/>
  </interface>
</node>`);

// Spotify can announce a new song in several steps, the first still carrying the old length.
const LOOKUP_DELAY_MS = 400;

// Lands a tick just after a line's timestamp, so rounding never shows the old line again.
const LINE_MARGIN_MS = 15;

// MPRIS does not announce the position as it advances, so it is read again now and then to catch drift.
const POSITION_POLL_SECONDS = 3;

/**
 * Signals: 'track-changed' (track, art or playing state), 'lyric-changed'.
 */
export class NowPlaying extends EventEmitter {
    constructor({userAgent, cacheDirectory, offsetMs}) {
        super();

        /** @type {{title: string, artist: string, album: string, durationMs: number, artUrl: string} | null} */
        this.track = null;
        /** @type {Gio.Icon | null} */
        this.art = null;
        this.playing = false;
        /** The lyric line playing now, or a status message such as "No synced lyrics". */
        this.lyric = '';
        this.lyricIsMessage = false;

        this._clock = new PositionClock(offsetMs);
        this._http = new Http(userAgent);
        this._lyrics = new LyricsProvider(
            new LyricsCache(cacheDirectory),
            new LrclibClient(path => this._http.lrclib(path)));
        this._cancellable = new Gio.Cancellable();

        this._proxy = null;
        this._timeline = null;
        this._message = '';
        this._songKey = '';
        this._lyricsKey = '';
        this._artUrl = '';
        this._lookupGeneration = 0;
        this._artGeneration = 0;
        this._lookupTimer = 0;
        this._lineTimer = 0;
        this._pollTimer = 0;

        this._watchId = Gio.bus_watch_name(Gio.BusType.SESSION, BUS_NAME, Gio.BusNameWatcherFlags.NONE,
            () => this._connect(),
            () => this._disconnect());
    }

    set offsetMs(offsetMs) {
        this._clock.offsetMs = offsetMs;
        this._showLyric();
    }

    playPause() {
        this._proxy?.PlayPauseAsync().catch(() => {});
    }

    next() {
        this._proxy?.NextAsync().catch(() => {});
    }

    previous() {
        this._proxy?.PreviousAsync().catch(() => {});
    }

    /** Brings the Spotify window to the front, or starts Spotify. */
    open() {
        const apps = Shell.AppSystem.get_default();
        const app = apps.get_running().find(candidate => /spotify/i.test(`${candidate.get_id()} ${candidate.get_name()}`)) ??
            DESKTOP_IDS.map(id => apps.lookup_app(id)).find(Boolean);
        app?.activate();
    }

    destroy() {
        this.disconnectAll();
        Gio.bus_unwatch_name(this._watchId);
        this._cancellable.cancel();
        this._disconnect();
        this._removeSource('_lineTimer');
        this._removeSource('_pollTimer');
        this._http.destroy();
    }

    _connect() {
        this._disconnect();
        const proxy = new PlayerProxy(Gio.DBus.session, BUS_NAME, OBJECT_PATH, (_proxy, error) => {
            if (error || proxy !== this._proxy)
                return;
            proxy.connectObject('g-properties-changed', (_p, changed) => this._onPropertiesChanged(changed), this);
            this._seekedId = proxy.connectSignal('Seeked', (_p, _sender, [position]) => {
                if (this._clock.sync(position / 1000, monotonicMs(), true))
                    this._showLyric();
            });
            this._readPlayback();
            this._readTrack();
        }, this._cancellable);
        this._proxy = proxy;
    }

    _disconnect() {
        if (this._proxy) {
            this._proxy.disconnectObject(this);
            if (this._seekedId)
                this._proxy.disconnectSignal(this._seekedId);
            this._seekedId = 0;
            this._proxy = null;
        }
        this.playing = false;
        this._clock.setPlayback(false, 1, monotonicMs());
        this._setTrack(null);
    }

    _onPropertiesChanged(changed) {
        const keys = Object.keys(changed.deepUnpack());
        if (keys.includes('PlaybackStatus') || keys.includes('Rate'))
            this._readPlayback();
        if (keys.includes('Metadata'))
            this._readTrack();
    }

    _readTrack() {
        this._setTrack(trackFromMetadata(this._proxy?.get_cached_property('Metadata')?.recursiveUnpack()));
    }

    _readPlayback() {
        const status = this._proxy?.get_cached_property('PlaybackStatus')?.unpack();
        const rate = this._proxy?.get_cached_property('Rate')?.unpack() ?? 1;
        const playing = status === 'Playing';
        const changed = playing !== this.playing;
        this.playing = playing;

        if (this._clock.setPlayback(playing, rate, monotonicMs()))
            this._showLyric();
        // Pausing, resuming and seeking can all shift the position; ask rather than guess.
        this._queryPosition(true);
        this._updatePolling();
        if (changed)
            this.emit('track-changed');
    }

    _setTrack(track) {
        if (sameTrack(track, this.track))
            return;
        this.track = track;

        if (!track) {
            this._songKey = '';
            this._lyricsKey = '';
            this._loadArt('');
            this._cancelLookup();
            this._setLyrics(null, '');
            this.emit('track-changed');
            return;
        }

        const songKey = `${track.artist}|${track.title}`;
        if (songKey !== this._songKey) {
            this._songKey = songKey;
            this._clock.sync(0, monotonicMs(), true);
            this._queryPosition(true);
        }
        this._clock.setDuration(track.durationMs);
        this._loadArt(track.artUrl);
        this.emit('track-changed');

        const lyricsKey = `${songKey}|${Math.round(track.durationMs / 1000)}`;
        if (lyricsKey === this._lyricsKey)
            return;
        this._lyricsKey = lyricsKey;
        this._cancelLookup();
        this._setLyrics(null, '');

        const query = {title: track.title, artist: track.artist, durationMs: track.durationMs};
        const generation = this._lookupGeneration;
        this._lookupTimer = GLib.timeout_add(GLib.PRIORITY_DEFAULT, LOOKUP_DELAY_MS, () => {
            this._lookupTimer = 0;
            this._lookUp(query, generation);
            return GLib.SOURCE_REMOVE;
        });
    }

    async _lookUp(query, generation) {
        const result = await this._lyrics.get(query);
        if (generation !== this._lookupGeneration)
            return;

        switch (result.status) {
        case 'synced':
            this._setLyrics(new LyricTimeline(parseLrc(result.syncedLyrics)), '');
            break;
        case 'instrumental':
            this._setLyrics(null, '♪ Instrumental');
            break;
        case 'notFound':
            this._setLyrics(null, 'No synced lyrics');
            break;
        default:
            this._setLyrics(null, 'Lyrics unavailable');
        }
    }

    _cancelLookup() {
        this._lookupGeneration++;
        this._removeSource('_lookupTimer');
    }

    async _loadArt(url) {
        if (url === this._artUrl)
            return;
        this._artUrl = url;
        const generation = ++this._artGeneration;

        // The old cover must not stay up next to a new song while the new one downloads.
        if (this.art) {
            this.art = null;
            this.emit('track-changed');
        }
        if (!url)
            return;

        const art = await loadArtwork(this._http, url);
        if (generation !== this._artGeneration || !art)
            return;
        this.art = art;
        this.emit('track-changed');
    }

    _setLyrics(timeline, message) {
        this._timeline = timeline;
        this._message = message;
        this._updatePolling();
        this._showLyric();
    }

    _showLyric() {
        const now = monotonicMs();
        const position = this._clock.now(now);
        this._scheduleNextLine(position, now);

        const lyric = this._timeline ? this._timeline.lineAt(position) : this._message;
        const isMessage = !this._timeline;
        if (lyric === this.lyric && isMessage === this.lyricIsMessage)
            return;
        this.lyric = lyric;
        this.lyricIsMessage = isMessage;
        this.emit('lyric-changed');
    }

    _scheduleNextLine(position, now) {
        this._removeSource('_lineTimer');
        // While paused nothing moves; the playback change wakes the lyrics on resume.
        const next = this._timeline?.nextChangeAfter(position);
        const wait = next === null || next === undefined ? null : this._clock.until(next, now);
        if (wait === null)
            return;
        this._lineTimer = GLib.timeout_add(GLib.PRIORITY_DEFAULT, Math.ceil(wait + LINE_MARGIN_MS), () => {
            this._lineTimer = 0;
            this._showLyric();
            return GLib.SOURCE_REMOVE;
        });
    }

    _updatePolling() {
        const wanted = this._timeline !== null && this._clock.playing && this._proxy !== null;
        if (!wanted) {
            this._removeSource('_pollTimer');
        } else if (!this._pollTimer) {
            this._pollTimer = GLib.timeout_add_seconds(GLib.PRIORITY_DEFAULT, POSITION_POLL_SECONDS, () => {
                this._queryPosition(false);
                return GLib.SOURCE_CONTINUE;
            });
        }
    }

    async _queryPosition(force) {
        const proxy = this._proxy;
        if (!proxy)
            return;

        try {
            const reply = await Gio.DBus.session.call(
                BUS_NAME, OBJECT_PATH, 'org.freedesktop.DBus.Properties', 'Get',
                new GLib.Variant('(ss)', [PLAYER_INTERFACE, 'Position']), new GLib.VariantType('(v)'),
                Gio.DBusCallFlags.NONE, 1000, this._cancellable);
            if (proxy !== this._proxy)
                return;
            const [microseconds] = reply.recursiveUnpack();
            if (this._clock.sync(Number(microseconds) / 1000, monotonicMs(), force))
                this._showLyric();
        } catch {
            // Spotify is quitting, or never answered; its next report or the next poll catches up.
        }
    }

    _removeSource(field) {
        if (this[field]) {
            GLib.source_remove(this[field]);
            this[field] = 0;
        }
    }
}

function sameTrack(a, b) {
    return a === b || (a !== null && b !== null &&
        a.title === b.title && a.artist === b.artist && a.album === b.album &&
        a.durationMs === b.durationMs && a.artUrl === b.artUrl);
}

function monotonicMs() {
    return GLib.get_monotonic_time() / 1000;
}
