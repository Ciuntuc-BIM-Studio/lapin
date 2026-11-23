import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { MAX_POINT_GRIPS, boundaryLoop, decorateCornerGrips, elevationStep, outlinePointIndices, terrainControls, terrainPlanPointGrips, terrainPointGrips, withinBoundary } from './pointGrips';
import { pickedOf, setPicked, useTerrainEdit } from './prefs';

const terrain = (points: number[][], datum = 100): NodeDto =>
  ({ id: 't1', type: 'IfcGeographicElement', name: 'Terrain', props: { predefinedType: 'TERRAIN', points, datum } } as unknown as NodeDto);

describe('a terrain\'s survey point grips', () => {
  it('stand at each point\'s model height (survey height less the datum, on the storey) and move it as one command', () => {
    const g = terrainPointGrips(terrain([[0, 0, 101], [10, 0, 102.5], [0, 10, 100]]), { x: 0, y: 0 }, 3);
    expect(g.map((x) => x.at.z)).toEqual([4, 5.5, 3]);
    expect(g[1].dir).toEqual({ x: 0, y: 0, z: 1 });
    expect(g[1].step(0.25)).toEqual({ command: 'civil.moveTerrainPoint', args: { id: 't1', index: 1, dz: 0.25 } });
  });

  it('are only the nearest to the view\'s centre on a large survey, keeping each point\'s own index', () => {
    const pts: number[][] = [];
    for (let i = 0; i < 100; i++) for (let j = 0; j < 100; j++) pts.push([i, j, 100]);
    const g = terrainPointGrips(terrain(pts), { x: 80, y: 80 });
    expect(g).toHaveLength(MAX_POINT_GRIPS);
    expect(Math.max(...g.map((x) => Math.hypot(x.at.x - 80, x.at.y - 80)))).toBeLessThan(12);
    expect(g.some((x) => x.key === `pt${80 * 100 + 80}`)).toBe(true);
    expect(terrainPointGrips(terrain(pts.slice(0, 50)), { x: 0, y: 0 })).toHaveLength(50);
  });
});

describe('a terrain\'s control cage in 3D (issue #113)', () => {
  const caged = (cage?: number[][]): NodeDto =>
    ({ id: 't1', type: 'IfcGeographicElement', name: 'Terrain', props: { predefinedType: 'TERRAIN', points: [[0, 0, 0], [10, 0, 0], [0, 10, 0], [10, 10, 0]], datum: 0, cage } } as unknown as NodeDto);

  it('knows its controls by their place in plan, to the millimetre', () => {
    expect([...terrainControls(caged([[10, 0], [0, 10.0004], [5, 5]]))]).toEqual([1, 2]);
    expect(terrainControls(caged()).size).toBe(0);
  });

  it('shows grips on the controls only in the cage, each moving its point as before (the engine moves the free ones)', () => {
    const g = terrainPointGrips(caged([[10, 10]]), { x: 0, y: 0 }, 0, MAX_POINT_GRIPS, 'cage');
    expect(g.map((x) => x.key)).toEqual(['pt3']);
    expect(g[0].title).toBe('Control point height');
    expect(g[0].step(1)).toEqual({ command: 'civil.moveTerrainPoint', args: { id: 't1', index: 3, dz: 1 } });
    expect(terrainPointGrips(caged(), { x: 0, y: 0 }, 0, MAX_POINT_GRIPS, 'cage')).toEqual([]);
  });

  it('draws every control filled (marked) in the controls-only mode, not just the one touched', () => {
    const g = terrainPointGrips(caged([[10, 0], [0, 10], [10, 10]]), { x: 0, y: 0 }, 0, MAX_POINT_GRIPS, 'cage');
    expect(g).toHaveLength(3);
    expect(g.every((x) => x.marked === true)).toBe(true);
  });

  it('marks the controls among every point while choosing them, a click making a point a control or free again', () => {
    const g = terrainPointGrips(caged([[10, 0]]), { x: 0, y: 0 }, 0, MAX_POINT_GRIPS, 'pick');
    expect(g).toHaveLength(4);
    expect(g.map((x) => !!x.marked)).toEqual([false, true, false, false]);
    expect(g[2].click?.()).toEqual({ command: 'civil.setTerrainControls', args: { id: 't1', indices: [2], mode: 'toggle' } });
  });

  it('is today\'s every-point editing when not asked for, a click only picking a point (no command)', () => {
    useTerrainEdit.setState({ picked: null });
    const g = terrainPointGrips(caged([[10, 0]]), { x: 0, y: 0 });
    expect(g).toHaveLength(4);
    expect(g.every((x) => !x.marked)).toBe(true);
    expect(g[1].click?.()).toBeNull();
    expect(pickedOf('t1')).toEqual([1]);
  });
});

describe('a terrain on its outline (site grading T1)', () => {
  const square = (extra: Record<string, unknown> = {}): NodeDto =>
    ({ id: 't2', type: 'IfcGeographicElement', name: 'Terrain', props: { predefinedType: 'TERRAIN', datum: 0, boundary: [[0, 0], [10, 0], [10, 10], [0, 10]], points: [[0, 0, 1], [10, 0, 2], [10, 10, 3], [0, 10, 4], [5, 5, 2.5], [14, 5, 9], [10.02, 5, 2]], ...extra } } as unknown as NodeDto);

  it('gives no grip to a point the boundary cut away, but one on the outline keeps it', () => {
    useTerrainEdit.setState({ picked: null });
    const keys = terrainPointGrips(square(), { x: 0, y: 0 }).map((g) => g.key);
    expect(keys).toEqual(['pt0', 'pt1', 'pt2', 'pt3', 'pt4', 'pt6']);
    expect(withinBoundary(boundaryLoop(square()), { x: 14, y: 5 })).toBe(false);
    expect(withinBoundary(null, { x: 14, y: 5 })).toBe(true);
  });

  it('knows the points on the outline, an arc\'s too', () => {
    expect(outlinePointIndices(square())).toEqual([0, 1, 2, 3, 6]);
    // The north edge bulging 2 m out: (5, 12) stands on it.
    const arc = square({ boundaryArcs: [0, 0, -2, 0], points: [[0, 0, 0], [10, 0, 0], [10, 10, 0], [0, 10, 0], [5, 12, 0]] });
    expect(outlinePointIndices(arc)).toContain(4);
    expect(terrainPointGrips(arc, { x: 0, y: 0 }).map((g) => g.key)).toContain('pt4');
  });

  it('marks the points picked, and types one elevation for all of them in one command', () => {
    setPicked('t2', [0, 2]);
    const g = terrainPointGrips(square(), { x: 0, y: 0 });
    expect(g.filter((x) => x.marked).map((x) => x.key)).toEqual(['pt0', 'pt2']);
    expect(elevationStep(square(), 2, 7.5)).toEqual({ command: 'civil.setTerrainPoints', args: { id: 't2', indices: [0, 2], z: 7.5 } });
    // A point not picked: that point alone, its exact elevation.
    expect(elevationStep(square(), 4, 1)).toEqual({ command: 'civil.moveTerrainPoint', args: { id: 't2', index: 4, z: 1 } });
    setPicked('t2', []);
    expect(useTerrainEdit.getState().picked).toBeNull();
  });

  it('in plan, gives the points not on a corner a grip whose right-click sets the elevation, and a corner\'s grip the same menu', () => {
    useTerrainEdit.setState({ picked: null });
    const asked: number[] = [];
    const ask = (now: number) => { asked.push(now); return 6; };
    const plan = terrainPlanPointGrips(square(), { x: 0, y: 0 }, ask);
    expect(plan.map((g) => g.key)).toEqual(['pt4', 'pt6']);
    expect(plan[0].step).toBeUndefined();
    expect(plan[0].menu?.map((m) => m.label)).toEqual(['Set elevation…', 'Pick point']);
    expect(plan[0].menu?.[0].run()).toEqual({ command: 'civil.moveTerrainPoint', args: { id: 't2', index: 4, z: 6 } });
    expect(asked).toEqual([2.5]);
    const corner = decorateCornerGrips(square(), [{ key: 'v2', kind: 'vertex', at: { x: 10, y: 10 } }, { key: 'e0', kind: 'edgeMid', at: { x: 5, y: 0 } }], ask);
    expect(corner[0].menu?.map((m) => m.label)).toEqual(['Set elevation…', 'Pick point']);
    expect(corner[0].menu?.[0].run()).toEqual({ command: 'civil.moveTerrainPoint', args: { id: 't2', index: 2, z: 6 } });
    expect(corner[1].menu).toBeUndefined();
    corner[0].menu?.[1].run();
    expect(pickedOf('t2')).toEqual([2]);
  });
});
