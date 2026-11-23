/**
 * A terrain's survey points as 3D height grips (T3, viewport/Grips3D.tsx drags them; civil.moveTerrainPoint, one undo step
 * each). A LandXML surface can hold thousands of points, more than can be drawn or picked: only the MAX_POINT_GRIPS
 * nearest the 3D view's centre (the camera's target, in plan) get a grip, and moving the view brings others in reach.
 *
 * The control cage (issue #113): `cage` shows grips on the terrain's control points only (props.cage, their places in
 * plan); moving one, the engine moves the free points by the cage's interpolation (civil/Cage.cs). `pick` shows every
 * point with the controls marked, a click making a point a control or a free point again.
 *
 * Site grading T1 (docs/plan-site-grading.md, forum topic 230): a point outside the terrain's boundary (a survey point the
 * outline cut away) gets no grip; with every point shown, a click picks a point (drawn filled) to type its elevation in the
 * terrain's properties, many at once. In plan, the same points get grips whose right-click sets the elevation or picks the
 * point; a point on the outline's corners shares its grip with the boundary's corner.
 */

import type { Grip, Grip3D, Vec2, Vec3 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { num } from '../../engine/model';
import { useSession } from '../../engine/session';
import { arcsOf, fromPairs, outlinePoints } from '../../viewport/contour';
import { inLoop } from '../../viewport/outlineShape';
import { pickedOf, togglePicked } from './prefs';

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
  const pts = gripPoints(n, centre, max, mode === 'cage' ? controls : null);
  const picked = mode === 'all' ? new Set(pickedOf(n.id)) : null;
  return pts.map((p) => {
    const g: Grip3D = {
      key: `pt${p.i}`,
      kind: 'height' as const,
      at: { x: p.x, y: p.y, z: base + p.z - datum },
      dir: UP,
      title: mode === 'cage' ? 'Control point height' : 'Survey point height',
      step: (by: number) => ({ command: 'civil.moveTerrainPoint', args: { id: n.id, index: p.i, dz: by } }),
    };
    // Controls are drawn filled in both modes that show them (every grip is one in the cage).
    if (mode !== 'all') g.marked = controls.has(p.i);
    if (mode === 'pick') {
      g.click = () => ({ command: 'civil.setTerrainControls', args: { id: n.id, indices: [p.i], mode: 'toggle' } });
    }
    // Every point shown: a click picks it, to type its elevation (no command: nothing changes until the elevation is set).
    if (picked) {
      g.marked = picked.has(p.i);
      g.click = () => { togglePicked(n.id, p.i); return null; };
    }
    return g;
  });
}

/** A survey point with a grip: its index, its place (survey height) and how far it is from the centre, squared. */
interface GripPoint { i: number; x: number; y: number; z: number; d: number }

/** How far outside its drawn outline a point still counts as on it (the screen lays an arc coarser than the engine). */
const ON_OUTLINE = 0.05;

/** The terrain's boundary as drawn, an arc in short steps; none when it has no boundary. */
export function boundaryLoop(n: NodeDto): Vec2[] | null {
  const pts = fromPairs(n.props.boundary);
  return pts.length >= 3 ? outlinePoints({ pts, arcs: arcsOf(n.props.boundaryArcs, pts.length) }) : null;
}

const segDist = (p: Vec2, a: Vec2, b: Vec2): number => {
  const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
  return Math.hypot(p.x - a.x - dx * t, p.y - a.y - dy * t);
};

/** Whether a point is on the ground a boundary keeps: inside it, or on it. */
export function withinBoundary(loop: Vec2[] | null, p: Vec2): boolean {
  if (!loop || inLoop(loop, p)) return true;
  return loop.some((a, i) => segDist(p, a, loop[(i + 1) % loop.length]) <= ON_OUTLINE);
}

/** The survey points that get a grip: on the ground the boundary keeps, (given) among `only`, the `max` nearest `centre`. */
function gripPoints(n: NodeDto, centre: { x: number; y: number }, max: number, only: Set<number> | null): GripPoint[] {
  if (!Array.isArray(n.props.points)) return [];
  const loop = boundaryLoop(n);
  const pts: GripPoint[] = [];
  (n.props.points as unknown[]).forEach((pt, i) => {
    if (!Array.isArray(pt) || pt.length < 3) return;
    if (only && !only.has(i)) return;
    const [x, y, z] = pt as [number, number, number];
    if (![x, y, z].every(Number.isFinite)) return;
    if (!withinBoundary(loop, { x, y })) return;
    pts.push({ i, x, y, z, d: (x - centre.x) ** 2 + (y - centre.y) ** 2 });
  });
  if (pts.length > max) pts.sort((a, b) => a.d - b.d).length = max;
  return pts;
}

/** The indices of the survey points standing on the terrain's outline as built (to the millimetre, its arcs in short steps). */
export function outlinePointIndices(n: NodeDto): number[] {
  const loop = boundaryLoop(n);
  if (!loop || !Array.isArray(n.props.points)) return [];
  const o: number[] = [];
  (n.props.points as unknown[]).forEach((pt, i) => {
    if (!Array.isArray(pt) || pt.length < 3) return;
    const p = { x: pt[0] as number, y: pt[1] as number };
    if (loop.some((a, k) => segDist(p, a, loop[(k + 1) % loop.length]) <= ON_OUTLINE)) o.push(i);
  });
  return o;
}

/** A point's elevation typed: as one command, the points picked with it when it is one of them. */
export function elevationStep(n: NodeDto, index: number, z: number): { command: string; args: Record<string, unknown> } {
  const picked = pickedOf(n.id);
  return picked.length > 1 && picked.includes(index)
    ? { command: 'civil.setTerrainPoints', args: { id: n.id, indices: picked, z } }
    : { command: 'civil.moveTerrainPoint', args: { id: n.id, index, z } };
}

/**
 * The terrain's points as grips in plan (T1): each one's right-click sets its elevation (`ask` gives it, in metres, or null)
 * or picks it; a point on a corner of the boundary takes no grip of its own (the corner's grip there gets the same menu, by
 * `decorate`). Not dragged: a point is moved in plan by its outline, or in 3D up and down.
 */
export function terrainPlanPointGrips(n: NodeDto, centre: { x: number; y: number }, ask: (now: number) => number | null, max = MAX_POINT_GRIPS): Grip[] {
  const datum = typeof n.props.datum === 'number' ? n.props.datum : 0;
  const corners = fromPairs(n.props.boundary);
  const picked = new Set(pickedOf(n.id));
  return gripPoints(n, centre, max, null)
    .filter((p) => !corners.some((c) => Math.hypot(c.x - p.x, c.y - p.y) <= 1e-3))
    .map((p) => ({
      key: `pt${p.i}`, kind: 'centre' as const, at: { x: p.x, y: p.y }, title: picked.has(p.i) ? 'Survey point (picked)' : 'Survey point',
      menu: pointMenu(n, p.i, p.z - datum, ask),
    }));
}

/** What a point's grip offers on a right-click: its elevation typed, it picked or let go. */
function pointMenu(n: NodeDto, index: number, now: number, ask: (now: number) => number | null): NonNullable<Grip['menu']> {
  const picked = pickedOf(n.id).includes(index);
  return [
    { label: 'Set elevation…', run: () => { const z = ask(now); return z == null ? null : elevationStep(n, index, z); } },
    { label: picked ? 'Unpick point' : 'Pick point', run: () => { togglePicked(n.id, index); return null; } },
  ];
}

/** The boundary's corner grips with the menu of the point standing there (a terrain whose outline is its points). */
export function decorateCornerGrips(n: NodeDto, grips: Grip[], ask: (now: number) => number | null): Grip[] {
  if (!Array.isArray(n.props.points)) return grips;
  const datum = typeof n.props.datum === 'number' ? n.props.datum : 0;
  const pts = n.props.points as unknown[];
  return grips.map((g) => {
    if (g.kind !== 'vertex') return g;
    const i = pts.findIndex((pt) => Array.isArray(pt) && Math.hypot((pt[0] as number) - g.at.x, (pt[1] as number) - g.at.y) <= 1e-3);
    if (i < 0) return g;
    const z = (pts[i] as number[])[2];
    return { ...g, menu: [...(g.menu ?? []), ...pointMenu(n, i, z - datum, ask)] };
  });
}

/** The storey elevation a terrain's model heights stand on. */
export const terrainBase = (n: NodeDto): number => num(n.storeyId ? useSession.getState().nodes.get(n.storeyId) : undefined, 'elevation');
