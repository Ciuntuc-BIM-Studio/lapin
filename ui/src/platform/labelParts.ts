/**
 * A label's parts edited on screen (decision 292): a door's or window's marker
 * (each text line, each end of its line) and an opening void's label. The
 * engine draws each part and says where it is, where it would be unmoved
 * (`base`), the label's own directions (`u` along its wall, `v` across) and
 * how long a paper millimetre is in the view; the grips turn a drag into the
 * part's offset (paper mm along u and v), its turn (degrees from the way it
 * reads by default) or a line end's reach (paper mm further), one command
 * each, so each drag is one undo step.
 */

import type { NodeDto } from '../engine/protocol';
import type { Grip, GripStep, GripView, Vec2 } from './module';

/** One part as the engine tells it (LabelParts.TextData / EndData). */
export interface PartDto {
  key: string;
  kind: 'text' | 'end';
  at: number[];
  base: number[];
  mm: number;
  u?: number[];
  v?: number[];
  dir?: number[];
  angle?: number;
  from?: number;
  rotAt?: number[];
  moved?: boolean;
  turned?: boolean;
}

const P = (a: number[] | undefined): Vec2 | null => (Array.isArray(a) && a.length >= 2 && Number.isFinite(a[0]) && Number.isFinite(a[1]) ? { x: a[0], y: a[1] } : null);
const dot = (a: Vec2, b: Vec2) => a.x * b.x + a.y * b.y;
const round = (n: number, step = 0.1) => Math.round(n / step) * step;

/** Whether something the engine sent is a part. */
export function isPart(x: unknown): x is PartDto {
  const p = x as PartDto | null;
  return !!p && typeof p.key === 'string' && (p.kind === 'text' || p.kind === 'end') && !!P(p.at) && !!P(p.base) && typeof p.mm === 'number' && p.mm > 0;
}

/** A text dragged to `to`: its offset from where it would be unmoved, paper mm along and across its label's directions. */
export function offsetOf(p: PartDto, to: Vec2): [number, number] {
  const base = P(p.base)!, u = P(p.u) ?? { x: 1, y: 0 }, v = P(p.v) ?? { x: -u.y, y: u.x };
  const d = { x: to.x - base.x, y: to.y - base.y };
  return [round(dot(d, u) / p.mm), round(dot(d, v) / p.mm)];
}

/** A text's turning grip dragged to `to`: its turn from the angle it counts from, in whole degrees within (−180°, 180°]. */
export function turnOf(p: PartDto, to: Vec2): number {
  const at = P(p.at)!;
  const deg = (Math.atan2(to.y - at.y, to.x - at.x) * 180) / Math.PI - (p.from ?? 0);
  let r = Math.round(deg) % 360;
  if (r > 180) r -= 360;
  if (r <= -180) r += 360;
  return r;
}

/** A line's end dragged to `to`: how many paper mm further than by default it reaches (negative: shorter). */
export function reachOf(p: PartDto, to: Vec2): number {
  const base = P(p.base)!, dir = P(p.dir) ?? { x: 1, y: 0 };
  return round(dot({ x: to.x - base.x, y: to.y - base.y }, dir) / p.mm);
}

/**
 * The grips of a label's parts: each text's (move it; Ctrl/⌘-click: back in its place) and its turning grip
 * (Ctrl/⌘-click: as by default), each line end's (lengthen or shorten; Ctrl/⌘-click: back to its length).
 * `edit` makes the command from the part's change ({part, offset | rot | ext}, null: as by default).
 */
export function partGrips(parts: PartDto[], edit: (args: Record<string, unknown>) => GripStep | null, titles: Partial<Record<'move' | 'turn' | 'end', string>> = {}, prefix = ''): Grip[] {
  const grips: Grip[] = [];
  for (const p of parts) {
    if (!isPart(p)) continue;
    const at = P(p.at)!;
    const part = p.key;
    if (p.kind === 'end') {
      grips.push({
        key: `${prefix}${part}`, kind: 'end', at, title: titles.end ?? 'Drag: lengthen or shorten the line · Ctrl/⌘-click: back to its length',
        step: (to) => edit({ part, ext: reachOf(p, to) }),
        remove: () => (p.moved ? edit({ part, ext: null }) : null),
      });
      continue;
    }
    grips.push({
      key: `${prefix}${part}`, kind: 'centre', at, title: titles.move ?? 'Drag: move this text · Ctrl/⌘-click: back in its place',
      step: (to) => edit({ part, offset: offsetOf(p, to) }),
      remove: () => (p.moved ? edit({ part, offset: null }) : null),
    });
    const rotAt = P(p.rotAt);
    if (rotAt) grips.push({
      key: `${prefix}${part}:turn`, kind: 'rotate', at: rotAt, title: titles.turn ?? 'Drag: turn this text · Ctrl/⌘-click: as the tag type lays it',
      step: (to) => edit({ part, rot: turnOf(p, to) }),
      remove: () => (p.turned ? edit({ part, rot: null }) : null),
    });
  }
  return grips;
}

/**
 * An opening void's label (decisions 290, 292), where a view draws the voids: a click on a void selects its opening (a
 * roof window's, its roof), whose grips then hold each of its void labels in that view: move the label (Ctrl/⌘-click:
 * back where it stands by default, clear of the door's swing) and turn it (Ctrl/⌘-click: level again). Kept on the
 * view, by the void's key.
 */
export function voidLabelGrips(node: NodeDto, view?: GripView): Grip[] {
  const grips: Grip[] = [];
  for (const m of view?.marks ?? []) {
    const d = m.data as { key?: unknown; view?: unknown; label?: unknown } | undefined;
    if (m.id !== node.id || typeof d?.key !== 'string' || typeof d.view !== 'string' || !isPart(d.label)) continue;
    const { key, view: viewId } = d as { key: string; view: string };
    const edit = (args: Record<string, unknown>): GripStep => {
      const { part: _part, ...rest } = args;
      return { command: 'view.voidLabel', args: { viewId, key, ...rest } };
    };
    for (const g of partGrips([d.label], edit, { move: 'Drag: move the label · Ctrl/⌘-click: back where it stood', turn: 'Drag: turn the label · Ctrl/⌘-click: level again' }, `void:${key}:`))
      grips.push({ ...g, view: viewId });
  }
  return grips;
}

/** Whether `p` falls on one of a mark's label texts (their boxes: a marker's parts, a void's label), within `tol`. */
export function hitsLabel(p: Vec2, data: Record<string, unknown> | undefined, tol = 0): boolean {
  if (!data) return false;
  const parts = [...(Array.isArray(data.parts) ? data.parts : []), data.label];
  return parts.some((x) => {
    const b = (x as { box?: unknown } | undefined)?.box;
    if (!Array.isArray(b) || b.length < 5 || !b.every((n) => typeof n === 'number')) return false;
    const [cx, cy, hw, hh, deg] = b as number[];
    const a = (deg * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
    const dx = p.x - cx, dy = p.y - cy;
    return Math.abs(dx * c + dy * s) <= hw + tol && Math.abs(-dx * s + dy * c) <= hh + tol;
  });
}
