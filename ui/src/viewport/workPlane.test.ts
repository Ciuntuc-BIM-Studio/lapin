import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { frameOfView, levelFrame, rayOnPlane, toPlane, toWorld, workPlaneView } from './workPlane';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto =>
  ({ id, type, name, parentId: null, x: 0, y: 0, props, editable: true, owner: null, storeyId: null }) as unknown as NodeDto;

const nodes = new Map<string, NodeDto>([
  ['s1', node('s1', 'IfcBuildingStorey', { elevation: 3 }, 'First floor')],
  ['p1', node('p1', 'LapinView', { kind: 'plan', storeyId: 's1' }, 'Plan 1')],
  // A plane standing at x = 2, facing +x (bearing 0, no tilt).
  ['rp', node('rp', 'LapinView', { kind: 'plane', x: 2, y: 0, z: 1, bearing: 0, tilt: 0, spin: 0 }, 'Plane A')],
  ['sec', node('sec', 'LapinView', { kind: 'section' })],
]);

describe('drafting in the 3D view', () => {
  it('draws on a plan\'s level: the storey\'s elevation, (x, y) the model\'s', () => {
    const f = frameOfView('plan:s1', nodes)!;
    expect(f).toEqual(levelFrame(3));
    expect(frameOfView('p1', nodes)).toEqual(levelFrame(3));
    expect(toWorld(f, { x: 4, y: 5 })).toEqual({ x: 4, y: 5, z: 3 });
    // Looking down from above at (4, 5): the ray lands on that point of the level.
    const p = rayOnPlane(f, { x: 4, y: 5, z: 20 }, { x: 0, y: 0, z: -1 })!;
    expect(p.x).toBeCloseTo(4, 9);
    expect(p.y).toBeCloseTo(5, 9);
    // A slanting ray from (0, 0, 13) down 45° toward +x meets the level 10 m on.
    const q = rayOnPlane(f, { x: 0, y: 0, z: 13 }, { x: Math.SQRT1_2, y: 0, z: -Math.SQRT1_2 })!;
    expect(q.x).toBeCloseTo(10, 9);
    // Along the plane, or away from it: no point.
    expect(rayOnPlane(f, { x: 0, y: 0, z: 5 }, { x: 1, y: 0, z: 0 })).toBeNull();
    expect(rayOnPlane(f, { x: 0, y: 0, z: 5 }, { x: 0, y: 0, z: 1 })).toBeNull();
  });

  it('draws on a reference plane in its own (x, y), as the engine places what is drawn on it', () => {
    const f = frameOfView('rp', nodes)!;
    expect(f.normal.x).toBeCloseTo(1, 9);
    const w = toWorld(f, { x: 1.5, y: 0.5 });
    expect(w.x).toBeCloseTo(2, 9);
    const back = toPlane(f, w);
    expect(back.x).toBeCloseTo(1.5, 9);
    expect(back.y).toBeCloseTo(0.5, 9);
    // A ray toward the plane from x = 10 meets it where it aims.
    const p = rayOnPlane(f, { x: 10, y: w.y, z: w.z }, { x: -1, y: 0, z: 0 })!;
    expect(p.x).toBeCloseTo(1.5, 9);
    expect(p.y).toBeCloseTo(0.5, 9);
    expect(frameOfView('sec', nodes)).toBeNull();
  });

  it('works on the reference plane selected, else the active storey\'s plan (its own plan view when it has one: report #113)', () => {
    expect(workPlaneView({ selection: 'rp', planViewId: null, storeyId: 's1' }, nodes)).toEqual({ id: 'rp', name: 'Plane A' });
    expect(workPlaneView({ selection: null, planViewId: 'p1', storeyId: 's1' }, nodes)).toEqual({ id: 'p1', name: 'Plan 1' });
    expect(workPlaneView({ selection: 'sec', planViewId: null, storeyId: 's1' }, nodes)).toEqual({ id: 'p1', name: 'Plan 1' });
    const bare = new Map([...nodes].filter(([k]) => k !== 'p1'));
    expect(workPlaneView({ selection: 'sec', planViewId: null, storeyId: 's1' }, bare)).toEqual({ id: 'plan:s1', name: 'First floor' });
    expect(workPlaneView({ selection: null, planViewId: null, storeyId: null }, nodes)).toBeNull();
  });
});
