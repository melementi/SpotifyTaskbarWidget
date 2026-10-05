import assert from 'node:assert/strict';
import {describe, test} from 'node:test';

import {PositionClock} from '../extension/lib/clock.js';

const T0 = 1_000_000;
const s = seconds => seconds * 1000;

function playing(offsetMs = 0, rate = 1) {
    const clock = new PositionClock(offsetMs);
    clock.setDuration(s(200));
    clock.setPlayback(true, rate, T0);
    return clock;
}

describe('PositionClock', () => {
    test('a paused clock returns the reported position', () => {
        const clock = new PositionClock();
        clock.sync(s(60), T0, true);
        assert.equal(clock.now(T0 + s(30)), s(60));
    });

    test('a playing clock advances from the report time', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.now(T0 + s(5)), s(65));
    });

    test('the position stops at the track length', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.now(T0 + s(3600)), s(200));
    });

    test('an unknown length does not stop the position', () => {
        const clock = playing();
        clock.setDuration(0);
        clock.sync(s(60), T0, true);
        assert.equal(clock.now(T0 + s(300)), s(360));
    });

    test('the offset is added', () => {
        const clock = playing(500);
        clock.sync(s(60), T0, true);
        assert.equal(clock.now(T0 + s(5)), s(65.5));
    });

    test('a negative offset never gives a negative position', () => {
        const clock = new PositionClock(-5000);
        clock.sync(s(1), T0, true);
        assert.equal(clock.now(T0), 0);
    });

    test('pausing freezes the position', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        clock.setPlayback(false, 1, T0 + s(5));
        assert.equal(clock.now(T0 + s(50)), s(65));
    });

    test('the playback rate scales elapsed time', () => {
        const clock = playing(0, 2);
        clock.sync(s(10), T0, true);
        assert.equal(clock.now(T0 + s(5)), s(20));
    });

    test('a report close to the estimate is ignored unless forced', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.sync(s(64.7), T0 + s(5)), false);
        assert.equal(clock.now(T0 + s(5)), s(65));
        assert.equal(clock.sync(s(64.7), T0 + s(5), true), true);
        assert.equal(clock.now(T0 + s(5)), s(64.7));
    });

    test('a report far from the estimate is taken', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.sync(s(120), T0 + s(5)), true);
        assert.equal(clock.now(T0 + s(6)), s(121));
    });

    test('setPlayback reports whether anything changed', () => {
        const clock = new PositionClock();
        assert.equal(clock.setPlayback(true, 1, T0), true);
        assert.equal(clock.setPlayback(true, 1, T0 + s(1)), false);
        assert.equal(clock.setPlayback(true, 2, T0 + s(1)), true);
    });

    test('until is the wall clock wait to reach a position', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.until(s(70), T0 + s(5)), s(5));
    });

    test('until accounts for rate and offset', () => {
        const clock = playing(1000, 2);
        clock.sync(s(10), T0, true);
        assert.equal(clock.until(s(20), T0), s(4.5));
    });

    test('until a passed position is zero', () => {
        const clock = playing();
        clock.sync(s(60), T0, true);
        assert.equal(clock.until(s(30), T0), 0);
    });

    test('until is null while paused', () => {
        const clock = new PositionClock();
        clock.sync(s(60), T0, true);
        assert.equal(clock.until(s(70), T0), null);
    });
});
