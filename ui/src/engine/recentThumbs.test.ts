import { beforeEach, describe, expect, it, vi } from 'vitest';
import { keepThumb, MAX_THUMBS, rememberThumb, thumbOf } from './recentThumbs';

class Store {
  map = new Map<string, string>();
  getItem(k: string) { return this.map.get(k) ?? null; }
  setItem(k: string, v: string) { this.map.set(k, v); }
}
let store: Store;
beforeEach(() => { store = new Store(); vi.stubGlobal('localStorage', store); });

describe('recent thumbnails', () => {
  it('keeps a picture per path and gives it back', () => {
    keepThumb('/a.lapin', 'data:a');
    keepThumb('/b.lapin', 'data:b');
    expect(thumbOf('/a.lapin')).toBe('data:a');
    expect(thumbOf('/b.lapin')).toBe('data:b');
    expect(thumbOf('/none.lapin')).toBeNull();
  });

  it('replaces the picture of a path that is saved again', () => {
    keepThumb('/a.lapin', 'data:1');
    keepThumb('/a.lapin', 'data:2');
    expect(thumbOf('/a.lapin')).toBe('data:2');
  });

  it('keeps the last MAX_THUMBS and lets the oldest go', () => {
    for (let i = 0; i < MAX_THUMBS + 5; i++) keepThumb(`/p${i}.lapin`, `data:${i}`);
    expect(Object.keys(JSON.parse(store.getItem('lapin.recentThumbs')!))).toHaveLength(MAX_THUMBS);
    expect(thumbOf('/p0.lapin')).toBeNull();
    expect(thumbOf('/p4.lapin')).toBeNull();
    expect(thumbOf('/p5.lapin')).toBe('data:5');
    expect(thumbOf(`/p${MAX_THUMBS + 4}.lapin`)).toBe(`data:${MAX_THUMBS + 4}`);
  });

  it('a path saved again is the newest, so it outlives the older ones', () => {
    for (let i = 0; i < MAX_THUMBS; i++) keepThumb(`/p${i}.lapin`, `data:${i}`);
    keepThumb('/p0.lapin', 'data:again');
    keepThumb('/new.lapin', 'data:new');
    expect(thumbOf('/p0.lapin')).toBe('data:again');
    expect(thumbOf('/p1.lapin')).toBeNull();
  });

  it('reads bad stored data as empty and writes over it', () => {
    for (const bad of ['{not json', '[1,2]', 'null', '"text"', '{"/a":5}']) {
      store.map.set('lapin.recentThumbs', bad);
      expect(thumbOf('/a')).toBeNull();
    }
    store.map.set('lapin.recentThumbs', '{not json');
    keepThumb('/a', 'data:x');
    expect(thumbOf('/a')).toBe('data:x');
  });

  it('survives a storage that throws, and a snapshot that throws or gives nothing', () => {
    vi.stubGlobal('localStorage', { getItem: () => { throw new Error('no'); }, setItem: () => { throw new Error('no'); } });
    expect(() => keepThumb('/a', 'data:x')).not.toThrow();
    expect(thumbOf('/a')).toBeNull();
    expect(() => rememberThumb('/a', () => { throw new Error('no canvas'); })).not.toThrow();
    expect(() => rememberThumb('/a', () => null)).not.toThrow();
  });
});
