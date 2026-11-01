/** A bar's anchor grips (decision 226): the engine's anchors as orange grips that run `rebar.grip` with their key. */

import { describe, expect, it } from 'vitest';
import { anchorGrips } from './anchors';

const step = (key: string) => (to: { x: number; y: number }) => ({ command: 'rebar.grip', args: { id: 'bar', key, point: [to.x, to.y] } });

describe('a bar\'s anchors', () => {
  it('become grips of kind anchor, each dropping through rebar.grip with its key', () => {
    const g = anchorGrips([{ key: 'anchor:origin', at: [0.97, 0.97] }, { key: 'anchor:end', at: [3.03, 0.97] }], step);
    expect(g.map((x) => [x.key, x.kind, x.at])).toEqual([['anchor:origin', 'anchor', { x: 0.97, y: 0.97 }], ['anchor:end', 'anchor', { x: 3.03, y: 0.97 }]]);
    expect(g[1].step?.({ x: 4.03, y: 0.97 })).toEqual({ command: 'rebar.grip', args: { id: 'bar', key: 'anchor:end', point: [4.03, 0.97] } });
    expect(g[0].title).toMatch(/formwork/);
  });
  it('leaves out what is not an anchor or has no point, and shows through an inset', () => {
    const g = anchorGrips([{ key: 'end', at: [1, 1] }, { key: 'anchor:end', at: [] }, { key: 'anchor:origin', at: [1, 2] }], step, (p) => ({ x: p.x * 2, y: p.y * 2 }));
    expect(g.map((x) => [x.key, x.at])).toEqual([['anchor:origin', { x: 2, y: 4 }]]);
    expect(anchorGrips(undefined, step)).toEqual([]);
  });
});
