import { describe, expect, it } from 'vitest';
import { carriedWith, moveArgs, moves, moveTarget, staysOnLevel, typedTarget, vertical } from './moveIn3d';

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

describe('a stair or a wall moved in 3D keeps its level (decision 399)', () => {
  const parse = (x: string) => (Number.isFinite(Number(x)) ? Number(x) / 1000 : null);
  // The stair's top corner (2.97, under its 3 cm finish) onto the floor's corner (3.00): 1–3 cm lifted before.
  const base = { x: 3.88, y: 3.82, z: 2.97 };
  const corner = { point: { x: 2.55, y: 4.85, z: 3.0 }, kind: 'Endpoint' as const };
  it('goes to the corner in plan only', () => {
    expect(moveTarget(base, corner, '', null, true, parse)).toEqual({ x: 2.55, y: 4.85, z: 2.97 });
  });
  it('goes up or down when the blue axis is held or inferred, or dz is typed', () => {
    expect(moveTarget(base, corner, '', { axis: 'blue' }, true, parse)?.z).toBe(3.0);
    expect(moveTarget(base, { ...corner, kind: 'On blue axis', color: 'blue' }, '', null, true, parse)?.z).toBe(3.0);
    expect(moveTarget(base, corner, '0,0,500', null, true, parse)).toEqual({ x: 3.88, y: 3.82, z: 3.47 });
    expect(vertical(null, corner, '100;0;30')).toBe(true);
  });
  it('measures a typed length in plan', () => {
    const t = moveTarget({ x: 0, y: 0, z: 0 }, { point: { x: 3, y: 0, z: 2 }, kind: 'Endpoint' }, '1000', null, true, parse)!;
    expect(t.x).toBeCloseTo(1, 9);
    expect(t.z).toBe(0);
  });
  it('leaves what has no level of its own free in 3D', () => {
    expect(moveTarget(base, corner, '', null, false, parse)?.z).toBe(3.0);
    expect(staysOnLevel(['IfcStair', 'IfcWall'])).toBe(true);
    expect(staysOnLevel(['IfcStair', 'IfcBuildingElementProxy'])).toBe(false);
    expect(staysOnLevel([])).toBe(false);
  });
});
