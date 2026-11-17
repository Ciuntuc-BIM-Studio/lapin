import { describe, expect, it } from 'vitest';
import type { GridAxis } from '../../shell/gridAxes';
import { axisDims, commitSteps, stationAt, formatDegrees, framePoint, parseDegrees, type ParallelDims, type RadialDims } from './gridDims';

/** Axes 1–4 up the plan at x = 0, 6, 12, 16.5 (y from −1.5 to 10.5); A and B across at y = 0 and 9. */
const ortho: GridAxis[] = [
  ...[0, 6, 12, 16.5].map((x, i) => ({ tag: String(i + 1), a: { x, y: -1.5 }, b: { x, y: 10.5 } })),
  ...[0, 9].map((y, i) => ({ tag: 'AB'[i], a: { x: -1.5, y }, b: { x: 18, y } })),
];

const xsAfter = (steps: ReturnType<typeof commitSteps>) => Object.fromEntries(steps.map((s) => [s.args.tag as string, s.args.x1 as number]));

describe('a grid axis\'s temporary dimensions (forum #78)', () => {
  it('measures to the parallel axes either side, square to them', () => {
    const d = axisDims(ortho, '2') as ParallelDims;
    expect(d.kind).toBe('parallel');
    expect(d.dims.map((x) => [x.other, x.value])).toEqual([['1', 6], ['3', 6]]);
    // Halfway along the stretch the three axes share; the frame gives back points on the axes.
    expect(d.station).toBeCloseTo(4.5);
    const on2 = framePoint(d, d.dims[0].b, d.station);
    expect(on2.x).toBeCloseTo(6);
    expect(on2.y).toBeCloseTo(4.5);
    expect(framePoint(d, d.dims[1].a, d.station).x).toBeCloseTo(12);
    // An end axis has one neighbour; the lettered ones measure to each other.
    expect((axisDims(ortho, '4') as ParallelDims).dims.map((x) => [x.other, x.value])).toEqual([['3', 4.5]]);
    expect((axisDims(ortho, 'B') as ParallelDims).dims.map((x) => [x.other, x.value])).toEqual([['A', 9]]);
  });

  it('typing a distance moves the selected axis, the neighbour staying, alone or with the axes beyond', () => {
    const d = axisDims(ortho, '2') as ParallelDims;
    // 6 → 5 from axis 1: axis 2 to x = 5; with the axes beyond, 3 and 4 by the same.
    expect(xsAfter(commitSteps('g', ortho, d, 'g:1', 5, false))).toEqual({ 2: 5 });
    const all = xsAfter(commitSteps('g', ortho, d, 'g:1', 5, true));
    expect(all['2']).toBeCloseTo(5);
    expect(all['3']).toBeCloseTo(11);
    expect(all['4']).toBeCloseTo(15.5);
    expect(all['1']).toBeUndefined();
    // 6 → 4 from axis 3: axis 2 to x = 8, and with the axes beyond, axis 1 to 2 (the other way).
    const back = xsAfter(commitSteps('g', ortho, d, 'g:3', 4, true));
    expect(back['2']).toBeCloseTo(8);
    expect(back['1']).toBeCloseTo(2);
    expect(back['3']).toBeUndefined();
    // Each step the existing axis move, keeping the axis's length and direction; nothing when it already reads so.
    const s = commitSteps('g', ortho, d, 'g:1', 5, false)[0];
    expect(s.command).toBe('drafting.updateAxis');
    expect(s.args).toMatchObject({ gridId: 'g', tag: '2', y1: -1.5, y2: 10.5 });
    expect(commitSteps('g', ortho, d, 'g:1', 6, true)).toEqual([]);
    expect(commitSteps('g', ortho, d, 'g:1', 0, true)).toEqual([]);
  });

  it('a turned grid is measured square to its axes', () => {
    const r = Math.PI / 6, u = { x: Math.cos(r), y: Math.sin(r) }, n = { x: -u.y, y: u.x };
    const turned: GridAxis[] = [0, 4, 7].map((o, i) => ({ tag: String(i + 1), a: { x: n.x * o - u.x * 2, y: n.y * o - u.y * 2 }, b: { x: n.x * o + u.x * 8, y: n.y * o + u.y * 8 } }));
    const d = axisDims(turned, '2') as ParallelDims;
    expect(Object.fromEntries(d.dims.map((x) => [x.other, x.value.toFixed(6)]))).toEqual({ 1: '4.000000', 3: '3.000000' });
    const [step] = commitSteps('g', turned, d, 'g:1', 5, false);
    // Moved 1 m square to the axes.
    expect(Math.hypot((step.args.x1 as number) - turned[1].a.x, (step.args.y1 as number) - turned[1].a.y)).toBeCloseTo(1);
    expect(((step.args.x1 as number) - turned[1].a.x) * u.x + ((step.args.y1 as number) - turned[1].a.y) * u.y).toBeCloseTo(0);
  });

  it('an axis of a radial grid measures the angles to its neighbours round the centre and turns about it', () => {
    // Five spokes from (10, 10), 1 m to 9 m out, at 0°, 30°, 75°, 120° and 180°.
    const spoke = (tag: string, deg: number): GridAxis => {
      const r = (deg * Math.PI) / 180, c = Math.cos(r), s = Math.sin(r);
      return { tag, a: { x: 10 + c, y: 10 + s }, b: { x: 10 + 9 * c, y: 10 + 9 * s } };
    };
    const radial = [spoke('R1', 0), spoke('R2', 30), spoke('R3', 75), spoke('R4', 120), spoke('R5', 180)];
    const d = axisDims(radial, 'R3') as RadialDims;
    expect(d.kind).toBe('radial');
    expect(d.centre.x).toBeCloseTo(10);
    expect(d.centre.y).toBeCloseTo(10);
    expect(d.dims.map((x) => [x.other, Math.round(x.value * 1000) / 1000])).toEqual([['R4', 45], ['R2', 45]]);
    // 45° → 30° from R2: R3 turns to 60°, about the centre; with the axes beyond, R4 and R5 by the same 15° back.
    const steps = commitSteps('g', radial, d, 'r:R2', 30, true);
    const at = (tag: string) => { const s = steps.find((x) => x.args.tag === tag)!; return Math.atan2((s.args.y2 as number) - 10, (s.args.x2 as number) - 10) * 180 / Math.PI; };
    expect(steps.map((s) => s.args.tag).sort()).toEqual(['R3', 'R4', 'R5']);
    expect(at('R3')).toBeCloseTo(60);
    expect(at('R4')).toBeCloseTo(105);
    expect(Math.abs(at('R5'))).toBeCloseTo(165);
    // Typed from R4 alone: R3 turns to 120° − 30° = 90°.
    const one = commitSteps('g', radial, d, 'r:R4', 30, false);
    expect(one).toHaveLength(1);
    expect(Math.atan2((one[0].args.y2 as number) - 10, (one[0].args.x2 as number) - 10) * 180 / Math.PI).toBeCloseTo(90);
    // An orthogonal grid's axis is never read as radial, and a lone axis has nothing to measure to.
    expect(axisDims(ortho, '1')?.kind).toBe('parallel');
    expect(axisDims([radial[0]], 'R1')).toBeNull();
  });

  it('angles are typed in degrees', () => {
    expect(parseDegrees('37,5°')).toBe(37.5);
    expect(parseDegrees('x')).toBeNull();
    expect(formatDegrees(44.99999)).toBe('45°');
  });
});

describe('where the dimensions stand (issue #65)', () => {
  it('stand near the clicked point, kept within the stretch the axes share; the middle without a click', () => {
    const at = (click: { x: number; y: number } | null) => axisDims(ortho, '2', click) as ParallelDims;
    expect(at(null).station).toBeCloseTo(4.5);
    expect(at({ x: 6, y: 8 }).station).toBeCloseTo(8);
    // Past the shared stretch (−1.5..10.5) it is held at its end.
    expect(at({ x: 6, y: 40 }).station).toBeCloseTo(10.5);
    expect(at({ x: 6, y: -9 }).station).toBeCloseTo(-1.5);
    // A tilted axis: the click's component along the axis.
    expect(stationAt(0, 10, [0, 10], 3)).toBe(3);
    expect(stationAt(5, 5, [0, 10], 3)).toBe(5);
  });
  it('a radial grid ignores the click', () => {
    const fan: GridAxis[] = [0, 45, 90].map((deg, i) => ({ tag: String(i + 1), a: { x: 0, y: 0 }, b: { x: 10 * Math.cos((deg * Math.PI) / 180), y: 10 * Math.sin((deg * Math.PI) / 180) } }));
    expect(axisDims(fan, '2', { x: 3, y: 3 })).toEqual(axisDims(fan, '2'));
  });
});
