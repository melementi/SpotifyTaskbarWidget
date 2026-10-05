import assert from 'node:assert/strict';
import {describe, test} from 'node:test';

import {ArtworkCache, artworkUrls, trackFromMetadata} from '../extension/lib/metadata.js';

describe('trackFromMetadata', () => {
    test('reads Spotify metadata', () => {
        assert.deepEqual(trackFromMetadata({
            'mpris:trackid': '/com/spotify/track/abc',
            'mpris:length': 267_000_000,
            'mpris:artUrl': 'https://i.scdn.co/image/ab67616d0000b273abc',
            'xesam:title': 'Yellow',
            'xesam:artist': ['Coldplay', 'Someone'],
            'xesam:album': 'Parachutes',
        }), {
            title: 'Yellow',
            artist: 'Coldplay, Someone',
            album: 'Parachutes',
            durationMs: 267_000,
            artUrl: 'https://i.scdn.co/image/ab67616d0000b273abc',
        });
    });

    test('nothing loaded gives null', () => {
        assert.equal(trackFromMetadata(null), null);
        assert.equal(trackFromMetadata({}), null);
        assert.equal(trackFromMetadata({'xesam:title': '  '}), null);
    });

    test('missing fields become empty', () => {
        assert.deepEqual(trackFromMetadata({'xesam:title': 'Song', 'xesam:artist': 'Solo'}), {
            title: 'Song', artist: 'Solo', album: '', durationMs: 0, artUrl: '',
        });
    });
});

describe('artworkUrls', () => {
    test('tries the small Spotify cover first', () => {
        assert.deepEqual(artworkUrls('https://i.scdn.co/image/ab67616d0000b273abc'), [
            'https://i.scdn.co/image/ab67616d00004851abc',
            'https://i.scdn.co/image/ab67616d0000b273abc',
        ]);
    });

    test('rewrites the old open.spotify.com address', () => {
        assert.deepEqual(artworkUrls('https://open.spotify.com/image/xyz'), ['https://i.scdn.co/image/xyz']);
    });

    test('keeps local files and rejects other schemes', () => {
        assert.deepEqual(artworkUrls('file:///tmp/cover.png'), ['file:///tmp/cover.png']);
        assert.deepEqual(artworkUrls('data:image/png;base64,AAAA'), []);
        assert.deepEqual(artworkUrls(''), []);
    });
});

describe('ArtworkCache', () => {
    test('stores and retrieves cached items', () => {
        const cache = new ArtworkCache(3);
        const icon1 = {id: 1};
        cache.put('https://url/1', icon1);
        assert.equal(cache.get('https://url/1'), icon1);
        assert.equal(cache.get('https://url/2'), null);
    });

    test('evicts least recently used item when capacity is exceeded', () => {
        const cache = new ArtworkCache(2);
        cache.put('url1', {id: 1});
        cache.put('url2', {id: 2});
        assert.deepEqual(cache.get('url1'), {id: 1});
        cache.put('url3', {id: 3});
        assert.deepEqual(cache.get('url1'), {id: 1});
        assert.deepEqual(cache.get('url3'), {id: 3});
        assert.equal(cache.get('url2'), null);
    });

    test('clearing cache removes all items', () => {
        const cache = new ArtworkCache(2);
        cache.put('url1', {id: 1});
        cache.clear();
        assert.equal(cache.get('url1'), null);
    });
});
