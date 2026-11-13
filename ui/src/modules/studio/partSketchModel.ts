/**
 * Editing a part's own sketch in place (docs/plan-studio-parametric.md §3.2, decision 520): the pure pieces. A sketch's shapes are
 * in its plane's 2D coordinates (u, v), mapped to the world by the sketch's frame (origin + u·right + v·up); the camera looks
 * straight at that plane; the drawing tools make shapes the whole list of which is sent back with their ids kept
 * (`studio.partSketchSetShapes`); the engine solves the constraints and drags.
 */

import type { Vec2 } from '../../platform/module';
import { arcThrough, bounds, dist, fromJson, toJson, type Shape } from '../../viewport/cad/shapes';
import { pointOf } from '../../viewport/cad/constraints';
import type { CameraPose } from '../../viewport/camera';

export interface V3 { x: number; y: number; z: number }
export interface SketchFrame { origin: number[]; right: number[]; up: number[]; normal: number[] }

const v3 = (a: number[]): V3 => ({ x: a[0] ?? 0, y: a[1] ?? 0, z: a[2] ?? 0 });
const dot = (a: V3, b: V3) => a.x * b.x + a.y * b.y + a.z * b.z;

/** A sketch point as the world's: origin + u·right + v·up. */
export function sketchToWorld(f: SketchFrame, p: Vec2): V3 {
  const o = v3(f.origin), r = v3(f.right), u = v3(f.up);
  return { x: o.x + p.x * r.x + p.y * u.x, y: o.y + p.x * r.y + p.y * u.y, z: o.z + p.x * r.z + p.y * u.z };
}

/** A world point's (u, v) in the sketch's plane (the point projected on it). */
export function worldToSketch(f: SketchFrame, p: V3): Vec2 {
  const o = v3(f.origin), d = { x: p.x - o.x, y: p.y - o.y, z: p.z - o.z };
  return { x: dot(d, v3(f.right)), y: dot(d, v3(f.up)) };
}

/** Where a ray (from the camera through a screen point) meets the sketch's plane, as (u, v); null when it runs parallel or the plane is behind. */
export function rayToSketch(f: SketchFrame, origin: V3, dir: V3): Vec2 | null {
  const n = v3(f.normal), o = v3(f.origin);
  const den = dot(n, dir);
  if (Math.abs(den) < 1e-9) return null;
  const t = dot(n, { x: o.x - origin.x, y: o.y - origin.y, z: o.z - origin.z }) / den;
  if (t < 0) return null;
  return worldToSketch(f, { x: origin.x + dir.x * t, y: origin.y + dir.y * t, z: origin.z + dir.z * t });
}

/** The sketch's points on the screen, through any projection (null for a point the camera cannot show). */
export const projectSketch = (f: SketchFrame, pts: readonly Vec2[], project: (p: V3) => { x: number; y: number } | null): ({ x: number; y: number } | null)[] => pts.map((p) => project(sketchToWorld(f, p)));

// ─── The camera looking straight at the plane ───────────────────────────────────

/** The pose that looks at a sketch's plane head on (against its normal, its up up the screen), at `centre` (u, v) from a distance that frames `extent` metres. */
export function poseForSketch(f: SketchFrame, centre: Vec2, extent: number, fov: number): CameraPose {
  const d = v3(f.normal), look = { x: -d.x, y: -d.y, z: -d.z };
  const target = sketchToWorld(f, centre);
  const flat = Math.hypot(look.x, look.y);
  let azimuth: number, pitch: number;
  if (flat < 1e-4) {
    // Straight down or up: the screen's up is the azimuth's direction in plan.
    const up = v3(f.up);
    azimuth = Math.atan2(up.y, up.x);
    pitch = look.z < 0 ? -Math.PI / 2 + 0.01 : Math.PI / 2 - 0.01;
  } else { azimuth = Math.atan2(look.y, look.x); pitch = Math.atan2(look.z, flat); }
  const distance = Math.max(0.5, (Math.max(extent, 0.2) * 1.4) / (2 * Math.tan(Math.max(fov, 0.2) / 2)));
  return { azimuth, pitch, distance, target };
}

/** The middle and size (metres) of what a sketch draws; a unit square round the origin when it has nothing. */
export function sketchExtent(shapes: readonly { shape: Shape }[]): { centre: Vec2; extent: number } {
  if (!shapes.length) return { centre: { x: 0, y: 0 }, extent: 1 };
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const s of shapes) { const b = bounds(s.shape); x0 = Math.min(x0, b.min.x); y0 = Math.min(y0, b.min.y); x1 = Math.max(x1, b.max.x); y1 = Math.max(y1, b.max.y); }
  return { centre: { x: (x0 + x1) / 2, y: (y0 + y1) / 2 }, extent: Math.max(x1 - x0, y1 - y0, 0.2) };
}

// ─── The shapes, as the engine lists them ──────────────────────────────────────

export interface LocalShape { id: string; shape: Shape; inProfile?: boolean }

/** A sketch's shapes as the part tree gives them ({id, type, …}); one that does not read is left out. */
export function localShapes(raw: unknown): LocalShape[] {
  const out: LocalShape[] = [];
  for (const o of (Array.isArray(raw) ? raw : []) as Record<string, unknown>[]) {
    const s = fromJson(o);
    if (s && typeof o.id === 'string') out.push({ id: o.id, shape: s, ...(o.inProfile ? { inProfile: true } : {}) });
  }
  return out;
}

/** The list `studio.partSketchSetShapes` takes: every shape with its id (a new shape has none: the engine gives it one). */
export const shapesPayload = (list: readonly { id?: string; shape: Shape }[]): Record<string, unknown>[] => list.map((s) => ({ ...(s.id ? { id: s.id } : {}), ...toJson(s.shape) }));

/** The list with a shape added (no id yet). */
export const withShape = (list: readonly LocalShape[], shape: Shape): { id?: string; shape: Shape }[] => [...list.map((s) => ({ id: s.id, shape: s.shape })), { shape }];
/** The list without a shape; the others keep their ids. */
export const withoutShape = (list: readonly LocalShape[], id: string): { id?: string; shape: Shape }[] => list.filter((s) => s.id !== id).map((s) => ({ id: s.id, shape: s.shape }));
/** The list with one shape replaced by another of the same id. */
export const withReplaced = (list: readonly LocalShape[], id: string, shape: Shape): { id?: string; shape: Shape }[] => list.map((s) => ({ id: s.id, shape: s.id === id ? shape : s.shape }));

// ─── The drawing tools ──────────────────────────────────────────────────────────

export type DrawTool = 'line' | 'rectangle' | 'circle' | 'arc';
export const DRAW_TOOLS: DrawTool[] = ['line', 'rectangle', 'circle', 'arc'];

/** The tools of sketch editing, in the order the toolbar shows them (the constraint tools are the CAD commands, beside). */
export type SketchTool = DrawTool | 'delete';
export const SKETCH_TOOLS: SketchTool[] = ['line', 'rectangle', 'circle', 'arc', 'delete'];

/** The points a tool asks for before it makes its shape. */
export const POINTS_NEEDED: Record<DrawTool, number> = { line: 2, rectangle: 2, circle: 2, arc: 3 };

/** What the sketch editing allows: drawing and deleting while no constraint command runs, the constraints always (not moving, copying, extruding: those belong to the model). */
export function toolAllowed(tool: string, o: { constraintCommand: boolean }): boolean {
  if (tool.startsWith('studio.constrain.') || tool.startsWith('CN') || tool === 'CDIM') return true;
  return (SKETCH_TOOLS as string[]).includes(tool) && !o.constraintCommand;
}

/** The shape the points make, or null while they are not enough or make nothing (a line of no length, three points on a line). */
export function shapeFrom(tool: DrawTool, pts: readonly Vec2[]): Shape | null {
  if (pts.length < POINTS_NEEDED[tool]) return null;
  const [a, b, c] = pts;
  switch (tool) {
    case 'line': return dist(a, b) > 1e-6 ? { type: 'line', a, b } : null;
    case 'rectangle': return Math.abs(a.x - b.x) > 1e-6 && Math.abs(a.y - b.y) > 1e-6
      ? { type: 'polyline', pts: [{ x: a.x, y: a.y }, { x: b.x, y: a.y }, { x: b.x, y: b.y }, { x: a.x, y: b.y }], closed: true } : null;
    case 'circle': return dist(a, b) > 1e-6 ? { type: 'circle', c: a, r: dist(a, b) } : null;
    case 'arc': return arcThrough(a, b, c);
  }
}

/** What a tool draws while it waits for its next point: the points so far and the cursor as the shape they would make (or the line to it). */
export function previewShapes(tool: DrawTool, pts: readonly Vec2[], cursor: Vec2 | null): Shape[] {
  if (!pts.length || !cursor) return [];
  const all = [...pts, cursor];
  const made = shapeFrom(tool, all);
  if (made) return [made];
  return all.length >= 2 ? [{ type: 'polyline', pts: all.map((p) => ({ ...p })), closed: false }] : [];
}

/** The nearest shape within `tol` of a point (a click to delete or select), or null. */
export function shapeAt(list: readonly LocalShape[], p: Vec2, tol: number, near: (s: Shape, p: Vec2) => number): LocalShape | null {
  let best: LocalShape | null = null, bd = tol;
  for (const s of list) { const d = near(s.shape, p); if (d <= bd) { best = s; bd = d; } }
  return best;
}

// ─── Grips ────────────────────────────────────────────────────────────────────────

export interface Grip { shape: string; part: string; at: Vec2 }

/** The points of every shape that can be dragged: a line's ends, a polyline's vertices, a circle's centre, an arc's ends and centre. */
export function gripsOf(list: readonly LocalShape[]): Grip[] {
  const out: Grip[] = [];
  for (const { id, shape } of list) {
    const parts = shape.type === 'line' ? ['start', 'end'] : shape.type === 'polyline' ? shape.pts.map((_, i) => `vertex:${i}`) : shape.type === 'circle' ? ['center'] : ['start', 'end', 'center'];
    for (const part of parts) { const at = pointOf(shape, part); if (at) out.push({ shape: id, part, at }); }
  }
  return out;
}

/** What the engine answers a drag with: the shapes ({id, …} or {id, shape}) as shapes by id. */
export function dragShapes(raw: unknown): { id: string; shape: Shape }[] {
  const out: { id: string; shape: Shape }[] = [];
  for (const x of (Array.isArray(raw) ? raw : []) as Record<string, unknown>[]) {
    const s = fromJson((x.shape && typeof x.shape === 'object' ? x.shape : x) as Record<string, unknown>);
    if (s && typeof x.id === 'string') out.push({ id: x.id, shape: s });
  }
  return out;
}

/** The nearest grip within `tol` of a point (snapping a new point onto an existing one), or null. */
export function snapToGrip(grips: readonly Grip[], p: Vec2, tol: number): Grip | null {
  let best: Grip | null = null, bd = tol;
  for (const g of grips) { const d = dist(g.at, p); if (d <= bd) { best = g; bd = d; } }
  return best;
}

// ─── The sketch's state, for the chip ───────────────────────────────────────────────

/** The part tree's local sketch, with the fields the editing reads. */
export interface LocalSketchState { status?: string; dof?: number; freeShapes?: { shape: string; part: string }[]; redundant?: string[]; conflicting?: string[]; problem?: string }

/** The status as the chip and the overlay read it (the view store's schema). */
export const statusOf = (s: LocalSketchState): { status: string; dof: number; freeShapes: { shape: string; part: string }[]; redundant: string[]; conflicting: string[]; problem?: string } => ({
  status: s.status ?? 'unknown', dof: s.dof ?? 0, freeShapes: s.freeShapes ?? [], redundant: s.redundant ?? [], conflicting: s.conflicting ?? [], ...(s.problem ? { problem: s.problem } : {}),
});
