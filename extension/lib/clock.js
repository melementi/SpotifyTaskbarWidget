// Playback position between reports. Plain JavaScript with no GNOME imports, so the tests can run it under Node.

// Spotify reports a position that trails the audio by a little and varies from call to call. Taking every
// report would let the lyric step back to the previous line for a moment right after a line change.
const DRIFT_TOLERANCE_MS = 400;

export class PositionClock {
    /** @param {number} offsetMs positive values run ahead of the music, so lyrics show earlier */
    constructor(offsetMs = 0) {
        this.offsetMs = offsetMs;
        this._position = 0;
        this._reportedAt = 0;
        this._durationMs = 0;
        this._playing = false;
        this._rate = 1;
    }

    get playing() {
        return this._playing && this._rate > 0;
    }

    /** @returns {boolean} whether the playing state or rate changed */
    setPlayback(playing, rate, now) {
        if (playing === this._playing && rate === this._rate)
            return false;
        this._position = this._rawAt(now);
        this._reportedAt = now;
        this._playing = playing;
        this._rate = rate;
        return true;
    }

    setDuration(durationMs) {
        this._durationMs = durationMs;
    }

    /**
     * Takes a position the player reported at `now`.
     *
     * @param {boolean} force take it even if it is close to the estimate: after a seek or a track change
     * @returns {boolean} whether the clock moved
     */
    sync(positionMs, now, force = false) {
        if (!force && Math.abs(positionMs - this._rawAt(now)) <= DRIFT_TOLERANCE_MS)
            return false;
        this._position = positionMs;
        this._reportedAt = now;
        return true;
    }

    /** The position lyrics are shown for, with the offset applied. */
    now(now) {
        return Math.max(0, this._rawAt(now) + this.offsetMs);
    }

    /** Milliseconds until `now()` reaches `target`, or null while paused. */
    until(target, now) {
        if (!this.playing)
            return null;
        return Math.max(0, (target - this.now(now)) / this._rate);
    }

    _rawAt(now) {
        const elapsed = this._playing && now > this._reportedAt ? (now - this._reportedAt) * this._rate : 0;
        const raw = this._position + elapsed;
        return this._durationMs > 0 ? Math.min(raw, this._durationMs) : raw;
    }
}
