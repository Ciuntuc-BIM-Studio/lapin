import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { lookOf } from '../camera';
import { closedOutline, poseFacing, sketchResult } from './sketch3d';
import { frameOfView, rayOnPlane, toWorld, workPlaneView } from '../workPlane';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto =>
  ({ id, type, name, parentId: null, x: 0, y: 0, props, editable: true, owner: null, storeyId: null }) as unknown as NodeDto;

describe('the 3D sketch\'s shared core (decision 374)', () => {
  it('looks square at a plane from the side it faces', () => {
    const east = poseFacing({ x: 1, y: 0, z: 0 }, { x: 2, y: 1, z: 1 }, 10);
    const look = lookOf(east);
    expect(look.x).toBeCloseTo(-1, 9);
    expect(look.z).toBeCloseTo(0, 9);
    expect(east.target).toEqual({ x: 2, y: 1, z: 1 });
    const top = lookOf(poseFacing({ x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 0 }, 10));
    expect(top.z).toBeLessThan(-0.99);
  });

  it('the sketch\'s work plane comes before the selection, and the 3D host draws in its x, y', () => {
    const nodes = new Map<string, NodeDto>([
      ['s1', node('s1', 'IfcBuildingStorey', { elevation: 0 })],
      ['face', node('face', 'LapinView', { kind: 'plane', onFace: 'cube', x: 2, y: 1, z: 1, bearing: 0, tilt: 0, spin: 0 }, 'Cube, east face')],
      ['rp', node('rp', 'LapinView', { kind: 'plane', x: 0, y: 0, z: 5, bearing: 0, tilt: 90, spin: 0 }, 'Plane A')],
    ]);
    expect(workPlaneView({ selection: 'rp', planViewId: null, storeyId: 's1', sketch: 'face' }, nodes)).toEqual({ id: 'face', name: 'Cube, east face' });
    expect(workPlaneView({ selection: 'rp', planViewId: null, storeyId: 's1', sketch: null }, nodes)?.id).toBe('rp');
    expect(workPlaneView({ selection: null, planViewId: null, storeyId: 's1', sketch: 'gone' }, nodes)?.id).toBe('plan:s1');
    // A ray from the east onto the face's plane lands on it, and back in the model where it was aimed.
    const f = frameOfView('face', nodes)!;
    const p = rayOnPlane(f, { x: 10, y: 1.3, z: 1.4 }, { x: -1, y: 0, z: 0 })!;
    const w = toWorld(f, p);
    expect(w.x).toBeCloseTo(2, 9); expect(w.y).toBeCloseTo(1.3, 9); expect(w.z).toBeCloseTo(1.4, 9);
  });

  it('Finish sketch takes the closed shapes drawn since it began, and tells open lines apart', () => {
    const nodes = new Map<string, NodeDto>([
      ['old', node('old', 'IfcAnnotation', { viewId: 'face', shape: { type: 'circle', c: [0, 0], r: 1 } })],
      ['sq', node('sq', 'IfcAnnotation', { viewId: 'face', shape: { type: 'polyline', pts: [[0, 0], [1, 0], [1, 1]], closed: true } })],
      ['ln', node('ln', 'IfcAnnotation', { viewId: 'face', shape: { type: 'line', a: [0, 0], b: [1, 0] } })],
      ['other', node('other', 'IfcAnnotation', { viewId: 'plan:s1', shape: { type: 'circle', c: [0, 0], r: 1 } })],
    ]);
    const r = sketchResult({ planeId: 'face', name: 'F', before: ['old'] }, nodes);
    expect(r.closed.map((n) => n.id)).toEqual(['sq']);
    expect(r.open.map((n) => n.id)).toEqual(['ln']);
    expect(closedOutline({ type: 'circle', c: { x: 0, y: 0 }, r: 1 }, 4)?.length).toBe(4);
    expect(closedOutline({ type: 'line', a: { x: 0, y: 0 }, b: { x: 1, y: 0 } })).toBeNull();
  });
});
