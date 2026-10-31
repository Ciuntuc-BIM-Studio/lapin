/** A rebar label's grips (decision 222): the elbow, the leader's middle that makes one, the string's middle that moves it off the bars. */

import { describe, expect, it } from 'vitest';
import { labelPlaceGrips, type LabelPatch } from './labelGrips';

const step = (patch: LabelPatch) => ({ command: 'rebar.placeLabel', args: { viewId: 'v', owner: 'b', key: 'projection', ...patch } });
const args = (g: ReturnType<typeof labelPlaceGrips>, key: string, to: { x: number; y: number }) => g.find((x) => x.key === key)?.step?.(to)?.args;

describe('a label without an elbow', () => {
  it('has its point and, at the leader\'s middle, a grip whose drag makes the elbow', () => {
    const g = labelPlaceGrips({ at: [1.2, 0.9], leader: [1.0, 0.8] }, 'label:projection', step);
    expect(g.map((x) => [x.key, x.kind])).toEqual([['label:projection', 'move'], ['label:projection:elbow', 'edgeMid']]);
    expect(args(g, 'label:projection', { x: 2, y: 1 })).toMatchObject({ at: [2, 1] });
    expect(args(g, 'label:projection:elbow', { x: 1.1, y: 0.7 })).toMatchObject({ elbow: [1.1, 0.7] });
  });
});

describe('a label with an elbow', () => {
  it('drags the elbow as a vertex and takes it away with Ctrl-click', () => {
    const g = labelPlaceGrips({ at: [1.2, 0.9], elbow: [1.0, 0.8] }, 'label', step);
    const elbow = g.find((x) => x.key === 'label:elbow');
    expect(elbow?.kind).toBe('vertex');
    expect(elbow?.step?.({ x: 1, y: 1 })?.args).toMatchObject({ elbow: [1, 1] });
    expect(elbow?.remove?.()?.args).toMatchObject({ elbow: null });
  });
});

describe('a distribution\'s string', () => {
  it('has a grip at its middle whose drag across the bars changes the offset in paper mm, rounded to the half', () => {
    const g = labelPlaceGrips({ at: [0.8, -0.2], string: { mid: [2, -0.2], dir: [0, -1], offset: 4, mm: 0.05 } }, 'label', step);
    const s = g.find((x) => x.key === 'label:string');
    expect(s?.kind).toBe('edgeMid');
    // 0.3 m farther down at 1:50 is 6 mm more; a drag along the string changes nothing.
    expect(s?.step?.({ x: 2.5, y: -0.5 })?.args).toMatchObject({ offset: 10 });
    expect(s?.step?.({ x: 3, y: -0.2 })?.args).toMatchObject({ offset: 4 });
    expect(s?.step?.({ x: 2, y: -0.2 + 0.03 })?.args).toMatchObject({ offset: 3.5 });
  });

  it('maps its points through an inset both ways', () => {
    const map = { show: (p: { x: number; y: number }) => ({ x: p.x + 10, y: p.y * 2 }), back: (p: { x: number; y: number }) => ({ x: p.x - 10, y: p.y / 2 }) };
    const g = labelPlaceGrips({ at: [1, 1], leader: [2, 2] }, 'label', step, map);
    expect(g[0].at).toEqual({ x: 11, y: 2 });
    expect(g[1].at).toEqual({ x: 12, y: 4 });
    expect(args(g, 'label:elbow', { x: 13, y: 6 })).toMatchObject({ elbow: [3, 3] });
  });
});
