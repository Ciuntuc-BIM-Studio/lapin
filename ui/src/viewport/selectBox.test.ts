import { describe, expect, it } from 'vitest';
import { boxTest, segsOf } from './selectBox';

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
