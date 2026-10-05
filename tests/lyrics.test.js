import assert from 'node:assert/strict';
import {describe, test} from 'node:test';

import {LyricTimeline, cleanTitle, parseLrc, primaryArtist} from '../extension/lib/lyrics.js';

describe('parseLrc', () => {
    test('parses timestamped lines', () => {
        assert.deepEqual(parseLrc('[00:12.50] Hello\n[01:02.03] World'), [
            {time: 12_500, text: 'Hello'},
            {time: 62_030, text: 'World'},
        ]);
    });

    test('a line with several timestamps yields one entry per timestamp', () => {
        assert.deepEqual(parseLrc('[00:20.00][00:10.00] Chorus'), [
            {time: 10_000, text: 'Chorus'},
            {time: 20_000, text: 'Chorus'},
        ]);
    });

    test('skips metadata and malformed lines', () => {
        assert.deepEqual(parseLrc('[ar:Someone]\nno timestamp\n[xx:yy] bad\n[00:01.00] ok'), [{time: 1000, text: 'ok'}]);
    });

    test('ignores timestamps that do not lead the line', () => {
        assert.deepEqual(parseLrc('[00:01.00] at [00:09.00] mid'), [{time: 1000, text: 'at [00:09.00] mid'}]);
    });

    test('empty text becomes a note symbol', () => {
        assert.deepEqual(parseLrc('[00:05.00]'), [{time: 5000, text: '♪'}]);
    });

    test('sorts out of order lines', () => {
        assert.deepEqual(parseLrc('[00:30.00] C\n[00:10.00] A\n[00:20.00] B').map(line => line.text), ['A', 'B', 'C']);
    });

    test('handles windows line endings', () => {
        assert.deepEqual(parseLrc('[00:01.00] A\r\n[00:02.00] B').map(line => line.text), ['A', 'B']);
    });

    test('accepts three digit and missing fractions', () => {
        const lines = parseLrc('[00:01.123] A\n[00:05] B');
        assert.equal(lines[0].time, 1123);
        assert.equal(lines[1].time, 5000);
    });

    test('null or empty input gives no lines', () => {
        assert.deepEqual(parseLrc(null), []);
        assert.deepEqual(parseLrc(''), []);
    });
});

describe('LyricTimeline', () => {
    const three = new LyricTimeline([
        {time: 10_000, text: 'A'},
        {time: 20_000, text: 'B'},
        {time: 30_000, text: 'C'},
    ]);

    test('before the first line nothing shows', () => {
        assert.equal(three.lineAt(3000), '');
    });

    test('between lines shows the earlier one', () => {
        assert.equal(three.lineAt(15_000), 'A');
        assert.equal(three.lineAt(25_000), 'B');
    });

    test('exactly on a timestamp selects that line', () => {
        assert.equal(three.lineAt(20_000), 'B');
    });

    test('after the last line the last line stays', () => {
        assert.equal(three.lineAt(99_000), 'C');
    });

    test('no lines gives an empty line', () => {
        assert.equal(new LyricTimeline([]).lineAt(1000), '');
    });

    test('next change before the first line is the first line', () => {
        assert.equal(three.nextChangeAfter(3000), 10_000);
    });

    test('next change between lines is the following line', () => {
        assert.equal(three.nextChangeAfter(20_000), 30_000);
    });

    test('next change skips lines sharing the current timestamp', () => {
        const timeline = new LyricTimeline([
            {time: 5000, text: 'A'},
            {time: 5000, text: 'B'},
            {time: 9000, text: 'C'},
        ]);
        assert.equal(timeline.nextChangeAfter(5000), 9000);
    });

    test('no next change after the last line', () => {
        assert.equal(three.nextChangeAfter(30_000), null);
        assert.equal(new LyricTimeline([]).nextChangeAfter(0), null);
    });
});

describe('cleanTitle and primaryArtist', () => {
    for (const [title, expected] of [
        ['Yellow - Remastered 2011', 'Yellow'],
        ['Song (feat. Someone)', 'Song'],
        ['Song [Live]', 'Song'],
        ['Song (Live) - 2019 Mix', 'Song'],
        ['Song', 'Song'],
        ['(Intro)', '(Intro)'],
    ]) {
        test(`cleanTitle(${JSON.stringify(title)})`, () => assert.equal(cleanTitle(title), expected));
    }

    for (const [artist, expected] of [
        ['First, Second', 'First'],
        ['First; Second', 'First'],
        ['Solo', 'Solo'],
    ]) {
        test(`primaryArtist(${JSON.stringify(artist)})`, () => assert.equal(primaryArtist(artist), expected));
    }
});
