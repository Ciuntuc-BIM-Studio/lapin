import { describe, expect, it } from 'vitest';
import { nearestSide, outlineAt, toggleSides } from './alongEdges';

// A 0.3 x 0.5 beam section, clockwise as a drawing may give it: (0,0) (0,.5) (.3,.5) (.3,0).
const cw = [0, 0, 0, 0.5, 0.3, 0.5, 0.3, 0];

describe('bars along edges', () => {
  it('reads the cut holding the click, counter-clockwise, and none elsewhere', () => {
    const loop = outlineAt([{ loops: [cw] }], { x: 0.1, y: 0.2 })!;
    expect(loop).toHaveLength(4);
    // Counter-clockwise: a positive area.
    let a = 0;
    for (let i = 0; i < 4; i++) { const p = loop[i], q = loop[(i + 1) % 4]; a += p.x * q.y - q.x * p.y; }
    expect(a).toBeGreaterThan(0);
    expect(outlineAt([{ loops: [cw] }], { x: 1, y: 1 })).toBeNull();
  });
  it('drops a repeated closing vertex and ignores a click in a hole', () => {
    const closed = [...cw, 0, 0];
    expect(outlineAt([{ loops: [closed] }], { x: 0.1, y: 0.2 })).toHaveLength(4);
    const hole = [0.1, 0.1, 0.2, 0.1, 0.2, 0.4, 0.1, 0.4];
    expect(outlineAt([{ loops: [cw, hole] }], { x: 0.15, y: 0.2 })).toBeNull();
    expect(outlineAt([{ loops: [cw, hole] }], { x: 0.05, y: 0.2 })).not.toBeNull();
  });
  it('finds the side nearest a click and toggles it', () => {
    const loop = outlineAt([{ loops: [cw] }], { x: 0.1, y: 0.2 })!;
    const bottom = nearestSide(loop, { x: 0.15, y: -0.01 });
    expect(bottom.distance).toBeCloseTo(0.01);
    expect(toggleSides(loop, [{ x: 0.15, y: -0.01 }])).toEqual([bottom.index]);
    expect(toggleSides(loop, [{ x: 0.15, y: -0.01 }, { x: 0.1, y: 0.01 }])).toEqual([]);
    expect(toggleSides(loop, [{ x: 0.15, y: -0.01 }, { x: 0.15, y: 0.51 }])).toHaveLength(2);
  });
});
