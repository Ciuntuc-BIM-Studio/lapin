import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { architectureGrips3d, insidePoint, wallAcross, wallFaces, wallHeights } from './grips3d';
import { alongGrip, alongOf } from '../../viewport/Grips3D';
import { offsetDistance } from '../../viewport/outlineOffset';
import { outlineGrips } from '../../viewport/gripKit';

const node = (id: string, type: string, props: NodeDto['props'], storeyId: string | null = 's0'): NodeDto =>
  ({ id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId });
const storey = (id: string, elevation: number, height = 3) => node(id, 'IfcBuildingStorey', { elevation, height }, null);

function model(...ns: NodeDto[]) {
  useSession.setState({ nodes: new Map([storey('s0', 0), storey('s1', 3.2), ...ns].map((n) => [n.id, n])), relations: new Map() });
}

describe('a wall\'s 3D grips (decision 601)', () => {
  it('stands its top where the engine puts it: the storey above with its offset, else its own height', () => {
    const above = node('w', 'IfcWall', { start: [0, 0], end: [4, 0], height: 3, top: 'above', topOffset: -0.2, baseOffset: 0.1 });
    model(above);
    expect(wallHeights(above)).toMatchObject({ base: 0.1, top: 3.0, connected: true });
    const own = node('w', 'IfcWall', { start: [0, 0], end: [4, 0], height: 2.5 });
    model(own);
    expect(wallHeights(own)).toMatchObject({ base: 0, top: 2.5, connected: false });
  });

  it('drags its top as the top\'s offset when tied to a storey, as its height when not, one command each', () => {
    const tied = node('w', 'IfcWall', { start: [0, 0], end: [4, 0], height: 3, top: 'above', topOffset: 0 });
    model(tied);
    const top = architectureGrips3d.IfcWall(tied).find((g) => g.key === 'top')!;
    expect(top.at.z).toBeCloseTo(3.2);
    expect(top.step(-0.3)).toEqual({ command: 'architecture.setWallTop', args: { ids: ['w'], topOffset: -0.3 } });
    const free = node('w', 'IfcWall', { start: [0, 0], end: [4, 0], height: 2.5 });
    model(free);
    const g = architectureGrips3d.IfcWall(free).find((x) => x.key === 'top')!;
    expect(g.step(0.5)).toEqual({ command: 'architecture.setWallTop', args: { ids: ['w'], height: 3 } });
    // Not under its base.
    expect(g.step(-3)).toBeNull();
  });

  it('puts a grip on each face half way up and moves the wall sideways from either', () => {
    const w = node('w', 'IfcWall', { start: [0, 0], end: [4, 0], height: 3, thickness: 0.3 });
    model(w);
    expect(wallFaces(w)).toEqual({ left: 0.15, right: -0.15 });
    const gs = architectureGrips3d.IfcWall(w);
    const left = gs.find((g) => g.key === 'face:left')!, right = gs.find((g) => g.key === 'face:right')!;
    expect(left.at).toEqual({ x: 2, y: 0.15, z: 1.5 });
    expect(right.dir.y).toBeCloseTo(-1);
    expect(Math.abs(right.dir.x) + Math.abs(right.dir.z)).toBe(0);
    expect(left.step(0.5)).toEqual({ command: 'architecture.updateWall', args: { id: 'w', x1: 0, y1: 0.5, x2: 4, y2: 0.5, arc: 0 } });
    expect(right.step(0.5)!.args).toMatchObject({ y1: -0.5, y2: -0.5 });
  });

  it('changes a curved wall\'s radius about its centre, its ends and rise scaled', () => {
    // A half circle of radius 2 about (2, 0), bulging left (+y).
    const c = { a: { x: 0, y: 0 }, b: { x: 4, y: 0 }, rise: 2 };
    const out = wallAcross(c, { x: 0, y: 1 }, 0.5)!;
    expect(out.a.x).toBeCloseTo(-0.5); expect(out.b.x).toBeCloseTo(4.5); expect(out.rise).toBeCloseTo(2.5);
    const back = wallAcross(c, { x: 0, y: -1 }, 0.5)!;
    expect(back.rise).toBeCloseTo(1.5);
    expect(wallAcross(c, { x: 0, y: -1 }, 2)).toBeNull();
  });
});

describe('a floor\'s and a roof\'s 3D grips', () => {
  it('drags a floor\'s top as its offset from its storey, and a roof\'s eave as its wall plate', () => {
    const slab = node('f', 'IfcSlab', { contour: [[0, 0], [4, 0], [4, 4], [0, 4]], offset: 0.1 });
    const roof = node('r', 'IfcRoof', { contour: [[0, 0], [8, 0], [8, 6], [0, 6]], eaveOffset: 0.2 });
    model(slab, roof);
    const top = architectureGrips3d.IfcSlab(slab)[0];
    expect(top.at).toEqual({ x: 2, y: 2, z: 0.1 });
    expect(top.step(-0.25)).toEqual({ command: 'architecture.updateSlab', args: { id: 'f', offset: -0.15 } });
    const eave = architectureGrips3d.IfcRoof(roof)[0];
    expect(eave.at).toEqual({ x: 2, y: 0, z: 3.2 });
    expect(eave.step(0.3)).toEqual({ command: 'architecture.updateRoof', args: { id: 'r', attrs: { eaveOffset: 0.5 } } });
  });

  it('stands a floor\'s grip inside an L too', () => {
    const l = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 4 }, { x: 0, y: 4 }];
    const p = insidePoint(l);
    expect(p.x > 0 && p.y > 0 && (p.x < 1 || p.y < 1)).toBe(true);
  });
});

describe('dragging along a 3D grip\'s line', () => {
  it('takes the line\'s point nearest the cursor\'s ray, and a snapped point\'s place along it', () => {
    const g = { at: { x: 1, y: 1, z: 2 }, dir: { x: 0, y: 0, z: 1 } };
    // A ray along x at height 3.5, passing by the grip's line.
    expect(alongGrip(g, { origin: { x: -10, y: 1, z: 3.5 }, dir: { x: 1, y: 0, z: 0 } })).toBeCloseTo(1.5);
    expect(alongOf(g, { x: 7, y: 3, z: 0.4 })).toBeCloseTo(-1.6);
  });
});

describe('offset outline (decision 600)', () => {
  it('lends each edge\'s middle the outline, its outward normal and its update', () => {
    const square = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 4 }, { x: 0, y: 4 }];
    const save = (o: { pts: { x: number; y: number }[] }) => ({ command: 'x.update', args: { points: o.pts.map((p) => [p.x, p.y]) } });
    const e0 = outlineGrips({ pts: square }, save).find((g) => g.key === 'e0')!;
    expect(e0.offsetAll!.normal.y).toBeCloseTo(-1);
    // Clockwise, outward is still away from the inside: its first edge is the top one, outward up.
    const cw = outlineGrips({ pts: [...square].reverse() }, save).find((g) => g.key === 'e0')!;
    expect(cw.offsetAll!.normal.y).toBeCloseTo(1);
    expect(offsetDistance({ x: 2, y: 0 }, e0.offsetAll!.normal, { x: 3, y: -0.4 })).toBeCloseTo(0.4);
    expect(e0.offsetAll!.save({ pts: square })).toEqual(save({ pts: square }));
  });
});
