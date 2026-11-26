/**
 * The location-based schedule's drawing maths (planning.lbs, engine/src/Modules/Construction/Lapin.Planning/Lbs.cs): where a
 * segment lies on the chart (time across in pixels a day, the locations up in pixels a metre), what a click hits, the
 * handles of the segment selected and what dragging them writes, the first and second click (a segment, then its task across
 * all its locations), the labels along their lines, the zooms. Pure, so it is tested apart from the pane.
 */

import { dateOf, dayOf, dragDays } from './gantt';

export type LbsLevel = 'building' | 'storey' | 'zone';
export type LbsRep = 'line' | 'block' | 'milestone';

export interface LbsLocation {
  id: string; name: string; level: LbsLevel; parent: string | null; at: number; extent: number; row: number;
  elevation?: number; folded?: boolean; children?: string[];
}
export interface LbsSegment {
  id: string; task: string; name: string; group: string; colour: string; from: string; to: string; locations: string[];
  row0: number; row1: number; y0: number; y1: number; start: string; finish: string; rep: LbsRep;
  critical: boolean; status: 'planned' | 'started' | 'done'; complete: number; count: number; totalFloat: number; duration: number;
}
export interface LbsLabel { dx: number; dy: number; angle: number }
export interface LbsNote { id: string; date: string; location: string; text: string; dx?: number; dy?: number }
export interface LbsSettings { level: LbsLevel; zoneField?: string; collapsed?: string[]; labels?: Record<string, LbsLabel>; notes?: LbsNote[] }
export interface LbsDto {
  view: string | null; level: LbsLevel; zoneField: string | null; settings: LbsSettings;
  start: string; finish: string; dataDate: string | null; height: number;
  locations: LbsLocation[]; segments: LbsSegment[];
  groups: { key: string; colour: string; tasks: string[] }[];
  unlocated: { task: string; name: string; start: string; finish: string; count: number; why: 'none' | 'unplaced' }[];
}

/** The chart's scales: its first day, pixels a day across, pixels a metre up, and the axis' height in metres (the top at y 0). */
export interface Scale { origin: number; ppd: number; ppm: number; height: number }

export const MIN_PPD = 0.4, MAX_PPD = 60, MIN_PPM = 4, MAX_PPM = 200;

export const xOf = (s: Scale, day: number) => (day - s.origin) * s.ppd;
/** A height on the axis (metres from its foot) as a y on the chart: the foot at the bottom. */
export const yOf = (s: Scale, at: number) => (s.height - at) * s.ppm;
export const dayAt = (s: Scale, x: number) => Math.floor(x / s.ppd) + s.origin;
export const atOf = (s: Scale, y: number) => s.height - y / s.ppm;

/** A segment on the chart: a line from its start at its locations' foot to the end of its finish at their top; a block over them; a milestone a tick. */
export interface Geom { kind: LbsRep; x0: number; y0: number; x1: number; y1: number }

export function geomOf(seg: Pick<LbsSegment, 'start' | 'finish' | 'y0' | 'y1' | 'rep'>, s: Scale): Geom {
  const x0 = xOf(s, dayOf(seg.start));
  const x1 = seg.rep === 'milestone' ? x0 : xOf(s, dayOf(seg.finish) + 1);
  return { kind: seg.rep, x0, y0: yOf(s, seg.y0), x1, y1: yOf(s, seg.y1) };
}

/** The distance from a point to a segment's drawing (0 inside a block). */
export function distanceTo(g: Geom, x: number, y: number): number {
  if (g.kind === 'block') {
    const dx = Math.max(g.x0 - x, 0, x - g.x1);
    const dy = Math.max(g.y1 - y, 0, y - g.y0);
    return Math.hypot(dx, dy);
  }
  const vx = g.x1 - g.x0, vy = g.y1 - g.y0;
  const len2 = vx * vx + vy * vy;
  const t = len2 === 0 ? 0 : Math.max(0, Math.min(1, ((x - g.x0) * vx + (y - g.y0) * vy) / len2));
  return Math.hypot(x - (g.x0 + t * vx), y - (g.y0 + t * vy));
}

/** The segment under a point (the nearest within a tolerance; a line over a block it crosses); null: none. */
export function hit<T extends Pick<LbsSegment, 'id' | 'start' | 'finish' | 'y0' | 'y1' | 'rep'>>(segments: readonly T[], s: Scale, x: number, y: number, tolerance = 6): T | null {
  let best: T | null = null, bestD = Infinity;
  for (const seg of segments) {
    const d = distanceTo(geomOf(seg, s), x, y) + (seg.rep === 'block' ? 3 : 0);
    if (d <= tolerance + (seg.rep === 'block' ? 3 : 0) && d < bestD) { best = seg; bestD = d; }
  }
  return best;
}

/** The handles of a segment selected: the bottom one at its start (moves it), the top one at its finish (its duration). */
export function handlesOf(g: Geom): { start: [number, number]; finish: [number, number] } {
  return { start: [g.x0, g.y0], finish: [g.x1, g.y1] };
}

export type HandleKind = 'start' | 'finish';
export function handleAt(g: Geom, x: number, y: number, r = 7): HandleKind | null {
  const h = handlesOf(g);
  if (Math.hypot(x - h.start[0], y - h.start[1]) <= r) return 'start';
  if (g.kind !== 'milestone' && Math.hypot(x - h.finish[0], y - h.finish[1]) <= r) return 'finish';
  return null;
}

/** What a handle dragged by some pixels writes through planning.updateTask: a start (no earlier than) or a finish (its duration). */
export function dragArgs(seg: Pick<LbsSegment, 'task' | 'start' | 'finish'>, kind: HandleKind, dx: number, ppd: number): Record<string, unknown> | null {
  const days = dragDays(dx, ppd);
  if (days === 0) return null;
  if (kind === 'start') return { id: seg.task, start: dateOf(dayOf(seg.start) + days) };
  return { id: seg.task, finish: dateOf(Math.max(dayOf(seg.start), dayOf(seg.finish) + days)) };
}

/** The selection in the chart: one segment (a task in a location), or a task across all its locations (its group). */
export interface LbsPick { segment: string; whole: boolean; tasks: string[] }

/**
 * A click on a segment: the first selects it (its task); a second click on the same segment selects the task across all its
 * locations (every task of its group, as the trade climbing the building); a click elsewhere starts again.
 */
export function clickPick(prev: LbsPick | null, seg: Pick<LbsSegment, 'id' | 'task' | 'group'> | null, all: readonly Pick<LbsSegment, 'task' | 'group'>[]): LbsPick | null {
  if (!seg) return null;
  if (prev && prev.segment === seg.id && !prev.whole) {
    const tasks = [...new Set(all.filter((x) => x.group === seg.group).map((x) => x.task))];
    return { segment: seg.id, whole: true, tasks };
  }
  return { segment: seg.id, whole: false, tasks: [seg.task] };
}

/** The segments a pick shows emphasised. */
export function picked(pick: LbsPick | null, segments: readonly Pick<LbsSegment, 'id' | 'task'>[]): Set<string> {
  if (!pick) return new Set();
  if (!pick.whole) return new Set([pick.segment]);
  const tasks = new Set(pick.tasks);
  return new Set(segments.filter((s) => tasks.has(s.task)).map((s) => s.id));
}

/** A pick that still holds once the selection elsewhere (the Gantt, the 3D) changed: kept when its tasks are the ones selected, else the selection's own. */
export function pickFromSelection(prev: LbsPick | null, selected: readonly string[], segments: readonly Pick<LbsSegment, 'id' | 'task'>[]): LbsPick | null {
  const tasks = selected.filter((id) => segments.some((s) => s.task === id));
  if (!tasks.length) return null;
  if (prev && prev.tasks.length === tasks.length && prev.tasks.every((t) => tasks.includes(t))) return prev;
  const first = segments.find((s) => s.task === tasks[0])!;
  return tasks.length === 1 ? { segment: first.id, whole: false, tasks } : { segment: first.id, whole: true, tasks };
}

/**
 * A segment's label: in the middle of its line (or block), turned along a line, moved and turned as the view keeps it
 * (offsets in days and metres, so it stays put at any zoom and on the sheet; the angle counter-clockwise, as on paper).
 */
export function labelOf(g: Geom, s: Scale, kept?: LbsLabel): { x: number; y: number; angle: number } {
  const x = (g.x0 + g.x1) / 2 + (kept?.dx ?? 0) * s.ppd;
  const y = (g.y0 + g.y1) / 2 - 3 - (kept?.dy ?? 0) * s.ppm;
  const along = g.kind === 'line' && g.x1 > g.x0 ? (Math.atan2(g.y0 - g.y1, g.x1 - g.x0) * 180) / Math.PI : 0;
  return { x, y, angle: kept ? kept.angle : along };
}

/** A label dragged by some pixels: its offset in days and metres (from where it was kept). */
export function movedLabel(kept: LbsLabel | undefined, g: Geom, s: Scale, dx: number, dy: number): LbsLabel {
  const along = g.kind === 'line' && g.x1 > g.x0 ? (Math.atan2(g.y0 - g.y1, g.x1 - g.x0) * 180) / Math.PI : 0;
  return { dx: round2((kept?.dx ?? 0) + dx / s.ppd), dy: round2((kept?.dy ?? 0) - dy / s.ppm), angle: kept?.angle ?? round2(along) };
}

/** A label turned by a pointer moving across it (Alt+drag): a degree for two pixels, within ±90°. */
export const turnedLabel = (kept: LbsLabel | undefined, along: number, dx: number): LbsLabel =>
  ({ dx: kept?.dx ?? 0, dy: kept?.dy ?? 0, angle: Math.max(-90, Math.min(90, round2((kept?.angle ?? along) - dx / 2))) });

const round2 = (v: number) => Math.round(v * 100) / 100;

/** Ctrl + wheel zooms the time, Shift + wheel the locations, about the pointer: the new scale and the scroll that keeps the point under it. */
export function wheelZoom(s: Scale, e: { deltaY: number; ctrl: boolean; shift: boolean }, px: number, py: number, scroll: { left: number; top: number }): { scale: Scale; scroll: { left: number; top: number } } | null {
  const k = Math.exp(-e.deltaY * 0.002);
  if (e.ctrl) {
    const ppd = Math.min(MAX_PPD, Math.max(MIN_PPD, s.ppd * k));
    const day = (scroll.left + px) / s.ppd;
    return { scale: { ...s, ppd }, scroll: { left: Math.max(0, day * ppd - px), top: scroll.top } };
  }
  if (e.shift) {
    const ppm = Math.min(MAX_PPM, Math.max(MIN_PPM, s.ppm * k));
    const m = (scroll.top + py) / s.ppm;
    return { scale: { ...s, ppm }, scroll: { left: scroll.left, top: Math.max(0, m * ppm - py) } };
  }
  return null;
}

/** The scale that shows the whole chart in a pane (time across, the locations' height up). */
export function fitLbs(d: Pick<LbsDto, 'start' | 'finish' | 'height'>, width: number, height: number): Scale {
  const origin = dayOf(d.start) - 7;
  const days = Math.max(7, dayOf(d.finish) - origin + 7);
  return {
    origin,
    ppd: Math.min(MAX_PPD, Math.max(MIN_PPD, width / days)),
    ppm: Math.min(MAX_PPM, Math.max(MIN_PPM, (height - 12) / Math.max(1, d.height))),
    height: Math.max(1, d.height),
  };
}

/** The levels folded after a location's fold is toggled. */
export function toggleFold(collapsed: readonly string[] | undefined, id: string): string[] {
  const c = collapsed ?? [];
  return c.includes(id) ? c.filter((x) => x !== id) : [...c, id];
}

/** The locations as the axis lists them, top first, with their depth: those under a folded one left out. */
export function axisRows(locations: readonly LbsLocation[]): { loc: LbsLocation; depth: number }[] {
  const byId = new Map(locations.map((l) => [l.id, l]));
  const o: { loc: LbsLocation; depth: number }[] = [];
  const walk = (l: LbsLocation, depth: number) => {
    o.push({ loc: l, depth });
    if (l.folded) return;
    for (const c of l.children ?? []) { const x = byId.get(c); if (x) walk(x, depth + 1); }
  };
  for (const l of locations) if (!l.parent || !byId.has(l.parent)) walk(l, 0);
  // Top first, as the chart reads: the higher a location, the nearer the top (a parent before its children at the same height).
  return o.sort((a, b) => (b.loc.at + b.loc.extent) - (a.loc.at + a.loc.extent) || a.depth - b.depth);
}

/** A note's place on the chart: at its date, in the middle of its location's band, moved as the view keeps it. */
export function noteAt(n: LbsNote, locations: readonly LbsLocation[], s: Scale): { x: number; y: number } | null {
  const l = locations.find((x) => x.id === n.location);
  if (!l) return null;
  return { x: xOf(s, dayOf(n.date)) + (n.dx ?? 0) * s.ppd, y: yOf(s, l.at + l.extent / 2) - (n.dy ?? 0) * s.ppm };
}

/** The row (a location shown on the chart) at a height on the axis; null outside them. */
export function rowAt(locations: readonly LbsLocation[], at: number): LbsLocation | null {
  return locations.find((l) => l.row >= 0 && at >= l.at && at < l.at + l.extent) ?? null;
}
