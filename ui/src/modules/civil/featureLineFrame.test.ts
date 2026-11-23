import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { featureLineGrips3d } from './featureLineTools';
import { terrainPointGrips } from './pointGrips';

/**
 * The 3D view draws a terrain's body at its model heights (z − datum), whatever its storey's elevation: the engine builds it
 * so (FeatureLineFrameTests). A feature line's height grips and a terrain point's stand in that same frame, on a storey at
 * +1.20 too, so they sit on the surface.
 */
const node = (type: string, props: Record<string, unknown>): NodeDto => ({ id: 'x', type, name: type, storeyId: 's1', props } as unknown as NodeDto);

describe('3D grips in the terrain\'s own frame', () => {
  it('a feature line\'s corner grips stand at the corners\' resolved heights, no storey elevation added', () => {
    const n = node('LapinFeatureLine', { points: [[4, 4], [20, 9, 1.5]], cornerZ: [0.26, 1.5], ownZ: [false, true], segmentLengths: [16.76] });
    const g = featureLineGrips3d(n);
    expect(g.map((x) => x.at.z)).toEqual([0.26, 1.5]);
  });
  it('a terrain point\'s grip stands at z − datum', () => {
    const n = node('IfcGeographicElement', { predefinedType: 'TERRAIN', datum: 100, points: [[0, 0, 100.5], [3, 0, 101], [0, 3, 100.2]] });
    const g = terrainPointGrips(n, { x: 0, y: 0 });
    const zs = g.map((x) => x.at.z).sort((a, b) => a - b);
    [0.2, 0.5, 1].forEach((v, i) => expect(zs[i]).toBeCloseTo(v, 9));
  });
});
