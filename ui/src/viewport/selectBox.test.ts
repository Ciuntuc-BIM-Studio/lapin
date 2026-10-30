import { describe, expect, it } from 'vitest';
import { boxTest, lassoTest, segsOf } from './selectBox';

describe('selection box', () => {
  const line = segsOf([0, 0, 2, 0]);
  it('a window takes only what lies wholly inside it', () => {
    expect(boxTest({ x: -1, y: -1 }, { x: 3, y: 1 }, true)(line)).toBe(true);
    expect(boxTest({ x: -1, y: -1 }, { x: 1, y: 1 }, true)(line)).toBe(false);
  });
  it('crossing takes what touches it, even passing through without an end inside', () => {
    expect(boxTest({ x: 1, y: -1 }, { x: 3, y: 1 }, false)(line)).toBe(true);
    expect(boxTest({ x: 0.5, y: -1 }, { x: 1.5, y: 1 }, false)(line)).toBe(true);
    expect(boxTest({ x: 0, y: 1 }, { x: 2, y: 2 }, false)(line)).toBe(false);
  });
  it('takes nothing without segments', () => expect(boxTest({ x: -9, y: -9 }, { x: 9, y: 9 }, true)([])).toBe(false));
});

describe('selection lasso', () => {
  const P = (x: number, y: number) => ({ x, y });
  const tri = [P(-1, -1), P(5, -1), P(2, 4)];
  const line = segsOf([0, 0, 2, 0]);
  it('a window takes what lies wholly inside the polygon', () => {
    expect(lassoTest(tri, true)(line)).toBe(true);
    expect(lassoTest(tri, true)(segsOf([0, 0, 6, 0]))).toBe(false);
  });
  it('crossing takes what touches it, even passing through', () => {
    expect(lassoTest(tri, false)(segsOf([-3, 0, 6, 0]))).toBe(true);
    expect(lassoTest(tri, false)(segsOf([-3, 5, 6, 5]))).toBe(false);
  });
  it('a concave window leaves out what has both ends inside but pokes out', () => {
    // A C open to the right: the segment across its mouth has both ends in the arms.
    const c = [P(0, 0), P(4, 0), P(4, 1), P(1, 1), P(1, 3), P(4, 3), P(4, 4), P(0, 4)];
    expect(lassoTest(c, true)(segsOf([3.5, 0.5, 3.5, 3.5]))).toBe(false);
    expect(lassoTest(c, false)(segsOf([3.5, 0.5, 3.5, 3.5]))).toBe(true);
    expect(lassoTest(c, true)(segsOf([0.5, 0.5, 0.5, 3.5]))).toBe(true);
  });
  it('needs three points', () => expect(lassoTest([P(0, 0), P(1, 1)], false)(line)).toBe(false));
});
