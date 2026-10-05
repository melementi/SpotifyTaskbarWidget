// The top bar item: cover, song title and the current lyric line, with a menu of playback controls.

import Clutter from 'gi://Clutter';
import Gio from 'gi://Gio';
import GObject from 'gi://GObject';
import Pango from 'gi://Pango';
import St from 'gi://St';

import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import * as PopupMenu from 'resource:///org/gnome/shell/ui/popupMenu.js';

const FALLBACK_COVER = Gio.ThemedIcon.new('audio-x-generic-symbolic');
const LINE_FADE_MS = 180;
const MESSAGE_OPACITY = 150;

export const Indicator = GObject.registerClass(
class SpotifyLyricsIndicator extends PanelMenu.Button {
    _init(nowPlaying, settings, openPreferences) {
        super._init(0.5, 'Spotify Lyrics');
        this._nowPlaying = nowPlaying;
        this._settings = settings;

        const box = new St.BoxLayout({style_class: 'spotify-lyrics-box'});
        this._cover = new St.Icon({style_class: 'spotify-lyrics-cover', y_align: Clutter.ActorAlign.CENTER});
        this._title = this._label('spotify-lyrics-title');
        this._lyric = this._label('spotify-lyrics-line');
        box.add_child(this._cover);
        box.add_child(this._title);
        box.add_child(this._lyric);
        this.add_child(box);

        this._buildMenu(openPreferences);

        nowPlaying.connectObject(
            'track-changed', () => this._syncTrack(),
            'lyric-changed', () => this._syncLyric(true),
            this);
        settings.connectObject(
            'changed::title-max-width', () => this._syncWidths(),
            'changed::lyric-max-width', () => this._syncWidths(),
            this);

        this._syncWidths();
        this._syncTrack();
        this._syncLyric(false);
    }

    _label(styleClass) {
        const label = new St.Label({style_class: styleClass, y_align: Clutter.ActorAlign.CENTER});
        label.clutter_text.ellipsize = Pango.EllipsizeMode.END;
        return label;
    }

    _buildMenu(openPreferences) {
        this._details = new PopupMenu.PopupMenuItem('', {reactive: false});
        this._details.label.clutter_text.ellipsize = Pango.EllipsizeMode.END;
        this.menu.addMenuItem(this._details);

        const controls = new PopupMenu.PopupBaseMenuItem({reactive: false, can_focus: false});
        const row = new St.BoxLayout({style_class: 'spotify-lyrics-controls', x_expand: true, x_align: Clutter.ActorAlign.CENTER});
        row.add_child(this._controlButton('media-skip-backward-symbolic', 'Previous', () => this._nowPlaying.previous()));
        this._playPause = this._controlButton('media-playback-start-symbolic', 'Play or pause', () => this._nowPlaying.playPause());
        row.add_child(this._playPause);
        row.add_child(this._controlButton('media-skip-forward-symbolic', 'Next', () => this._nowPlaying.next()));
        controls.add_child(row);
        this.menu.addMenuItem(controls);

        this.menu.addMenuItem(new PopupMenu.PopupSeparatorMenuItem());
        this.menu.addAction('Open Spotify', () => this._nowPlaying.open());
        this.menu.addAction('Settings', openPreferences);
    }

    _controlButton(iconName, accessibleName, onClicked) {
        const button = new St.Button({
            style_class: 'spotify-lyrics-control-button',
            can_focus: true,
            accessible_name: accessibleName,
            child: new St.Icon({icon_name: iconName}),
        });
        button.connect('clicked', onClicked);
        return button;
    }

    _syncTrack() {
        const {track, art, playing} = this._nowPlaying;
        this.visible = track !== null;
        if (!track)
            return;

        this._cover.gicon = art ?? FALLBACK_COVER;
        this._title.text = track.title;
        this._details.label.text = [track.artist, track.album].filter(Boolean).join(' — ') || track.title;
        this._playPause.child.icon_name = playing ? 'media-playback-pause-symbolic' : 'media-playback-start-symbolic';
    }

    _syncLyric(animate) {
        const {lyric, lyricIsMessage} = this._nowPlaying;
        this._lyric.text = lyric;
        this._lyric.visible = lyric !== '';

        const opacity = lyricIsMessage ? MESSAGE_OPACITY : 255;
        this._lyric.remove_all_transitions();
        if (animate && !lyricIsMessage) {
            this._lyric.opacity = 80;
            this._lyric.ease({opacity, duration: LINE_FADE_MS, mode: Clutter.AnimationMode.EASE_OUT_QUAD});
        } else {
            this._lyric.opacity = opacity;
        }
    }

    _syncWidths() {
        this._title.style = `max-width: ${this._settings.get_int('title-max-width')}px;`;
        this._lyric.style = `max-width: ${this._settings.get_int('lyric-max-width')}px;`;
    }
});
