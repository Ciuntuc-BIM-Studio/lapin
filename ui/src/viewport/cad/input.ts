/**
 * Typed input on the command line (the old Lapin's forms): x,y absolute;
 * @dx,dy relative to the last point; @d<deg relative polar; d<deg absolute
 * polar; @ the last point; a bare number is a distance toward the cursor.
 * Ortho squares a point to its base; polar tracking rounds its angle.
 */

import type { Vec2 } from '../../platform/module';
import { parseImperial, unit } from '../../engine/unitsStore';

export type Parsed = { kind: 'point'; pt: Vec2 } | { kind: 'distance'; d: number };

const NUM = String.raw`[-+]?(?:\d+\.?\d*|\.\d+)`;
const CART = new RegExp(String.raw`^(${NUM})\s*,\s*(${NUM})$`);
const POLAR = new RegExp(String.raw`^(${NUM})\s*<\s*(${NUM})$`);
const BARE = new RegExp(String.raw`^${NUM}$`);

export function parseCoordinateInput(text: string, o: { last: Vec2 | null }): Parsed | null {
  let t = text.trim();
  if (!t) return null;
  const rel = t.startsWith('@');
  if (rel) {
    if (!o.last) return null;
    t = t.slice(1).trim();
    if (!t) return { kind: 'point', pt: { ...o.last } };
  }
  const base = rel && o.last ? o.last : { x: 0, y: 0 };
  // Feet and inches (3'6", 42"): a distance, or x,y of them; in an imperial project a bare number is feet.
  const imperial = /['"″′]/.test(t);
  const feet = unit() === 'ft-in' || unit() === 'in';
  const len = (s: string): number | null => (imperial ? parseImperial(s) : feet ? Number(s) * (unit() === 'in' ? 0.0254 : 0.3048) : Number(s));
  if (imperial) {
    const parts = t.split(',');
    if (parts.length === 2) { const x = parseImperial(parts[0]), y = parseImperial(parts[1]); return x != null && y != null ? { kind: 'point', pt: { x: base.x + x, y: base.y + y } } : null; }
    const d = parseImperial(t);
    return d != null && !rel ? { kind: 'distance', d } : null;
  }
  let m = CART.exec(t);
  if (m) return { kind: 'point', pt: { x: base.x + (len(m[1]) ?? 0), y: base.y + (len(m[2]) ?? 0) } };
  m = POLAR.exec(t);
  if (m) {
    const d = len(m[1]) ?? 0, a = (Number(m[2]) * Math.PI) / 180;
    return { kind: 'point', pt: { x: base.x + d * Math.cos(a), y: base.y + d * Math.sin(a) } };
  }
  if (!rel && BARE.test(t)) return { kind: 'distance', d: len(t) ?? 0 };
  return null;
}

/** The point squared to its base: along the dominant axis. */
export function applyOrtho(base: Vec2, p: Vec2): Vec2 {
  return Math.abs(p.x - base.x) >= Math.abs(p.y - base.y) ? { x: p.x, y: base.y } : { x: base.x, y: p.y };
}

/** The point at the same distance from its base, its angle rounded to the step (degrees). */
export function applyPolar(base: Vec2, p: Vec2, stepDeg: number): Vec2 {
  const d = Math.hypot(p.x - base.x, p.y - base.y);
  const step = (stepDeg * Math.PI) / 180;
  const a = Math.round(Math.atan2(p.y - base.y, p.x - base.x) / step) * step;
  return { x: base.x + d * Math.cos(a), y: base.y + d * Math.sin(a) };
}

/** A distance from a point toward a target (+x when they coincide). */
export function pointAlong(from: Vec2, toward: Vec2, d: number): Vec2 {
  const dx = toward.x - from.x, dy = toward.y - from.y, l = Math.hypot(dx, dy);
  return l < 1e-12 ? { x: from.x + d, y: from.y } : { x: from.x + (dx / l) * d, y: from.y + (dy / l) * d };
}
