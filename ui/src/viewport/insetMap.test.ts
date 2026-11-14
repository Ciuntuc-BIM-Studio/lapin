import { describe, expect, it } from 'vitest';
import type { Grip, MarkDto } from '../platform/module';
import { drawsThroughInsets, insetUnder, mapShape, mapper, PLACES_INSETS, throughInset, viaOf, type Via } from './insetMap';

// A section inset in a plan at (20, 0), its middle (0, 1.5) there, five times its size (1:20 in a 1:100 plan).
const via: Via = { inset: 'ins', host: 'plan:s1', source: 'sec', at: [20, 0], centre: [0, 1.5], factor: 5 };
const insetMark: MarkDto = { id: 'ins', segments: [], data: { source: 'sec', at: via.at, centre: via.centre, factor: 5, bounds: [15, -10, 25, 10] } };

describe('inset views (decision 574)', () => {
  it('maps the source to the host and back', () => {
    const { show, back } = mapper(via);
    expect(show({ x: 1, y: 1.5 })).toEqual({ x: 25, y: 0 });
    expect(back({ x: 25, y: 0 })).toEqual({ x: 1, y: 1.5 });
  });

  it('finds the inset the points fall in, and none when one is outside it', () => {
    const marks = [{ id: 'wall', segments: [0, 0, 4, 0] }, insetMark];
    const u = insetUnder(marks, [{ x: 20, y: 0 }, { x: 25, y: 5 }]);
    expect(u?.source).toBe('sec');
    expect(u?.inset).toBe('ins');
    expect(u?.map({ x: 25, y: 5 })).toEqual({ x: 1, y: 2.5 });
    expect(insetUnder(marks, [{ x: 20, y: 0 }, { x: 30, y: 0 }])).toBeNull();
    expect(insetUnder(marks, [])).toBeNull();
  });

  it('reads the mapping a mark came by, only when it is whole', () => {
    expect(viaOf({ id: 'line', segments: [], data: { via } })).toEqual(via);
    expect(viaOf({ id: 'line', segments: [], data: { via: { ...via, factor: 0 } } })).toBeNull();
    expect(viaOf({ id: 'line', segments: [] })).toBeNull();
  });

  it("shows a source view's grip where the inset shows it and takes its drag back to the source", () => {
    const g: Grip = { key: 'a', at: { x: 1, y: 1.5 }, view: 'sec', step: (to) => ({ command: 'drafting.grip', args: { to: [to.x, to.y] } }) };
    const t = throughInset(g, via)!;
    expect(t.at).toEqual({ x: 25, y: 0 });
    expect(t.view).toBe('plan:s1');
    expect(t.step!({ x: 30, y: 5 })).toEqual({ command: 'drafting.grip', args: { to: [2, 2.5] } });
  });

  it('keeps a grip its module put on the host already, and drops one of another view or of every view', () => {
    const own: Grip = { key: 'b', at: { x: 25, y: 0 }, view: 'plan:s1' };
    expect(throughInset(own, via)).toBe(own);
    expect(throughInset({ key: 'c', at: { x: 0, y: 0 }, view: 'other' }, via)).toBeNull();
    expect(throughInset({ key: 'd', at: { x: 0, y: 0 } }, via)).toBeNull();
  });

  it('takes a drafted shape to the source, its radius by the scale', () => {
    const { back } = mapper(via);
    expect(mapShape({ type: 'line', a: { x: 20, y: 0 }, b: { x: 25, y: 0 } }, back)).toEqual({ type: 'line', a: { x: 0, y: 1.5 }, b: { x: 1, y: 1.5 } });
    const c = mapShape({ type: 'circle', c: { x: 20, y: 0 }, r: 5 }, back);
    expect(c.type === 'circle' && c.r).toBeCloseTo(1, 9);
  });

  it("draws a plan's annotations on the inset's source, not its model, nor the tool placing the inset", () => {
    expect(drawsThroughInsets('drafting.dimension')).toBe(true);
    expect(drawsThroughInsets('rebar.straight')).toBe(true);
    expect(drawsThroughInsets('architecture.wall')).toBe(false);
    for (const id of PLACES_INSETS) expect(drawsThroughInsets(id)).toBe(false);
  });
});
