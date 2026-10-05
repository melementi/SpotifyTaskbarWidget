import assert from 'node:assert/strict';
import {describe, test} from 'node:test';

import {LrclibClient, LyricsProvider, cacheKeyText} from '../extension/lib/lrclib.js';

const query = {title: 'Yellow', artist: 'Coldplay', durationMs: 267_000};
const ok = body => ({status: 200, body});
const notFound = {status: 404, body: null};
const track = (duration, syncedLyrics, instrumental = false) => ({duration, instrumental, syncedLyrics});

function create(respond) {
    const requests = [];
    const client = new LrclibClient(async path => {
        requests.push(path);
        return respond(path);
    });
    return {client, requests};
}

describe('LrclibClient', () => {
    test('an exact match returns synced lyrics in one request', async () => {
        const {client, requests} = create(() => ok(track(267, '[00:01.00] First line')));
        assert.deepEqual(await client.fetch(query), {status: 'synced', syncedLyrics: '[00:01.00] First line'});
        assert.deepEqual(requests, ['api/get?track_name=Yellow&artist_name=Coldplay&duration=267']);
    });

    test('a missing exact match falls back to search and picks the closest duration', async () => {
        const {client, requests} = create(path => path.startsWith('api/get')
            ? notFound
            : ok([track(300, '[00:01.00] Far'), track(269, '[00:01.00] Near'), track(267, null)]));
        assert.deepEqual(await client.fetch(query), {status: 'synced', syncedLyrics: '[00:01.00] Near'});
        assert.deepEqual(requests.slice(1), ['api/search?track_name=Yellow&artist_name=Coldplay']);
    });

    test('an exact lookup rejected as bad request falls back to search', async () => {
        const {client, requests} = create(path => path.startsWith('api/get') ? {status: 400, body: null} : ok([]));
        const result = await client.fetch({title: 'Long Episode', artist: '', durationMs: 7_200_000});
        assert.equal(result.status, 'notFound');
        assert.match(requests[1], /^api\/search/);
    });

    test('search prefers the closest duration over the first in tolerance', async () => {
        const {client} = create(path => path.startsWith('api/get')
            ? notFound
            : ok([track(265, '[00:01.00] Edge'), track(269, '[00:01.00] Closest')]));
        const result = await client.fetch({...query, durationMs: 268_000});
        assert.equal(result.syncedLyrics, '[00:01.00] Closest');
    });

    test('search results more than three seconds off are rejected', async () => {
        const {client, requests} = create(path => path.startsWith('api/get') ? notFound : ok([track(300, '[00:01.00] Far')]));
        assert.equal((await client.fetch(query)).status, 'notFound');
        assert.equal(requests.length, 2);
    });

    test('retries search with the cleaned title and primary artist', async () => {
        const {client, requests} = create(path =>
            path.startsWith('api/get') ? notFound
                : path.includes('Remastered') ? ok([])
                    : ok([track(267, '[00:01.00] Found')]));
        const result = await client.fetch({title: 'Yellow - Remastered 2011', artist: 'Coldplay, Someone', durationMs: 267_000});
        assert.equal(result.status, 'synced');
        assert.equal(requests.length, 3);
        assert.equal(requests[2], 'api/search?track_name=Yellow&artist_name=Coldplay');
    });

    test('an instrumental track is reported as instrumental', async () => {
        const {client} = create(() => ok(track(267, null, true)));
        assert.equal((await client.fetch(query)).status, 'instrumental');
    });

    test('an exact match with plain lyrics only falls back to search', async () => {
        const {client, requests} = create(path => path.startsWith('api/get')
            ? ok(track(267, null))
            : ok([track(267, '[00:01.00] Synced')]));
        assert.equal((await client.fetch(query)).status, 'synced');
        assert.equal(requests.length, 2);
    });

    test('a server error is unavailable', async () => {
        const {client} = create(() => ({status: 500, body: null}));
        assert.equal((await client.fetch(query)).status, 'unavailable');
    });

    test('a network failure is unavailable', async () => {
        const {client} = create(() => {
            throw new Error('offline');
        });
        assert.equal((await client.fetch(query)).status, 'unavailable');
    });

    test('a reply that is not the expected JSON is ignored', async () => {
        const {client} = create(path => path.startsWith('api/get') ? ok('not json') : ok({not: 'a list'}));
        assert.equal((await client.fetch(query)).status, 'notFound');
    });

    test('zero duration skips the exact lookup and the duration filter', async () => {
        const {client, requests} = create(() => ok([track(180, '[00:01.00] Any')]));
        const result = await client.fetch({...query, durationMs: 0});
        assert.equal(result.status, 'synced');
        assert.equal(requests.length, 1);
        assert.match(requests[0], /^api\/search/);
    });

    test('escapes reserved characters', async () => {
        const {client, requests} = create(() => ok(track(267, '[00:01.00] Line')));
        await client.fetch({title: 'Rock & Roll', artist: 'AC/DC', durationMs: 267_000});
        assert.equal(requests[0], 'api/get?track_name=Rock%20%26%20Roll&artist_name=AC%2FDC&duration=267');
    });
});

describe('LyricsProvider', () => {
    const hit = track(267, '[00:01.00] Line');

    function memoryCache() {
        const entries = new Map();
        return {
            entries,
            get: async q => entries.get(cacheKeyText(q)) ?? null,
            put: (q, result) => entries.set(cacheKeyText(q), result),
        };
    }

    test('fetched lyrics are served from the cache afterwards', async () => {
        const cache = memoryCache();
        await new LyricsProvider(cache, create(() => ok(hit)).client).get(query);

        const offline = create(() => {
            throw new Error('offline');
        });
        const result = await new LyricsProvider(cache, offline.client).get(query);
        assert.deepEqual(result, {status: 'synced', syncedLyrics: '[00:01.00] Line'});
        assert.equal(offline.requests.length, 0);
    });

    test('not found is remembered without new requests', async () => {
        const {client, requests} = create(path => path.startsWith('api/get') ? notFound : ok([]));
        const provider = new LyricsProvider(memoryCache(), client);
        await provider.get(query);
        const afterFirst = requests.length;
        assert.equal((await provider.get(query)).status, 'notFound');
        assert.equal(requests.length, afterFirst);
    });

    test('unavailable is retried and not cached', async () => {
        let online = false;
        const cache = memoryCache();
        const {client} = create(() => {
            if (!online)
                throw new Error('offline');
            return ok(hit);
        });
        const provider = new LyricsProvider(cache, client);
        assert.equal((await provider.get(query)).status, 'unavailable');
        assert.equal(cache.entries.size, 0);
        online = true;
        assert.equal((await provider.get(query)).status, 'synced');
    });

    test('the cache key ignores case and surrounding spaces and rounds the length', () => {
        assert.equal(cacheKeyText({title: ' Yellow ', artist: 'COLDPLAY', durationMs: 266_600}), 'coldplay|yellow|267');
    });

    test('in-memory cache avoids repeated underlying cache reads', async () => {
        let cacheReads = 0;
        const underlying = {
            get: async () => {
                cacheReads++;
                return {status: 'synced', syncedLyrics: '[00:01.00] Line'};
            },
            put: () => {},
        };
        const provider = new LyricsProvider(underlying, create(() => ok(hit)).client);
        await provider.get(query);
        assert.equal(cacheReads, 1);

        const second = await provider.get(query);
        assert.equal(second.status, 'synced');
        assert.equal(cacheReads, 1);
    });
});
