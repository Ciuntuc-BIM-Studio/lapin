/**
 * A terrain's survey points as 3D height grips (T3, viewport/Grips3D.tsx drags them; civil.moveTerrainPoint, one undo step
 * each). A LandXML surface can hold thousands of points, more than can be drawn or picked: only the MAX_POINT_GRIPS
 * nearest the 3D view's centre (the camera's target, in plan) get a grip, and moving the view brings others in reach.
 *
 * The control cage (issue #113): `cage` shows grips on the terrain's control points only (props.cage, their places in
 * plan); moving one, the engine moves the free points by the cage's interpolation (civil/Cage.cs). `pick` shows every
 * point with the controls marked, a click making a point a control or a free point again.
 */

import type { Grip3D, Vec3 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { num } from '../../engine/model';
import { useSession } from '../../engine/session';

/** The most survey points that get a grip at once. */
export const MAX_POINT_GRIPS = 400;

/** Which points get a grip: every one (today's), the control cage's, or every one to choose the controls. */
export type PointGripMode = 'all' | 'cage' | 'pick';

const UP: Vec3 = { x: 0, y: 0, z: 1 };

/** The indices of the terrain's control points: the survey points standing on its `cage` places, to the millimetre. */
export function terrainControls(n: NodeDto): Set<number> {
  const found = new Set<number>();
  if (!Array.isArray(n.props.cage) || !Array.isArray(n.props.points)) return found;
  const pts = n.props.points as unknown[];
  for (const c of n.props.cage as unknown[]) {
    if (!Array.isArray(c) || c.length < 2) continue;
    const [cx, cy] = c as [number, number];
    let best = -1, bd = 1e-3 + 1e-9;
    pts.forEach((pt, i) => {
      if (!Array.isArray(pt)) return;
      const d = Math.hypot((pt[0] as number) - cx, (pt[1] as number) - cy);
      if (d <= bd) { bd = d; best = i; }
    });
    if (best >= 0) found.add(best);
  }
  return new Set([...found].sort((a, b) => a - b));
}

/**
 * The height grips of a terrain's survey points nearest `centre` in plan, at most `max`; `base`: what model heights
 * stand on (the storey's elevation). A survey height is z − datum in the model.
 */
export function terrainPointGrips(n: NodeDto, centre: { x: number; y: number }, base = 0, max = MAX_POINT_GRIPS, mode: PointGripMode = 'all'): Grip3D[] {
  if (!Array.isArray(n.props.points)) return [];
  const datum = typeof n.props.datum === 'number' ? n.props.datum : 0;
  const controls = mode === 'all' ? new Set<number>() : terrainControls(n);
  const pts: { i: number; x: number; y: number; z: number; d: number }[] = [];
  (n.props.points as unknown[]).forEach((pt, i) => {
    if (!Array.isArray(pt) || pt.length < 3) return;
    if (mode === 'cage' && !controls.has(i)) return;
    const [x, y, z] = pt as [number, number, number];
    if (![x, y, z].every(Number.isFinite)) return;
    pts.push({ i, x, y, z, d: (x - centre.x) ** 2 + (y - centre.y) ** 2 });
  });
  if (pts.length > max) pts.sort((a, b) => a.d - b.d).length = max;
  return pts.map((p) => {
    const g: Grip3D = {
      key: `pt${p.i}`,
      kind: 'height' as const,
      at: { x: p.x, y: p.y, z: base + p.z - datum },
      dir: UP,
      title: mode === 'cage' ? 'Control point height' : 'Survey point height',
      step: (by: number) => ({ command: 'civil.moveTerrainPoint', args: { id: n.id, index: p.i, dz: by } }),
    };
    if (mode === 'pick') {
      g.marked = controls.has(p.i);
      g.click = () => ({ command: 'civil.setTerrainControls', args: { id: n.id, indices: [p.i], mode: 'toggle' } });
    }
    return g;
  });
}

/** The storey elevation a terrain's model heights stand on. */
export const terrainBase = (n: NodeDto): number => num(n.storeyId ? useSession.getState().nodes.get(n.storeyId) : undefined, 'elevation');
