import { afterEach, describe, expect, it } from 'vitest';
import { liveBegin, liveEnd, liveTaken, useLive } from './liveInput';
import { resolveAim } from './cad/aim';
import { screenAxes } from './orientation';
import type { Shape } from './cad/shapes';

const close = (a: number, b: number, d = 1e-9) => expect(Math.abs(a - b)).toBeLessThan(d);
afterEach(() => liveEnd());

describe('a click takes what is typed (forum 47)', () => {
  it('a typed length goes along the clicked direction', () => {
    liveBegin('cad', { x: 1, y: 1 }, () => {});
    useLive.setState({ len: '145' });
    const t = liveTaken({ x: 1.3, y: 1.4 })!; // a click well short and off the line
    const d = Math.hypot(t.point.x - 1, t.point.y - 1);
    close(d, 0.145);
    close((t.point.x - 1) / d, 0.6, 1e-9);
    close(t.typed!, 0.145);
    expect(useLive.getState().len).toBe('');
  });

  it('nothing typed: no change', () => {
    liveBegin('cad', { x: 0, y: 0 }, () => {});
    expect(liveTaken({ x: 1, y: 1 })).toBeNull();
  });

  it('a typed angle holds the direction, the cursor gives the distance', () => {
    liveBegin('tool', { x: 0, y: 0 }, () => {});
    useLive.setState({ ang: '90' });
    const t = liveTaken({ x: 0.5, y: 2 })!;
    close(t.point.x, 0); close(t.point.y, 2);
  });

  it('width and height, the click giving the sign of an empty field', () => {
    liveBegin('cad', { x: 0, y: 0 }, () => {}, { rect: 'rectangle' });
    useLive.setState({ mode: 'rect', len: '2000', ang: '' });
    const t = liveTaken({ x: 0.3, y: -0.7 })!;
    close(t.point.x, 2); close(t.point.y, -0.7);
    expect(t.typed).toBeUndefined();
  });

  it('in a turned view the direction is the click, the length the typed one', () => {
    const f = screenAxes(30)!;
    const base = { x: 2, y: 3 };
    liveBegin('cad', base, () => {});
    useLive.setState({ len: '1000' });
    const click = resolveAim({ x: 5, y: 3.5 }, { base, ortho: true, polar: false, snap: null, acquired: [], tol: 0.01, frame: f }).point;
    const t = liveTaken(click)!;
    close(Math.hypot(t.point.x - 2, t.point.y - 3), 1);
    close(Math.atan2(t.point.y - 3, t.point.x - 2), Math.PI / 6);
  });

  it('another gesture is left alone', () => {
    liveBegin('grip', { x: 0, y: 0 }, () => {});
    useLive.setState({ len: '5' });
    expect(liveTaken({ x: 1, y: 1 })).toBeNull();
  });
});

describe('nearest slides to the grid (forum 47)', () => {
  const line = (a: [number, number], b: [number, number]): Shape => ({ type: 'line', a: { x: a[0], y: a[1] }, b: { x: b[0], y: b[1] } });
  const aim = (raw: { x: number; y: number }, shape: Shape, near: { x: number; y: number }, o: object = {}) =>
    resolveAim(raw, { base: null, ortho: false, polar: false, snap: { point: near, mode: 'nearest', shape }, acquired: [], tol: 0.1, grid: 0.1, ...o });

  it('on a horizontal line one axis is the object, the other the grid', () => {
    const a = aim({ x: 1.0161, y: 2.0 }, line([0, 2.0163], [5, 2.0163]), { x: 1.0161, y: 2.0163 });
    close(a.point.y, 2.0163); close(a.point.x, 1.0);
    expect(a.mode).toBe('nearest');
  });

  it('on a slanted line the point lands on a grid line', () => {
    const s = line([0, 0], [4, 2]); // y = x / 2
    const a = aim({ x: 1.93, y: 0.99 }, s, { x: 1.956, y: 0.978 });
    const on = (v: number) => Math.abs(v / 0.1 - Math.round(v / 0.1)) < 1e-9;
    expect(on(a.point.x) || on(a.point.y)).toBe(true);
    close(a.point.y, a.point.x / 2);
  });

  it('with the grid off it stays the nearest point; endpoints still win', () => {
    const a = aim({ x: 1.0161, y: 2.0 }, line([0, 2.0163], [5, 2.0163]), { x: 1.0161, y: 2.0163 }, { grid: undefined });
    close(a.point.x, 1.0161);
    const e = resolveAim({ x: 0.01, y: 2.0 }, { base: null, ortho: false, polar: false, snap: { point: { x: 0, y: 2.0163 }, mode: 'endpoint' }, acquired: [], tol: 0.1, grid: 0.1 });
    close(e.point.y, 2.0163);
  });

  it("in a turned view the grid is the screen's", () => {
    const f = screenAxes(30)!;
    const c = Math.cos(Math.PI / 6), s = Math.sin(Math.PI / 6);
    const W = (x: number, y: number) => ({ x: c * x - s * y, y: s * x + c * y });
    // A line horizontal on the screen at y = 2.0163, in plan coordinates.
    const shape: Shape = { type: 'line', a: W(0, 2.0163), b: W(5, 2.0163) };
    const raw = W(1.0161, 2.0163);
    const a = aim(raw, shape, raw, { frame: f });
    const x = c * a.point.x + s * a.point.y, y = -s * a.point.x + c * a.point.y;
    close(x, 1.0, 1e-9); close(y, 2.0163, 1e-9);
  });
});
