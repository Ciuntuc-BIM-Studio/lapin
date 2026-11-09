import { describe, expect, it } from 'vitest';
import { fillFlips, fillGhost, wallSide } from './fillPlacing';

const wall = { start: [0, 0], end: [10, 0] };
const arc = { a: { x: 0, y: 0 }, b: { x: 10, y: 0 }, rise: 0 };

describe('placing a door or window', () => {
  it('reads the side of the wall the cursor is on', () => {
    expect(wallSide(arc, { x: 3, y: 0.1 })).toEqual({ along: 3, left: true });
    expect(wallSide(arc, { x: 3, y: -0.1 }).left).toBe(false);
  });

  it('opens to the side of the cursor, in the hand Space chose', () => {
    expect(fillFlips(arc, { x: 3, y: 0.1 }, false)).toEqual({ flipHand: false, flipSide: false });
    expect(fillFlips(arc, { x: 3, y: -0.1 }, false)).toEqual({ flipHand: false, flipSide: true });
    expect(fillFlips(arc, { x: 3, y: 0.1 }, true)).toEqual({ flipHand: true, flipSide: false });
    expect(fillFlips(null, undefined, true)).toEqual({ flipHand: true, flipSide: false });
  });

  const door = { width: 1, columns: [1], panels: [{ col: 0, row: 0, operation: 'swing-left' }] };
  /** The leaf: the line that starts on the wall's face and runs one width away from it. */
  const leaf = (lines: { x: number; y: number }[][]) => lines[4];

  it('draws the leaf at the hinges, on the side it opens to', () => {
    const open = fillGhost(wall, { x: 5, y: 0.1 }, 0.3, door, false);
    expect(leaf(open)[0].x).toBeCloseTo(4.5);
    expect(leaf(open)[1].y).toBeCloseTo(1.15);
    const other = fillGhost(wall, { x: 5, y: -0.1 }, 0.3, door, false);
    expect(leaf(other)[0].x).toBeCloseTo(4.5);
    expect(leaf(other)[1].y).toBeCloseTo(-1.15);
  });

  it('moves the hinges to the other jamb with the other hand', () => {
    expect(leaf(fillGhost(wall, { x: 5, y: 0.1 }, 0.3, door, true))[0].x).toBeCloseTo(5.5);
  });

  it('draws a fixed pane without a leaf, and keeps the opening on the wall', () => {
    const fixed = fillGhost(wall, { x: 0.1, y: 0.1 }, 0.3, { width: 1.2, panels: [{ operation: 'fixed' }] }, false);
    expect(fixed).toHaveLength(4);
    expect(fixed[0][0].x).toBeCloseTo(0);
  });
});
