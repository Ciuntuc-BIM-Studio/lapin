/**
 * Feature lines (site grading T2, docs/plan-site-grading.md): the pure parts of editing one in plan and of dragging a
 * contour. A feature line is a 3D polyline belonging to a terrain: corners (a height of their own or none), an arc's rise per
 * segment, a grade per segment, open or closed. The engine (`civil.updateFeatureLine`, `civil.setFeatureLinePoints`,
 * `civil.setFeatureLineGrade`, `civil.dragContour`) does the work; these functions build what it is sent.
 */

import type { Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { insertVertex, type Outline } from '../../viewport/contour';
import { arcLength, project } from '../../viewport/arc';
import { parseLength, parseLevel } from '../../engine/unitsStore';

/** A feature line as the interface edits it: the corners, each one's own height (null: none), the segments' rises and grades. */
export interface FLine {
  pts: Vec2[];
  /** Each corner's own height (model height), null when it takes the ground's or a grade's. */
  z: (number | null)[];
  /** The segments' rises (0: straight): n − 1 of them open, n closed. */
  arcs: number[];
  /** The segments' grades (a fraction), null where none is set. */
  grades: (number | null)[];
  closed: boolean;
  /** Every corner's height as the engine resolved it, and which of them are the corner's own. */
  resolved: (number | null)[];
  own: boolean[];
}

const finite = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v);

/** The number of segments of a line with n corners. */
export const segmentCount = (n: number, closed: boolean): number => Math.max(0, closed ? (n >= 3 ? n : n - 1) : n - 1);

/** A feature line from its node's props (what the engine sends). */
export function lineOf(props: Record<string, unknown>): FLine {
  const raw = Array.isArray(props.points) ? props.points as unknown[] : [];
  const pts: Vec2[] = [], z: (number | null)[] = [];
  for (const p of raw) {
    if (!Array.isArray(p) || p.length < 2) continue;
    pts.push({ x: Number(p[0]), y: Number(p[1]) });
    z.push(finite(p[2]) ? p[2] : null);
  }
  const closed = props.closed === true && pts.length >= 3;
  const segs = segmentCount(pts.length, closed);
  const arcs = Array.from({ length: segs }, (_, i) => (Array.isArray(props.arcs) && finite(props.arcs[i]) ? props.arcs[i] as number : 0));
  const grades = Array.from({ length: segs }, (_, i) => (Array.isArray(props.grades) && finite(props.grades[i]) ? props.grades[i] as number : null));
  const resolved = pts.map((_, i) => (Array.isArray(props.cornerZ) && finite(props.cornerZ[i]) ? props.cornerZ[i] as number : null));
  const own = pts.map((_, i) => Array.isArray(props.ownZ) && props.ownZ[i] === true);
  return { pts, z, arcs, grades, closed, resolved, own };
}

/** What `civil.updateFeatureLine` takes for an edited line: its corners (with their own heights), arcs and grades. */
export function payloadOf(l: FLine): { points: number[][]; arcs: number[]; grades: (number | null)[]; closed: boolean } {
  return {
    points: l.pts.map((p, i) => (l.z[i] != null ? [p.x, p.y, l.z[i] as number] : [p.x, p.y])),
    arcs: l.arcs,
    grades: l.grades,
    closed: l.closed,
  };
}

/** Corner i moved in plan; its own height, arcs and grades stay. */
export function moveCorner(l: FLine, i: number, to: Vec2): FLine {
  return { ...l, pts: l.pts.map((p, k) => (k === i ? to : p)) };
}

/**
 * A corner put on segment `seg` (from corner seg to the next), at `at` (its middle when not given; on an arc, on the arc). The
 * two halves keep the segment's grade; on an arc each half is an arc of the same circle. When either end has a height of its own
 * (or the segment a grade), the new corner takes the line's height there as its own, so the line keeps its shape.
 */
export function insertCorner(l: FLine, seg: number, at?: Vec2): FLine {
  const n = l.pts.length;
  const o: Outline<unknown> = { pts: l.pts, arcs: l.closed ? l.arcs : [...l.arcs, 0] };
  const next = insertVertex(o, seg, at);
  const p = next.pts[seg + 1];
  const j = (seg + 1) % n;
  const a = l.pts[seg], b = l.pts[j];
  const rise = l.arcs[seg] ?? 0;
  const arc = { a, b, rise };
  let t: number;
  if (Math.abs(rise) > 1e-6) { const len = arcLength(arc); t = len > 0 ? project(arc, p) / len : 0.5; }
  else { const dx = b.x - a.x, dy = b.y - a.y, d2 = dx * dx + dy * dy; t = d2 > 0 ? ((p.x - a.x) * dx + (p.y - a.y) * dy) / d2 : 0.5; }
  t = Math.max(0, Math.min(1, t));
  const za = l.resolved[seg], zb = l.resolved[j];
  const keeps = l.own[seg] || l.own[j] || l.grades[seg] != null;
  const zNew = keeps && za != null && zb != null ? Math.round((za + (zb - za) * t) * 1e6) / 1e6 : null;
  const segs = segmentCount(next.pts.length, l.closed);
  const rises = (next.arcs ?? []).slice(0, segs);
  while (rises.length < segs) rises.push(0);
  const g = l.grades[seg] ?? null;
  const grades = [...l.grades.slice(0, seg), g, g, ...l.grades.slice(seg + 1)];
  return {
    ...l,
    pts: next.pts,
    z: [...l.z.slice(0, seg + 1), zNew, ...l.z.slice(seg + 1)],
    arcs: rises,
    grades,
    resolved: [...l.resolved.slice(0, seg + 1), zNew, ...l.resolved.slice(seg + 1)],
    own: [...l.own.slice(0, seg + 1), zNew != null, ...l.own.slice(seg + 1)],
  };
}

/** Corner i taken out (null when the line would have fewer than two corners open, three closed). The two segments it joined become one, straight and with no grade. */
export function removeCorner(l: FLine, i: number): FLine | null {
  const n = l.pts.length;
  if (n <= (l.closed ? 3 : 2) || i < 0 || i >= n) return null;
  const drop = <T,>(a: T[]) => a.filter((_, k) => k !== i);
  let arcs: number[], grades: (number | null)[];
  if (l.closed) {
    const k = (i - 1 + n) % n;
    arcs = l.arcs.map((r, q) => (q === k ? 0 : r)).filter((_, q) => q !== i);
    grades = l.grades.map((g, q) => (q === k ? null : g)).filter((_, q) => q !== i);
  } else if (i === 0) { arcs = l.arcs.slice(1); grades = l.grades.slice(1); }
  else if (i === n - 1) { arcs = l.arcs.slice(0, -1); grades = l.grades.slice(0, -1); }
  else {
    arcs = [...l.arcs.slice(0, i - 1), 0, ...l.arcs.slice(i + 1)];
    grades = [...l.grades.slice(0, i - 1), null, ...l.grades.slice(i + 1)];
  }
  return { ...l, pts: drop(l.pts), z: drop(l.z), resolved: drop(l.resolved), own: drop(l.own), arcs, grades };
}

/** Segment `seg` bent to `rise` (0: straight). */
export function bendSegment(l: FLine, seg: number, rise: number): FLine {
  return { ...l, arcs: l.arcs.map((r, k) => (k === seg ? rise : r)) };
}

/** The engine's command for a line edited in plan. */
export const saveLine = (id: string, l: FLine): { command: string; args: Record<string, unknown> } => ({ command: 'civil.updateFeatureLine', args: { id, ...payloadOf(l) } });

/** A grade as the panel shows it: a percentage with two decimals ("2.00 %"; a fraction of 0.02). */
export function formatGrade(g: number): string {
  const pct = g * 100;
  return `${(Math.abs(pct) < 5e-3 ? 0 : pct).toFixed(2)} %`;
}

/**
 * A grade typed, read the way the engine reads it ("2 %", "1:50", "-1,5 %", a fraction "0.02"): its fraction, null for an empty
 * text (the grade cleared), undefined when it does not read. The engine reads the text itself; this tells a typo before asking.
 */
export function readGrade(text: string): number | null | undefined {
  const t = text.trim().replace(',', '.').replace(/\s+/g, '');
  if (t === '') return null;
  const ratio = /^([+-]?)1:(\d+(?:\.\d+)?)$/.exec(t);
  if (ratio) { const d = Number(ratio[2]); return d > 0 ? (ratio[1] === '-' ? -1 : 1) / d : undefined; }
  const pct = /^([+-]?\d+(?:\.\d+)?)%$/.exec(t);
  if (pct) return Number(pct[1]) / 100;
  const plain = /^[+-]?\d+(?:\.\d+)?$/.exec(t);
  return plain ? Number(t) : undefined;
}

/** How much of a drag a point carries at `r` from the grab with the reach `reach`: (R − r)² / ((R − r)² + r²), 1 at the grab, ½ halfway, 0 at the reach and beyond. */
export function dragFade(r: number, reach: number): number {
  if (!(reach > 0) || r >= reach) return 0;
  if (r <= 0) return 1;
  const a = (reach - r) ** 2;
  return a / (a + r * r);
}

/** The reach a drag has when none is typed: the larger of 2 m and three times the drag's length. */
export const defaultReach = (drag: number): number => Math.max(2, 3 * drag);

/**
 * A contour as the drag would leave it: each point moved by the drag faded by its distance from the grab; the points beyond the
 * reach stay. The part moved, and the whole line (for the preview, the two drawn in turn).
 */
export function draggedContour(points: Vec2[], from: Vec2, to: Vec2, reach?: number): { line: Vec2[]; reach: number } {
  const dx = to.x - from.x, dy = to.y - from.y;
  const R = reach && reach > 0 ? reach : defaultReach(Math.hypot(dx, dy));
  const line = points.map((p) => {
    const w = dragFade(Math.hypot(p.x - from.x, p.y - from.y), R);
    return w > 0 ? { x: p.x + dx * w, y: p.y + dy * w } : p;
  });
  return { line, reach: R };
}

/** The terrain a feature line belongs to, when it is there. */
export const terrainOf = (n: NodeDto, nodes: Map<string, NodeDto>): NodeDto | undefined => (typeof n.props.terrain === 'string' ? nodes.get(n.props.terrain) : undefined);

/**
 * The heights of a line's corners typed in one go: separated by semicolons or spaces, in the order drawn; an empty one (or "-")
 * takes the ground's there. Null when one does not read.
 */
export function readHeights(text: string, n: number): (number | null)[] | null {
  const parts = text.includes(';') ? text.split(';') : text.trim().split(/\s+/);
  const out: (number | null)[] = [];
  for (let i = 0; i < n; i++) {
    const t = (parts[i] ?? '').trim();
    if (t === '' || t === '-') { out.push(null); continue; }
    const v = parseLevel(t);
    if (v == null || !Number.isFinite(v)) return null;
    out.push(v);
  }
  return out;
}

/** The reach typed in the tool's bar, in metres; undefined when empty (the engine's default), null when it does not read. */
export function readReach(text: string): number | undefined | null {
  if (text.trim() === '') return undefined;
  const v = parseLength(text);
  return v != null && v > 0 ? v : null;
}

