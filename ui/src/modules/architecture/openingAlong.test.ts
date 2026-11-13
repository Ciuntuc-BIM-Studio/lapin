import { describe, expect, it } from 'vitest';
import type { OpeningRefs } from './openingDims';
import { alongShown, alongWritten, referenceFor } from './openingAlong';

/** A 10 m wall; a wall crossing at 2 m (faces 1.9–2.1, core 1.95–2.05) and another at 8 m (faces 7.9–8.1), both on the left face. */
const wall = (id: string, f: number[], core?: number[]) => ({ id, name: id, sides: ['left' as const], left: { faces: f, core, axis: (f[0] + f[1]) / 2 }, right: { faces: f, axis: (f[0] + f[1]) / 2 } });
const refs: OpeningRefs = {
  wallId: 'w', length: 10, faces: { left: 0.1, right: -0.1 }, ends: { left: [0, 10], right: [0, 10] },
  walls: [wall('a', [1.9, 2.1], [1.95, 2.05]), wall('b', [7.9, 8.1])],
};

describe('an opening\'s Along the wall', () => {
  it('from the wall start reads the point\'s distance from 0 and writes the centre back', () => {
    const ref = referenceFor(refs, 'start', 5, 1, 'start');
    expect(alongShown(ref, 5, 1, 'start')).toBeCloseTo(4.5);
    expect(alongShown(ref, 5, 1, 'centre')).toBeCloseTo(5);
    expect(alongShown(ref, 5, 1, 'end')).toBeCloseTo(5.5);
    expect(alongWritten(ref, 4.5, 1, 'start')).toBeCloseTo(5);
    expect(alongWritten(ref, 5.5, 1, 'end')).toBeCloseTo(5);
    expect(alongWritten(ref, 3, 1, 'centre')).toBeCloseTo(3);
  });
  it('from the nearest face: the wall before reads its distance, the wall after the other way', () => {
    // An opening centred at 3 (2.5–3.5): wall a (face at 2.1) is the nearest, before it.
    const a = referenceFor(refs, 'face', 3, 1, 'start');
    expect(a.name).toBe('a');
    expect(alongShown(a, 3, 1, 'start')).toBeCloseTo(0.4);
    expect(alongWritten(a, 0.5, 1, 'start')).toBeCloseTo(3.1);
    // At 7 (6.5–7.5): wall b (face at 7.9) is after it.
    const b = referenceFor(refs, 'face', 7, 1, 'end');
    expect(b.name).toBe('b');
    expect(alongShown(b, 7, 1, 'end')).toBeCloseTo(0.4);
    expect(alongWritten(b, 0.4, 1, 'end')).toBeCloseTo(7);
    expect(alongWritten(b, 1, 1, 'centre')).toBeCloseTo(6.9);
  });
  it('from the core face: the wall\'s core when it has one, its face when not', () => {
    const a = referenceFor(refs, 'core', 3, 1, 'start');
    expect(a.s).toBeCloseTo(2.05);
    expect(alongShown(a, 3, 1, 'start')).toBeCloseTo(0.45);
    const b = referenceFor(refs, 'core', 7, 1, 'end');
    expect(b.s).toBeCloseTo(7.9);
  });
  it('round-trips whatever the choice, and falls back to the wall start without walls or references', () => {
    for (const from of ['start', 'face', 'core'] as const) for (const point of ['start', 'centre', 'end'] as const) {
      const ref = referenceFor(refs, from, 4.2, 0.9, point);
      expect(alongWritten(ref, alongShown(ref, 4.2, 0.9, point), 0.9, point)).toBeCloseTo(4.2);
    }
    expect(referenceFor(null, 'face', 3, 1, 'start')).toEqual({ s: 0, away: 1, name: null });
    expect(referenceFor({ wallId: 'w', walls: [] }, 'core', 3, 1, 'start').s).toBe(0);
  });
});
