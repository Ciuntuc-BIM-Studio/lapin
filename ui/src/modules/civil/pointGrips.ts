/**
 * A terrain's survey points as 3D height grips (T3, viewport/Grips3D.tsx drags them; civil.moveTerrainPoint, one undo step
 * each). A LandXML surface can hold thousands of points, more than can be drawn or picked: only the MAX_POINT_GRIPS
 * nearest the 3D view's centre (the camera's target, in plan) get a grip, and moving the view brings others in reach.
 */

import type { Grip3D, Vec3 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { num } from '../../engine/model';
import { useSession } from '../../engine/session';

/** The most survey points that get a grip at once. */
export const MAX_POINT_GRIPS = 400;

const UP: Vec3 = { x: 0, y: 0, z: 1 };

/**
 * The height grips of a terrain's survey points nearest `centre` in plan, at most `max`; `base`: what model heights
 * stand on (the storey's elevation). A survey height is z − datum in the model.
 */
export function terrainPointGrips(n: NodeDto, centre: { x: number; y: number }, base = 0, max = MAX_POINT_GRIPS): Grip3D[] {
  if (!Array.isArray(n.props.points)) return [];
  const datum = typeof n.props.datum === 'number' ? n.props.datum : 0;
  const pts: { i: number; x: number; y: number; z: number; d: number }[] = [];
  (n.props.points as unknown[]).forEach((pt, i) => {
    if (!Array.isArray(pt) || pt.length < 3) return;
    const [x, y, z] = pt as [number, number, number];
    if (![x, y, z].every(Number.isFinite)) return;
    pts.push({ i, x, y, z, d: (x - centre.x) ** 2 + (y - centre.y) ** 2 });
  });
  if (pts.length > max) pts.sort((a, b) => a.d - b.d).length = max;
  return pts.map((p) => ({
    key: `pt${p.i}`,
    kind: 'height' as const,
    at: { x: p.x, y: p.y, z: base + p.z - datum },
    dir: UP,
    title: 'Survey point height',
    step: (by: number) => ({ command: 'civil.moveTerrainPoint', args: { id: n.id, index: p.i, dz: by } }),
  }));
}

/** The storey elevation a terrain's model heights stand on. */
export const terrainBase = (n: NodeDto): number => num(n.storeyId ? useSession.getState().nodes.get(n.storeyId) : undefined, 'elevation');
