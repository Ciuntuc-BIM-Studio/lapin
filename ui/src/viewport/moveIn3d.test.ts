import { describe, expect, it } from 'vitest';
import { carriedWith, moveArgs, moves, typedTarget } from './moveIn3d';

const parse = (s: string) => { const v = Number(s); return Number.isFinite(v) ? v / 1000 : null; };
const base = { x: 1, y: 1, z: 0 };

describe('the move in 3D', () => {
  it('takes a typed length along the way the cursor points', () => {
    const t = typedTarget('500', base, { x: 1, y: 3, z: 0 }, parse)!;
    expect(t.x).toBeCloseTo(1, 9);
    expect(t.y).toBeCloseTo(1.5, 9);
    // Up a slope: the length is along it.
    const up = typedTarget('1000', base, { x: 4, y: 1, z: 4 }, parse)!;
    expect(Math.hypot(up.x - 1, up.y - 1, up.z)).toBeCloseTo(1, 9);
    // The cursor on the base: no way to go.
    expect(typedTarget('500', base, base, parse)).toBeNull();
    expect(typedTarget('500', base, null, parse)).toBeNull();
  });

  it('takes dx,dy and dx,dy,dz as the displacement itself', () => {
    expect(typedTarget('1000,-500', base, null, parse)).toEqual({ x: 2, y: 0.5, z: 0 });
    expect(typedTarget('0;0;200', base, null, parse)).toEqual({ x: 1, y: 1, z: 0.2 });
    expect(typedTarget('', base, null, parse)).toBeNull();
    expect(typedTarget('1,x', base, null, parse)).toBeNull();
    expect(typedTarget('1,2,3,4', base, null, parse)).toBeNull();
  });

  it('makes one element.transform, the vertical part as up', () => {
    expect(moveArgs(['a'], false, { x: 0.5, y: 0.25, z: 0 })).toEqual({ ids: ['a'], op: 'move', by: [0.5, 0.25] });
    expect(moveArgs(['a', 'b'], true, { x: 0, y: 0, z: 0.2 })).toEqual({ ids: ['a', 'b'], op: 'copy', by: [0, 0], up: 0.2 });
    expect(moves({ x: 0, y: 0, z: 0 })).toBe(false);
    expect(moves({ x: 0, y: 0, z: 0.001 })).toBe(true);
  });

  it('carries a wall\'s openings and the doors in them on the screen', () => {
    const rels = [
      { class: 'IfcRelVoidsElement', relating: 'wall', related: ['hole'] },
      { class: 'IfcRelFillsElement', relating: 'hole', related: ['door'] },
      { class: 'IfcRelVoidsElement', relating: 'other', related: ['hole2'] },
    ];
    expect([...carriedWith(['wall'], rels)].sort()).toEqual(['door', 'hole', 'wall']);
    expect([...carriedWith(['stair'], rels)]).toEqual(['stair']);
  });
});
