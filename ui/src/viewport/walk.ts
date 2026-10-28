/**
 * Walking through the model: a first-person camera put inside a room (from a
 * plan's right-click), at eye height over its storey, looking along the
 * room. Drag looks round (no roll), W A S D or the arrows walk, E and Q rise
 * and sink, the wheel steps forward and back, Shift runs; Esc goes back to
 * the camera there was. No physics: walls do not stop it.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import type { Vec3 } from './camera';
import { distToSegment, pointInPolygon } from './snap';

/** The eye over the floor (m). */
export const EYE_HEIGHT = 1.6;
/** Walking pace (m/s), and how much faster running is. */
export const WALK_SPEED = 1.4;
export const RUN_FACTOR = 3;
/** Rising and sinking pace (m/s). */
export const CLIMB_SPEED = 1;

export interface WalkStart { eye: Vec3; yaw: number; pitch: number; name: string }

/** Where a walk starts (null: not walking); the 3D view takes it up and clears it when the walk ends. */
export const useWalk = create<{ start: WalkStart | null }>(() => ({ start: null }));

export const exitWalk = () => useWalk.setState({ start: null });

/** How far a point is from an outline's nearest edge. */
function clearance(p: Vec2, loops: Vec2[][]): number {
  let d = Infinity;
  for (const l of loops) for (let i = 0; i < l.length; i++) d = Math.min(d, distToSegment(p, l[i], l[(i + 1) % l.length]));
  return d;
}

const inRoom = (p: Vec2, outer: Vec2[], islands: Vec2[][]) => pointInPolygon(p, outer) && !islands.some((i) => pointInPolygon(p, i));

/** The outline's area centroid (its vertices' mean when it has no area). */
export function centroid(poly: Vec2[]): Vec2 {
  let a = 0, cx = 0, cy = 0;
  for (let i = 0; i < poly.length; i++) {
    const p = poly[i], q = poly[(i + 1) % poly.length];
    const c = p.x * q.y - q.x * p.y;
    a += c; cx += (p.x + q.x) * c; cy += (p.y + q.y) * c;
  }
  if (Math.abs(a) < 1e-12) return { x: poly.reduce((s, p) => s + p.x, 0) / poly.length, y: poly.reduce((s, p) => s + p.y, 0) / poly.length };
  return { x: cx / (3 * a), y: cy / (3 * a) };
}

/**
 * Where to stand in a room: its centroid when that is inside it and clear of
 * its walls (half a metre, or a quarter of the room's widest clearance), else
 * the point of a fine grid over it farthest from every edge (an L-shaped
 * room's centroid can lie outside it), the nearer the centroid the better.
 */
export function roomEye(outer: Vec2[], islands: Vec2[][] = []): Vec2 {
  const loops = [outer, ...islands];
  const c = centroid(outer);
  const xs = outer.map((p) => p.x), ys = outer.map((p) => p.y);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const n = 48;
  let best: { p: Vec2; d: number } | null = null;
  for (let i = 0; i <= n; i++) for (let j = 0; j <= n; j++) {
    const p = { x: x0 + ((x1 - x0) * (i + 0.5)) / (n + 1), y: y0 + ((y1 - y0) * (j + 0.5)) / (n + 1) };
    if (!inRoom(p, outer, islands)) continue;
    const d = clearance(p, loops) - 1e-3 * Math.hypot(p.x - c.x, p.y - c.y);
    if (!best || d > best.d) best = { p, d };
  }
  if (!best) return c;
  if (inRoom(c, outer, islands) && clearance(c, loops) >= Math.min(0.5, best.d / 4)) return c;
  return best.p;
}

/** How far one can see from `p` along `dir` before an edge of the outline (Infinity: nothing). */
export function sightLine(p: Vec2, dir: Vec2, loops: Vec2[][]): number {
  let t = Infinity;
  for (const l of loops) for (let i = 0; i < l.length; i++) {
    const a = l[i], b = l[(i + 1) % l.length];
    const ex = b.x - a.x, ey = b.y - a.y;
    const den = dir.x * ey - dir.y * ex;
    if (Math.abs(den) < 1e-12) continue;
    const s = ((a.x - p.x) * ey - (a.y - p.y) * ex) / den;
    const u = ((a.x - p.x) * dir.y - (a.y - p.y) * dir.x) / den;
    if (s > 1e-9 && u >= 0 && u <= 1) t = Math.min(t, s);
  }
  return t;
}

/** Which way to look from `eye`: along the room's longest side, towards the longer view that way. Radians, 0 along +X. */
export function roomLook(outer: Vec2[], eye: Vec2, islands: Vec2[][] = []): number {
  let best = { len: -1, x: 1, y: 0 };
  for (let i = 0; i < outer.length; i++) {
    const a = outer[i], b = outer[(i + 1) % outer.length];
    const len = Math.hypot(b.x - a.x, b.y - a.y);
    if (len > best.len + 1e-9) best = { len, x: (b.x - a.x) / len, y: (b.y - a.y) / len };
  }
  const loops = [outer, ...islands];
  const ahead = sightLine(eye, best, loops), back = sightLine(eye, { x: -best.x, y: -best.y }, loops);
  return back > ahead + 1e-9 ? Math.atan2(-best.y, -best.x) : Math.atan2(best.y, best.x);
}

/** The keys a walk moves by (KeyboardEvent.code, so any keyboard layout walks the same). */
export const WALK_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'KeyE', 'KeyQ', 'PageUp', 'PageDown']);

/**
 * One step of a walk: where the eye is after `dt` seconds with `keys` held,
 * looking along `yaw`. Forward and sideways stay level whatever the pitch;
 * a diagonal is no faster than straight; Shift (`run`) runs.
 */
export function walkStep(pos: Vec3, yaw: number, keys: ReadonlySet<string>, dt: number, run = false): Vec3 {
  const has = (...k: string[]) => k.some((x) => keys.has(x));
  const fwd = (has('KeyW', 'ArrowUp') ? 1 : 0) - (has('KeyS', 'ArrowDown') ? 1 : 0);
  const side = (has('KeyD', 'ArrowRight') ? 1 : 0) - (has('KeyA', 'ArrowLeft') ? 1 : 0);
  const climb = (has('KeyE', 'PageUp') ? 1 : 0) - (has('KeyQ', 'PageDown') ? 1 : 0);
  const pace = WALK_SPEED * (run ? RUN_FACTOR : 1) * dt;
  const len = Math.hypot(fwd, side) || 1;
  const c = Math.cos(yaw), s = Math.sin(yaw);
  // Right of the look is the look turned clockwise.
  const dx = ((fwd * c + side * s) / len) * pace;
  const dy = ((fwd * s - side * c) / len) * pace;
  return { x: pos.x + dx, y: pos.y + dy, z: pos.z + climb * CLIMB_SPEED * (run ? RUN_FACTOR : 1) * dt };
}

/** The look as a unit vector, from yaw (0 along +X) and pitch (below 0 looks down). */
export const lookVector = (yaw: number, pitch: number): Vec3 => ({ x: Math.cos(yaw) * Math.cos(pitch), y: Math.sin(yaw) * Math.cos(pitch), z: Math.sin(pitch) });

/** Pitch kept short of straight up or down, so the camera never rolls over. */
export const clampPitch = (p: number) => Math.max(-1.45, Math.min(1.45, p));

/** A walk into a room: its outline (x y pairs) and islands, standing on its storey's level. */
export function walkInto(name: string, outline: number[], islands: number[][], elevation: number): WalkStart | null {
  const pts = (l: number[]) => Array.from({ length: l.length >> 1 }, (_, i) => ({ x: l[2 * i], y: l[2 * i + 1] }));
  const outer = pts(outline);
  if (outer.length < 3) return null;
  const holes = islands.map(pts);
  const at = roomEye(outer, holes);
  return { eye: { x: at.x, y: at.y, z: elevation + EYE_HEIGHT }, yaw: roomLook(outer, at, holes), pitch: 0, name };
}
