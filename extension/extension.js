import GLib from 'gi://GLib';

import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';

import {Indicator} from './lib/indicator.js';
import {NowPlaying} from './lib/nowPlaying.js';

export default class SpotifyTopBarLyrics extends Extension {
    enable() {
        this._settings = this.getSettings();
        this._nowPlaying = new NowPlaying({
            userAgent: `SpotifyTopBarLyrics/${this.metadata['version-name']} (${this.metadata.url})`,
            cacheDirectory: GLib.build_filenamev([GLib.get_user_cache_dir(), 'spotify-top-bar-lyrics', 'lyrics']),
            offsetMs: this._settings.get_int('lyric-offset-ms'),
        });

        this._settings.connectObject(
            'changed::lyric-offset-ms', () => {
                this._nowPlaying.offsetMs = this._settings.get_int('lyric-offset-ms');
            },
            'changed::panel-position', () => this._placeIndicator(),
            'changed::panel-index', () => this._placeIndicator(),
            this);
        this._placeIndicator();
    }

    disable() {
        this._settings.disconnectObject(this);
        this._indicator?.destroy();
        this._indicator = null;
        this._nowPlaying.destroy();
        this._nowPlaying = null;
        this._settings = null;
    }

    _placeIndicator() {
        // The panel has no API to move an item, so a new position means a new item; the song state stays.
        this._indicator?.destroy();
        this._indicator = new Indicator(this._nowPlaying, this._settings, () => this.openPreferences());
        Main.panel.addToStatusArea(
            this.uuid,
            this._indicator,
            this._settings.get_int('panel-index'),
            this._settings.get_string('panel-position'));
    }
}
