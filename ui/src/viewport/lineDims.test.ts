import { describe, expect, it } from 'vitest';
import { hostOf, lineDims, refOfPlace, type DimLineInfo, type DimRef } from './lineDims';
import { readPrefs, DEFAULT_PREFS } from '../app/prefs';

const L = (id: string, a: [number, number], b: [number, number], faces: [number, number], core?: [number, number], kind = 'wall'): DimLineInfo =>
  ({ id, name: id, kind, a: { x: a[0], y: a[1] }, b: { x: b[0], y: b[1] }, faces, core, joins: { start: [], end: [], path: [] } });

// Layered walls 400 thick, their brick core 250: the south one along y = 0, the north one at y = 5 drawn the other way, the
// west one at x = 0. The host at y = 2, x from 1 to 4: 200 thick, its core 100.
const south = L('S', [0, 0], [6, 0], [-0.2, 0.2], [-0.125, 0.125]);
const north = L('N', [6, 5], [0, 5], [-0.2, 0.2], [-0.125, 0.125]);
const west = L('W', [0, 0], [0, 5], [-0.2, 0.2], [-0.125, 0.125]);
const host = L('H', [1, 2], [4, 2], [-0.1, 0.1], [-0.05, 0.05]);
const lines = [south, north, west, host];
const val = (d: { value: number }) => Math.round(d.value * 1e6) / 1e6;
const by = (mode: DimRef | undefined, picks = {}) => Object.fromEntries(lineDims(hostOf(host), lines, [], picks, { mode }).map((d) => [d.key, d]));

describe('temporary dimensions by the dimension reference (decision 733)', () => {
  it('core faces: from the host\'s core face nearest the other to the other\'s core face nearest it', () => {
    const d = by('core');
    expect(val(d['x:left'])).toBe(2.825);   // 4.875 − 2.05
    expect(val(d['x:right'])).toBe(1.825);  // 1.95 − 0.125
    expect(d['x:left'].ownWhat).toBe('near core face');
    expect(d['x:left'].options[d['x:left'].opt].what).toBe('near core face');
    // Along: the west wall's core face, 0.125 from its axis, from the host's start at x = 1.
    expect(val(d['e:start'])).toBe(0.875);
  });

  it('faces, axis, and the reference line (the default, decision 693\'s way: own reference to the other\'s near face)', () => {
    expect(val(by('faces')['x:left'])).toBe(2.7);
    expect(val(by('faces')['x:right'])).toBe(1.7);
    expect(val(by('axis')['x:left'])).toBe(3);
    expect(val(by('axis')['x:right'])).toBe(2);
    expect(val(by('reference')['x:left'])).toBe(2.8);
    expect(val(by(undefined)['x:left'])).toBe(2.8);
    expect(val(by('axis')['e:start'])).toBe(1);
  });

  it('a wall without a distinct core falls back on its faces; a dot clicked still chooses for that one dimension', () => {
    const plain = L('P', [0, 4], [6, 4], [-0.15, 0.15], [-0.15, 0.15]);
    const d = Object.fromEntries(lineDims(hostOf(host), [south, plain, host], [], {}, { mode: 'core' }).map((x) => [x.key, x]));
    expect(d['x:left'].target.id).toBe('P');
    expect(val(d['x:left'])).toBe(1.8);      // 3.85 − 2.05: its face, the host's core
    expect(val(by('core', { 'x:left': { opt: 0 } })['x:left'])).toBe(2.75); // north's near face, the host's core face
    expect(val(by('core', { 'x:left': { own: 0 } })['x:left'])).toBe(2.875); // from the host's axis
  });

  it('the host\'s kept reference line is the reference line\'s own end', () => {
    const d = lineDims(hostOf({ ...host, reference: -0.1 }), lines, [], {}, { mode: 'reference' }).find((x) => x.key === 'x:right')!;
    expect(d.ownWhat).toBe('reference');
    expect(val(d)).toBe(1.7);
  });

  it('a dot\'s "make this the default" takes the reference its ends come to', () => {
    expect(refOfPlace('near core face', 'near core face')).toBe('core');
    expect(refOfPlace('far core face')).toBe('core');
    expect(refOfPlace('near face', 'near face')).toBe('faces');
    expect(refOfPlace('near face', 'reference')).toBe('reference');
    expect(refOfPlace('axis', 'axis')).toBe('axis');
  });

  it('is kept in the preferences, the reference line by default; anything else unreadable falls back on it', () => {
    expect(DEFAULT_PREFS.tempDimRef).toBe('reference');
    expect(readPrefs(null).tempDimRef).toBe('reference');
    expect(readPrefs({ tempDimRef: 'core' }).tempDimRef).toBe('core');
    expect(readPrefs({ tempDimRef: 'skin' }).tempDimRef).toBe('reference');
  });
});
