// Lyric parsing and timing. Plain JavaScript with no GNOME imports, so the tests can run it under Node.

// Sticky, so a row only counts timestamps that lead it: "[00:12.34][00:40.00]text".
const TIMESTAMP = /\s*\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]/y;
const BRACKETED = /\s*[([][^)\]]*[)\]]/g;

/**
 * Parses LRC text into lines sorted by time.
 *
 * @param {string | null | undefined} lrc
 * @returns {{time: number, text: string}[]} times in milliseconds
 */
export function parseLrc(lrc) {
    const lines = [];
    if (!lrc)
        return lines;

    for (const raw of lrc.split('\n')) {
        const row = raw.replace(/\r$/, '');
        const times = [];
        let end = 0;
        TIMESTAMP.lastIndex = 0;
        for (let match; (match = TIMESTAMP.exec(row)) !== null;) {
            times.push(toMilliseconds(match));
            end = TIMESTAMP.lastIndex;
        }
        if (times.length === 0)
            continue;

        const text = row.slice(end).trim() || '♪';
        for (const time of times)
            lines.push({time, text});
    }

    // Array.prototype.sort is stable, so lines sharing a timestamp keep their order.
    return lines.sort((a, b) => a.time - b.time);
}

function toMilliseconds(match) {
    const minutes = Number(match[1]);
    const seconds = Number(match[2]);
    const fraction = match[3] === undefined ? 0 : Number(match[3].padEnd(3, '0'));
    return (minutes * 60 + seconds) * 1000 + fraction;
}

export class LyricTimeline {
    /** @param {{time: number, text: string}[]} lines sorted by time */
    constructor(lines) {
        this._lines = lines;
    }

    /** The line showing at a position, or '' before the first line. */
    lineAt(position) {
        const index = this._indexAt(position);
        return index >= 0 ? this._lines[index].text : '';
    }

    /** When the shown line next changes, or null after the last line. */
    nextChangeAfter(position) {
        const next = this._indexAt(position) + 1;
        return next < this._lines.length ? this._lines[next].time : null;
    }

    _indexAt(position) {
        let low = 0, high = this._lines.length - 1, found = -1;
        while (low <= high) {
            const middle = (low + high) >> 1;
            if (this._lines[middle].time <= position) {
                found = middle;
                low = middle + 1;
            } else {
                high = middle - 1;
            }
        }
        return found;
    }
}

/** Drops " - Remastered 2011" style suffixes and bracketed notes such as "(feat. X)". */
export function cleanTitle(title) {
    const dash = title.indexOf(' - ');
    const head = dash > 0 ? title.slice(0, dash) : title;
    const cleaned = head.replace(BRACKETED, '').trim();
    return cleaned.length === 0 ? title : cleaned;
}

/** The first of several comma- or semicolon-separated artists. */
export function primaryArtist(artist) {
    const cut = artist.search(/[,;]/);
    return cut > 0 ? artist.slice(0, cut).trim() : artist;
}
