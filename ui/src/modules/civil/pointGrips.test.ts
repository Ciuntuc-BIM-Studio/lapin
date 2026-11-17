import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { MAX_POINT_GRIPS, terrainPointGrips } from './pointGrips';

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
