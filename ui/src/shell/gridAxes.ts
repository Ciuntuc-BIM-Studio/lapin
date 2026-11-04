/**
 * A grid's axes as the properties panel reads them (report #68): each axis's
 * line, length and angle, and its spacing from the parallel axis before it,
 * with the moves that set that spacing (this axis alone, or with the axes
 * beyond it).
 */

import type { GripStep, Vec2 } from '../platform/module';

export interface GridAxis { tag: string; a: Vec2; b: Vec2 }

export function axesOf(raw: unknown): GridAxis[] {
  const list = Array.isArray(raw) ? raw as { tag?: unknown; a?: unknown; b?: unknown }[] : [];
  const p = (v: unknown): Vec2 | null => (Array.isArray(v) && v.length >= 2 && typeof v[0] === 'number' && typeof v[1] === 'number' ? { x: v[0], y: v[1] } : null);
  return list.flatMap((x) => { const a = p(x.a), b = p(x.b); return typeof x.tag === 'string' && a && b ? [{ tag: x.tag, a, b }] : []; });
}

const dirOf = (x: GridAxis): Vec2 => { const l = Math.hypot(x.b.x - x.a.x, x.b.y - x.a.y) || 1; return { x: (x.b.x - x.a.x) / l, y: (x.b.y - x.a.y) / l }; };
export const lengthOf = (x: GridAxis) => Math.hypot(x.b.x - x.a.x, x.b.y - x.a.y);
/** Its direction in degrees, 0 along +x, counter-clockwise. */
export const angleOf = (x: GridAxis) => { const d = dirOf(x); return Math.round(Math.atan2(d.y, d.x) * 180 / Math.PI * 100) / 100; };

/** Across an axis, the way spacings count: towards +x, or +y for an axis along x (whichever way it was drawn). */
function across(x: GridAxis): Vec2 {
  const d = dirOf(x), n = { x: -d.y, y: d.x };
  return n.x < -1e-9 || (Math.abs(n.x) <= 1e-9 && n.y < 0) ? { x: -n.x, y: -n.y } : n;
}

/** The axes parallel to `axis` (itself among them), each with its offset across them, sorted by it. */
export function parallels(axes: GridAxis[], axis: GridAxis): { axis: GridAxis; offset: number }[] {
  const d = dirOf(axis), n = across(axis);
  return axes.filter((x) => { const e = dirOf(x); return Math.abs(e.x * d.y - e.y * d.x) < 1e-6; })
    .map((x) => ({ axis: x, offset: x.a.x * n.x + x.a.y * n.y }))
    .sort((p, q) => p.offset - q.offset);
}

/** The parallel axis before `axis` (the nearest on its lower side) and the spacing between them; null for the first. */
export function spacingOf(axes: GridAxis[], tag: string): { prev: string; spacing: number } | null {
  const axis = axes.find((x) => x.tag === tag);
  if (!axis) return null;
  const list = parallels(axes, axis);
  const i = list.findIndex((p) => p.axis.tag === tag);
  return i > 0 ? { prev: list[i - 1].axis.tag, spacing: list[i].offset - list[i - 1].offset } : null;
}

/** The moves that make the spacing before `tag` equal to `spacing`: this axis, and with `beyond` the parallel axes past it, by the same. */
export function setSpacing(gridId: string, axes: GridAxis[], tag: string, spacing: number, beyond: boolean): GripStep[] {
  const now = spacingOf(axes, tag);
  const axis = axes.find((x) => x.tag === tag);
  if (!now || !axis || !(spacing > 0)) return [];
  const by = spacing - now.spacing;
  if (Math.abs(by) < 1e-9) return [];
  const n = across(axis);
  const list = parallels(axes, axis);
  const from = list.findIndex((p) => p.axis.tag === tag);
  return list.slice(from, beyond ? undefined : from + 1).map(({ axis: x }) => ({
    command: 'drafting.updateAxis',
    args: { gridId, tag: x.tag, x1: x.a.x + n.x * by, y1: x.a.y + n.y * by, x2: x.b.x + n.x * by, y2: x.b.y + n.y * by },
  }));
}
