import Adw from 'gi://Adw';
import Gio from 'gi://Gio';
import Gtk from 'gi://Gtk';

import {ExtensionPreferences} from 'resource:///org/gnome/Shell/Extensions/js/extensions/prefs.js';

const POSITIONS = ['left', 'center', 'right'];

export default class SpotifyTopBarLyricsPreferences extends ExtensionPreferences {
    fillPreferencesWindow(window) {
        const settings = this.getSettings();
        const page = new Adw.PreferencesPage();
        window.add(page);

        const placement = new Adw.PreferencesGroup({title: 'Placement'});
        page.add(placement);

        const position = new Adw.ComboRow({
            title: 'Top bar section',
            model: Gtk.StringList.new(['Left', 'Center', 'Right']),
            selected: Math.max(0, POSITIONS.indexOf(settings.get_string('panel-position'))),
        });
        position.connect('notify::selected', () => settings.set_string('panel-position', POSITIONS[position.selected]));
        placement.add(position);

        placement.add(spinRow(settings, 'panel-index', 'Place within the section', '0 is first, -1 is last', 1));

        const size = new Adw.PreferencesGroup({title: 'Size'});
        page.add(size);
        size.add(spinRow(settings, 'title-max-width', 'Widest song title', 'Pixels; longer titles are shortened', 10));
        size.add(spinRow(settings, 'lyric-max-width', 'Widest lyric line', 'Pixels; longer lines are shortened', 10));

        const timing = new Adw.PreferencesGroup({title: 'Lyrics'});
        page.add(timing);
        timing.add(spinRow(settings, 'lyric-offset-ms', 'Lyric offset',
            'Milliseconds; positive values show lyrics earlier, negative values later', 50));
    }
}

function spinRow(settings, key, title, subtitle, step) {
    const range = settings.settingsSchema.get_key(key).get_range().deepUnpack()[1].deepUnpack();
    const row = new Adw.SpinRow({
        title,
        subtitle,
        adjustment: new Gtk.Adjustment({lower: range[0], upper: range[1], step_increment: step, page_increment: step * 10}),
    });
    settings.bind(key, row, 'value', Gio.SettingsBindFlags.DEFAULT);
    return row;
}
