/**
 * Drafting in the 3D view (a tester's report, decision 312): the Draft tab's
 * commands (LINE, PLINE, RECTANGLE, CIRCLE, ARC, OFFSET…) draw on a work
 * plane, the active storey's level by default (its plan's drafts, the same
 * element as one drawn on the plan) or the reference plane selected (Studio's
 * Plane on face makes one on any face picked). The cursor's ray meets the
 * plane; the point is then the plane's own (x, y), aimed and snapped as on
 * the plan (the plan's cut lines and its drafts). This holds the plane's
 * geometry; Draft3D.tsx the mouse and the overlay.
 */

import type { NodeDto } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import type { Vec3 } from './camera';
import { planeOf } from './refPlanes';

/** A work plane: a point (x, y) on it is origin + right·x + up·y; normal faces the side it is seen from. */
export interface WorkFrame { origin: Vec3; right: Vec3; up: Vec3; normal: Vec3 }

const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
const num = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? v : 0);

/** A plan's level: (x, y) the model's, at the storey's elevation, facing up. */
export const levelFrame = (z: number): WorkFrame => ({ origin: { x: 0, y: 0, z }, right: { x: 1, y: 0, z: 0 }, up: { x: 0, y: 1, z: 0 }, normal: { x: 0, y: 0, z: 1 } });

/**
 * The plane a view's drafts lie on, as the engine's Views.PlaneOf reads it: a plan's its storey's level (the plan's own
 * view, or `plan:<storey>`), a reference plane's; null for any other view (sections and elevations are drawn on in 2D).
 */
export function frameOfView(viewId: string, nodes: ReadonlyMap<string, NodeDto>): WorkFrame | null {
  if (viewId.startsWith('plan:')) { const s = nodes.get(viewId.slice(5)); return s ? levelFrame(num(s.props.elevation)) : null; }
  const v = nodes.get(viewId);
  if (!v) return null;
  if (v.props.kind === 'plane') { const p = planeOf(v); return { origin: p.origin, right: p.right, up: p.up, normal: p.normal }; }
  if ((v.props.kind ?? 'plan') === 'plan') { const s = nodes.get(String(v.props.storeyId ?? '')); return levelFrame(num(s?.props.elevation)); }
  return null;
}

/**
 * Which view a command in 3D draws on: the reference plane selected, else the active storey's plan (its own plan
 * view when that one is open, as the plan draws on it, else the storey's).
 */
export function workPlaneView(o: { selection: string | null; planViewId: string | null; storeyId: string | null }, nodes: ReadonlyMap<string, NodeDto>): { id: string; name: string } | null {
  const sel = o.selection ? nodes.get(o.selection) : undefined;
  if (sel?.type === 'LapinView' && sel.props.kind === 'plane') return { id: sel.id, name: sel.name };
  if (!o.storeyId) return null;
  const own = o.planViewId ? nodes.get(o.planViewId) : undefined;
  if (own && own.props.storeyId === o.storeyId) return { id: own.id, name: own.name };
  return { id: `plan:${o.storeyId}`, name: nodes.get(o.storeyId)?.name ?? 'Level' };
}

/** A point of the plane in the model. */
export const toWorld = (f: WorkFrame, p: Vec2): Vec3 => ({
  x: f.origin.x + f.right.x * p.x + f.up.x * p.y,
  y: f.origin.y + f.right.y * p.x + f.up.y * p.y,
  z: f.origin.z + f.right.z * p.x + f.up.z * p.y,
});

/** A point of the model on the plane: its (x, y) there (its height off the plane dropped). */
export function toPlane(f: WorkFrame, q: Vec3): Vec2 {
  const d = { x: q.x - f.origin.x, y: q.y - f.origin.y, z: q.z - f.origin.z };
  return { x: dot(d, f.right), y: dot(d, f.up) };
}

/** Where a ray (origin, direction) meets the plane, as the plane's (x, y); null when it runs along it or points away. */
export function rayOnPlane(f: WorkFrame, origin: Vec3, dir: Vec3): Vec2 | null {
  const den = dot(dir, f.normal);
  if (Math.abs(den) < 1e-9) return null;
  const t = dot({ x: f.origin.x - origin.x, y: f.origin.y - origin.y, z: f.origin.z - origin.z }, f.normal) / den;
  if (t < 0) return null;
  return toPlane(f, { x: origin.x + dir.x * t, y: origin.y + dir.y * t, z: origin.z + dir.z * t });
}
