// LRCLIB lookup rules. Plain JavaScript with no GNOME imports: the HTTP request and the cache are handed in,
// so the tests can run this under Node.

import {cleanTitle, primaryArtist} from './lyrics.js';

const DURATION_TOLERANCE_SECONDS = 3;

/** @typedef {{title: string, artist: string, durationMs: number}} TrackQuery */
/** @typedef {'synced' | 'instrumental' | 'notFound' | 'unavailable'} LyricsStatus */
/** @typedef {{status: LyricsStatus, syncedLyrics?: string}} LyricsResult */
/** @typedef {(path: string) => Promise<{status: number, body: any}>} Request */

export class LrclibClient {
    /** @param {Request} request GETs a path below https://lrclib.net/; body is the parsed JSON of a 200 reply */
    constructor(request) {
        this._request = request;
    }

    /**
     * @param {TrackQuery} query
     * @returns {Promise<LyricsResult>}
     */
    async fetch(query) {
        try {
            let match = query.durationMs > 0 ? await this._getExact(query) : null;
            match ??= await this._search(query.title, query.artist, query.durationMs);

            if (!match) {
                const title = cleanTitle(query.title);
                const artist = primaryArtist(query.artist);
                if (title !== query.title || artist !== query.artist)
                    match = await this._search(title, artist, query.durationMs);
            }

            if (!match)
                return {status: 'notFound'};
            if (match.instrumental)
                return {status: 'instrumental'};
            return {status: 'synced', syncedLyrics: match.syncedLyrics};
        } catch {
            // Network errors, timeouts, cancellation and malformed replies all mean "try again next song".
            return {status: 'unavailable'};
        }
    }

    async _getExact(query) {
        const seconds = Math.round(query.durationMs / 1000);
        const path = `api/get?track_name=${escape(query.title)}&artist_name=${escape(query.artist)}&duration=${seconds}`;

        const {status, body} = await this._request(path);
        // LRCLIB answers 400 instead of 404 for inputs it will not match exactly, such as an empty artist
        // or an episode longer than an hour; the search can still find those.
        if (status === 404 || status === 400)
            return null;
        ensureSuccess(status);
        return usable(body) ? body : null;
    }

    async _search(title, artist, durationMs) {
        const path = `api/search?track_name=${escape(title)}&artist_name=${escape(artist)}`;

        const {status, body} = await this._request(path);
        ensureSuccess(status);

        let candidates = (Array.isArray(body) ? body : []).filter(usable);
        const seconds = durationMs / 1000;
        if (seconds > 0) {
            candidates = candidates
                .filter(track => Math.abs(track.duration - seconds) <= DURATION_TOLERANCE_SECONDS)
                .sort((a, b) => Math.abs(a.duration - seconds) - Math.abs(b.duration - seconds));
        }

        return candidates.find(track => !track.instrumental) ?? candidates[0] ?? null;
    }
}

export class LyricsProvider {
    /**
     * @param {{get(query: TrackQuery): Promise<LyricsResult | null>, put(query: TrackQuery, result: LyricsResult): void}} cache
     * @param {LrclibClient} client
     */
    constructor(cache, client) {
        this._cache = cache;
        this._client = client;
        this._notFound = new Set();
    }

    /** @returns {Promise<LyricsResult>} */
    async get(query) {
        const cached = await this._cache.get(query);
        if (cached)
            return cached;

        const key = cacheKeyText(query);
        if (this._notFound.has(key))
            return {status: 'notFound'};

        const result = await this._client.fetch(query);
        if (result.status === 'synced' || result.status === 'instrumental')
            this._cache.put(query, result);
        else if (result.status === 'notFound')
            this._notFound.add(key);
        return result;
    }
}

/** The text a cache file name is hashed from: the same song at the same length is the same lyrics. */
export function cacheKeyText(query) {
    const seconds = Math.round(query.durationMs / 1000);
    return `${query.artist.trim().toLowerCase()}|${query.title.trim().toLowerCase()}|${seconds}`;
}

function usable(track) {
    return track !== null && typeof track === 'object' &&
        (track.instrumental === true || (typeof track.syncedLyrics === 'string' && track.syncedLyrics.trim() !== ''));
}

function ensureSuccess(status) {
    if (status < 200 || status > 299)
        throw new Error(`LRCLIB answered HTTP ${status}`);
}

function escape(value) {
    return encodeURIComponent(value);
}
