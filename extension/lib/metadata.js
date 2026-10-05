// Reading Spotify's MPRIS metadata. Plain JavaScript with no GNOME imports, so the tests can run it under Node.

// Spotify's album art ids start with a size code: 640x640 by default, 64x64 for the smallest copy.
const LARGE_ART = 'ab67616d0000b273';
const SMALL_ART = 'ab67616d00004851';

/**
 * @param {Record<string, unknown> | null | undefined} metadata the MPRIS Metadata property, fully unpacked
 * @returns {{title: string, artist: string, album: string, durationMs: number, artUrl: string} | null}
 *   null when nothing is loaded
 */
export function trackFromMetadata(metadata) {
    const title = text(metadata?.['xesam:title']);
    if (!title.trim())
        return null;

    const artists = metadata['xesam:artist'];
    const length = Number(metadata['mpris:length']);
    return {
        title,
        artist: Array.isArray(artists) ? artists.map(text).filter(Boolean).join(', ') : text(artists),
        album: text(metadata['xesam:album']),
        // MPRIS lengths are microseconds.
        durationMs: Number.isFinite(length) && length > 0 ? Math.round(length / 1000) : 0,
        artUrl: text(metadata['mpris:artUrl']),
    };
}

/**
 * The addresses to try for a track's cover, smallest first.
 *
 * @param {string} url the mpris:artUrl
 * @returns {string[]}
 */
export function artworkUrls(url) {
    // Older Spotify releases point at an address that no longer serves images.
    const normalized = url.replace(/^https?:\/\/open\.spotify\.com\/image\//, 'https://i.scdn.co/image/');
    if (!/^(https?|file):\/\//.test(normalized))
        return [];

    const small = normalized.replace(`/image/${LARGE_ART}`, `/image/${SMALL_ART}`);
    return small === normalized ? [normalized] : [small, normalized];
}

function text(value) {
    return typeof value === 'string' ? value : '';
}
