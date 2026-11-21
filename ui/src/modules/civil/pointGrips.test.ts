import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { MAX_POINT_GRIPS, terrainControls, terrainPointGrips } from './pointGrips';

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

  it('is today\'s every-point editing when not asked for', () => {
    const g = terrainPointGrips(caged([[10, 0]]), { x: 0, y: 0 });
    expect(g).toHaveLength(4);
    expect(g.every((x) => !x.marked && !x.click)).toBe(true);
  });
});
