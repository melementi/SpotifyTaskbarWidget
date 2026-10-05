// The GNOME side of networking and storage: HTTP through libsoup, the lyrics cache on disk, cover art.

import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Soup from 'gi://Soup?version=3.0';

import {cacheKeyText} from './lrclib.js';
import {ArtworkCache, artworkUrls} from './metadata.js';

Gio._promisify(Soup.Session.prototype, 'send_and_read_async');
Gio._promisify(Gio.File.prototype, 'load_contents_async');
Gio._promisify(Gio.File.prototype, 'replace_contents_bytes_async', 'replace_contents_finish');

const LRCLIB = 'https://lrclib.net/';
const TIMEOUT_SECONDS = 10;

export class Http {
    constructor(userAgent) {
        this._session = new Soup.Session({user_agent: userAgent, timeout: TIMEOUT_SECONDS});
        this._cancellable = new Gio.Cancellable();
        this._decoder = new TextDecoder();
    }

    /** GETs a path below https://lrclib.net/. */
    async lrclib(path) {
        const {status, bytes} = await this._get(LRCLIB + path);
        return {status, body: status === Soup.Status.OK ? JSON.parse(this._decoder.decode(bytes.toArray())) : null};
    }

    /** @returns {Promise<GLib.Bytes | null>} the body of a successful reply */
    async bytes(url) {
        const {status, bytes} = await this._get(url);
        return status === Soup.Status.OK ? bytes : null;
    }

    async _get(url) {
        const message = Soup.Message.new('GET', url);
        if (!message)
            throw new Error(`Not a valid address: ${url}`);
        const bytes = await this._session.send_and_read_async(message, GLib.PRIORITY_DEFAULT, this._cancellable);
        return {status: message.get_status(), bytes};
    }

    destroy() {
        this._cancellable.cancel();
        this._session.abort();
    }
}

/** Lyrics already found, one JSON file per song, so a song played again needs no request. */
export class LyricsCache {
    constructor(directory) {
        this._directory = directory;
        this._encoder = new TextEncoder();
        this._decoder = new TextDecoder();
    }

    async get(query) {
        try {
            const [contents] = await this._fileFor(query).load_contents_async(null);
            const result = JSON.parse(this._decoder.decode(contents));
            return typeof result?.status === 'string' ? result : null;
        } catch {
            // Not cached yet, unreadable or damaged: all mean "ask LRCLIB".
            return null;
        }
    }

    put(query, result) {
        this._write(query, result).catch(() => {
            // The cache only saves a network request; a full or read-only disk must not cost the lyrics.
        });
    }

    async _write(query, result) {
        GLib.mkdir_with_parents(this._directory, 0o700);
        const bytes = new GLib.Bytes(this._encoder.encode(JSON.stringify(result)));
        await this._fileFor(query).replace_contents_bytes_async(
            bytes, null, false, Gio.FileCreateFlags.REPLACE_DESTINATION, null);
    }

    _fileFor(query) {
        const name = GLib.compute_checksum_for_string(GLib.ChecksumType.SHA256, cacheKeyText(query), -1);
        return Gio.File.new_for_path(GLib.build_filenamev([this._directory, `${name}.json`]));
    }
}

const artworkCache = new ArtworkCache(30);

/**
 * Downloads a cover into memory. A Gio.BytesIcon is never kept in the shell's texture cache, unlike an icon
 * loaded from a file, so covers of songs played long ago do not pile up in memory.
 *
 * @returns {Promise<Gio.Icon | null>}
 */
export async function loadArtwork(http, url) {
    if (!url)
        return null;
    const cached = artworkCache.get(url);
    if (cached)
        return cached;

    for (const candidate of artworkUrls(url)) {
        try {
            const bytes = candidate.startsWith('file://')
                ? new GLib.Bytes((await Gio.File.new_for_uri(candidate).load_contents_async(null))[0])
                : await http.bytes(candidate);
            if (bytes && bytes.get_size() > 0) {
                const icon = Gio.BytesIcon.new(bytes);
                artworkCache.put(url, icon);
                return icon;
            }
        } catch {
            // Try the next copy; without any, the panel shows a generic music icon.
        }
    }
    return null;
}
