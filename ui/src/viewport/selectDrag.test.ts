import { describe, expect, it } from 'vitest';
import { dragTo, isWindow, regionTestOf, toggleLasso, type SelectDrag } from './selectDrag';
import { segsOf } from './selectBox';

const P = (x: number, y: number) => ({ x, y });
const same = (x: number, y: number) => P(x, y);

describe('a selection drag', () => {
  it('is a box, a window to the right and crossing to the left of the press', () => {
    const d: SelectDrag = { a: P(10, 10), b: P(50, 40) };
    expect(isWindow(d)).toBe(true);
    expect(isWindow(dragTo(d, P(0, 40)))).toBe(false);
    expect(dragTo(d, P(0, 40)).lasso).toBeUndefined();
  });
  it('Space turns the box into a lasso that follows the pointer and keeps its mode', () => {
    const l = toggleLasso({ a: P(10, 10), b: P(50, 40) });
    expect(l.lasso).toEqual([P(10, 10), P(50, 40)]);
    expect(l.window).toBe(true);
    // Round to the left of the press: still a window, as it began.
    const back = dragTo(dragTo(l, P(50, 80)), P(0, 80));
    expect(back.lasso).toEqual([P(10, 10), P(50, 40), P(50, 80), P(0, 80)]);
    expect(isWindow(back)).toBe(true);
    // A move of a pixel only moves its end.
    expect(dragTo(back, P(0.5, 80)).lasso).toHaveLength(4);
    expect(dragTo(back, P(0.5, 80)).b).toEqual(P(0.5, 80));
  });
  it('Space again is the box, judged live once more', () => {
    const l = dragTo(toggleLasso({ a: P(10, 10), b: P(50, 40) }), P(0, 80));
    const b = toggleLasso(l);
    expect(b.lasso).toBeUndefined();
    expect(b.b).toEqual(P(0, 80));
    expect(isWindow(b)).toBe(false);
  });
  it('tests in world coordinates: a concave lasso window leaves out what pokes out of it', () => {
    // A U round the origin, open at the top: the wall from (0,0) to (2,0) lies in it; one up its middle does not.
    const u = [P(-1, -1), P(3, -1), P(3, 3), P(2.5, 3), P(2.5, 0.5), P(-0.5, 0.5), P(-0.5, 3), P(-1, 3)];
    let d: SelectDrag = { a: u[0], b: u[1], lasso: u, window: true };
    const takes = regionTestOf(d, same);
    expect(takes(segsOf([0, 0, 2, 0]))).toBe(true);
    expect(takes(segsOf([1, 0, 1, 2]))).toBe(false);
    d = { ...d, window: false };
    expect(regionTestOf(d, same)(segsOf([1, 0, 1, 2]))).toBe(true);
  });
});
