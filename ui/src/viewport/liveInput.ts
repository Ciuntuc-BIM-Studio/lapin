/**
 * Live input while drawing or dragging: the segment's length and angle shown
 * at the cursor and in the bar over the view, and typed there. Lengths are
 * shown and typed in millimetres like the rest of the interface (a typed
 * value may carry its unit: 2400, 240cm, 2.4m, 3000+250); angles in degrees
 * from +X, counter-clockwise, or (decision 738) to the reference at the start:
 * the previous segment, the wall or beam it starts on. A typed length holds
 * while the cursor sets the direction; a typed angle holds the direction;
 * Enter takes the point.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { formatLength as formatByProject, parseLength as parseByProject, unit as projectUnit, type Unit } from '../engine/unitsStore';
import { angleFrom, directionAt, type AngleRef } from './angleDims';

/** A length in metres as the interface shows it (the project's unit: mm unless set otherwise). */
export const formatLength = (m: number) => formatByProject(m);
export const formatAngle = (deg: number) => `${(Math.round(deg * 10) / 10).toFixed(1)}`;

/** A typed length (the project's unit unless it says its own: 240cm, 2.4m, 3'6", 42"), sums and differences allowed; metres, or null. */
export const parseLength = (text: string, unit?: Unit): number | null => parseByProject(text, unit ?? projectUnit());

/** A typed angle in degrees (a ° allowed, sums too), or null. */
export function parseAngle(text: string): number | null {
  const t = text.replace(/\s+/g, '').replace(/°/g, '').replace(/,/g, '.');
  if (!t) return null;
  const terms = t.match(/[+-]?[^+-]+/g);
  if (!terms) return null;
  let sum = 0;
  for (const term of terms) {
    if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(term)) return null;
    sum += Number(term);
  }
  return sum;
}

/** `rel`: the angle to the reference at the segment's start (decision 738: the previous segment, the wall or beam it starts on). */
export type LiveField = 'len' | 'ang' | 'rel';
/** What the two fields mean: a segment's length and angle, or (a rectangle) its width and height; `len` holds the first, `ang` the second. */
export type LiveMode = 'polar' | 'rect';

interface LiveState {
  /** Who asked for it ("tool", "grip", "cad"); null: nothing is being drawn. */
  owner: string | null;
  base: Vec2 | null;
  cursor: Vec2 | null;
  len: string;
  ang: string;
  /** The angle typed to the reference (`ref`), degrees; the cursor gives the side. */
  rel: string;
  /** What the segment's angle is measured from at its start (decision 738); null: the X axis. */
  ref: AngleRef | null;
  /** The segment is a wall's, a beam's, a line's or a polyline's (decision 738): its angle at its start is drawn in the plan. */
  angles: boolean;
  focus: LiveField;
  /** What a typed point does; `typed` is the length typed (m) when one was, for a tool that measures along its own path. */
  commit: ((p: Vec2, typed?: number, typedAngle?: number) => void) | null;
  /** A move from the base (MOVE, COPY, a dragged grip): the cells at the cursor also show Δx and Δy. */
  delta: boolean;
  /** The two fields as length and angle, or as width and height. */
  mode: LiveMode;
  /** Set when the tool draws a rectangle (its key, to remember the mode by): Space switches the mode. */
  rect: string | null;
  /**
   * A measuring gesture (Scale image, forum 143): the cursor's point is taken as it is, and the length typed is what the two
   * points really measure (handed to the tool as `typed`), not where the second one goes.
   */
  measure: boolean;
  /**
   * An end grip dragged with Ctrl/⌘ held (decision 865): the element's own line (its fixed other end, and the direction from it
   * to the grip). The point stays on the line, a typed value is the change in length (`=` then the total), the angle is the line's.
   */
  lock: AlongLine | null;
  /** An end grip is dragged that could be kept on its line (shows the hint "Ctrl/⌘: keep its angle" until it is). */
  lockable: boolean;
}

/** The command line's prompt while an end grip that could keep its angle is dragged: the hint, then the lock's state (decision 865); null otherwise. */
export function dragPrompt(s: Pick<LiveState, 'lock' | 'lockable'>): string | null {
  return s.lock ? 'Angle kept' : s.lockable ? 'Ctrl/⌘: keep its angle' : null;
}

/** An element's own line: the fixed point and the unit direction from it to the end being dragged. */
export interface AlongLine { from: Vec2; dir: Vec2 }

/** The mode each rectangle tool was last left in (the box by the cursor opens as it was). */
const modes = new Map<string, LiveMode>();

export const useLive = create<LiveState>(() => ({ owner: null, base: null, cursor: null, len: '', ang: '', rel: '', ref: null, angles: false, focus: 'len', commit: null, delta: false, mode: 'polar', rect: null, measure: false, lock: null, lockable: false }));

/** Whether anything is typed in the box (a length, an angle, an angle to the reference, a width or a height). */
export const liveTyped = (s: Pick<LiveState, 'len' | 'ang'> & { rel?: string }): boolean => !!(s.len || s.ang || s.rel);

/** The fields Tab goes through: the angle to the reference only when there is one other than the X axis. */
export function liveFields(s: Pick<LiveState, 'measure' | 'mode' | 'ref'>): LiveField[] {
  if (s.measure) return ['len'];
  return s.mode === 'polar' && s.ref && s.ref.what !== 'x' ? ['len', 'ang', 'rel'] : ['len', 'ang'];
}

/** A rectangle opens on width and height (report #173: type the sizes after the base point); a reference point's offset on distance and angle. */
const defaultMode = (rect: string): LiveMode => (rect.startsWith('ref:') ? 'polar' : 'rect');

/** A segment starts at `base`: its length and angle can now be typed. */
export function liveBegin(owner: string, base: Vec2, commit: (p: Vec2, typed?: number, typedAngle?: number) => void, o: { delta?: boolean; rect?: string; measure?: boolean; ref?: AngleRef | null; angles?: boolean } = {}): void {
  const s = useLive.getState();
  const delta = !!o.delta;
  const rect = o.rect ?? null;
  const measure = !!o.measure;
  const angles = !!o.angles || !!o.ref;
  if (s.owner === owner && s.base && Math.hypot(s.base.x - base.x, s.base.y - base.y) < 1e-12) { useLive.setState({ commit, delta, measure, angles, ...(o.ref ? { ref: o.ref } : {}) }); return; }
  useLive.setState({ owner, base, commit, delta, measure, angles, len: '', ang: '', rel: '', ref: o.ref ?? null, focus: 'len', rect, mode: rect ? modes.get(rect) ?? defaultMode(rect) : 'polar', lock: null, lockable: false });
}

/** The reference the segment's angle is measured from (an overlay finds the wall or beam the start lies on); left alone when the same. */
export function liveSetRef(ref: AngleRef | null): void {
  const was = useLive.getState().ref;
  if (was === ref || (was && ref && was.what === ref.what && was.id === ref.id && was.dir.x === ref.dir.x && was.dir.y === ref.dir.y)) return;
  useLive.setState((s) => ({ ref, ...(ref ? {} : { rel: '', focus: s.focus === 'rel' ? 'len' : s.focus }) }));
}
export const liveCursor = (p: Vec2 | null) => useLive.setState({ cursor: p });
export function liveEnd(owner?: string): void {
  if (owner && useLive.getState().owner !== owner) return;
  useLive.setState({ owner: null, base: null, commit: null, len: '', ang: '', rel: '', ref: null, angles: false, delta: false, rect: null, mode: 'polar', measure: false, lock: null, lockable: false });
}

/** Ctrl/⌘ pressed or let go while an end grip is dragged: the lock on the element's own line (what is typed stays). */
export function liveLock(lock: AlongLine | null, lockable = !!lock): void {
  const was = useLive.getState().lock;
  if (useLive.getState().lockable !== lockable) useLive.setState({ lockable });
  if (was === lock || (was && lock && was.from === lock.from && was.dir === lock.dir)) return;
  useLive.setState({ lock });
}

/** A point on the line nearest to `p`, and the signed distance along it from `base` (positive: the way `dir` points, lengthening). */
export function alongPoint(lock: AlongLine, base: Vec2, p: Vec2): { point: Vec2; t: number } {
  const t = (p.x - base.x) * lock.dir.x + (p.y - base.y) * lock.dir.y;
  return { point: { x: base.x + lock.dir.x * t, y: base.y + lock.dir.y * t }, t };
}

const HINT_KEY = 'lapin.liveHintSeen';
let hintSeen: boolean | null = null;
/** Whether the user has switched a rectangle's box once already: the written hint ("Space: width × height") shows only until then. */
export function liveHintSeen(): boolean {
  if (hintSeen === null) { try { hintSeen = localStorage.getItem(HINT_KEY) === '1'; } catch { hintSeen = false; } }
  return hintSeen;
}
const markHintSeen = (): void => { hintSeen = true; try { localStorage.setItem(HINT_KEY, '1'); } catch { /* private window */ } };
/** For tests. */
export const resetLiveHint = (): void => { hintSeen = null; };

/** Space (or the button): length and angle become width and height, or back; what was typed is cleared, the first field is focused, the mode is remembered for the tool. */
export function liveToggleMode(): void {
  const s = useLive.getState();
  if (!s.rect) return;
  const mode: LiveMode = s.mode === 'rect' ? 'polar' : 'rect';
  modes.set(s.rect, mode);
  markHintSeen();
  useLive.setState({ mode, len: '', ang: '', rel: '', focus: 'len' });
}

/** Where the segment ends: the typed length and angle, else the cursor's. */
export function livePoint(s: Pick<LiveState, 'base' | 'cursor' | 'len' | 'ang'> & { mode?: LiveMode; measure?: boolean; rel?: string; ref?: AngleRef | null; lock?: AlongLine | null } = useLive.getState()): Vec2 | null {
  if (!s.base) return null;
  const c = s.cursor ?? { x: s.base.x + 1, y: s.base.y };
  if (s.lock) {
    // Kept on the element's own line: the cursor's side says lengthen or shorten, a typed length is the change, `=` the total.
    const { point, t } = alongPoint(s.lock, s.base, c);
    const raw = s.len.trim();
    const total = raw.startsWith('=');
    const typed = raw ? parseLength(total ? raw.slice(1) : raw) : null;
    if (typed == null) return point;
    if (total) return { x: s.lock.from.x + s.lock.dir.x * typed, y: s.lock.from.y + s.lock.dir.y * typed };
    const d = (t < 0 ? -1 : 1) * typed;
    return { x: s.base.x + s.lock.dir.x * d, y: s.base.y + s.lock.dir.y * d };
  }
  // Measuring: the point is the cursor's; what is typed is the true distance, not where the point goes.
  if (s.measure) return c;
  const dx = c.x - s.base.x, dy = c.y - s.base.y;
  if (s.mode === 'rect') {
    // Width and height, signed as on the command line (@2000,-1500): a field left empty follows the cursor.
    const w = parseLength(s.len), h = parseLength(s.ang);
    if (w == null && h == null) return c;
    return { x: s.base.x + (w ?? dx), y: s.base.y + (h ?? dy) };
  }
  const typedLen = parseLength(s.len);
  // An angle typed to the reference (decision 738) is the direction, on the side of it the cursor is, unless an angle from X is typed too.
  let typedAng = parseAngle(s.ang);
  const rel = s.rel && s.ref && s.ref.what !== 'x' ? parseAngle(s.rel) : null;
  if (typedAng == null && rel != null && s.ref) {
    const m = angleFrom(s.ref, { x: dx, y: dy });
    const d = directionAt(m?.ray ?? s.ref.dir, m?.side ?? 1, rel);
    typedAng = (Math.atan2(d.y, d.x) * 180) / Math.PI;
  }
  const len = typedLen ?? Math.hypot(dx, dy);
  const ang = typedAng != null ? (typedAng * Math.PI) / 180 : Math.atan2(dy, dx);
  if (typedLen == null && typedAng == null) return c;
  if (typedLen == null && typedAng != null) {
    // The direction held: the cursor's distance along it.
    const along = dx * Math.cos(ang) + dy * Math.sin(ang);
    return { x: s.base.x + Math.cos(ang) * along, y: s.base.y + Math.sin(ang) * along };
  }
  return { x: s.base.x + Math.cos(ang) * len, y: s.base.y + Math.sin(ang) * len };
}

/** The segment as shown: its length (m) and angle (degrees from +X). */
export function liveMeasure(s: Pick<LiveState, 'base' | 'cursor' | 'len' | 'ang'> & { mode?: LiveMode; measure?: boolean; rel?: string; ref?: AngleRef | null; lock?: AlongLine | null } = useLive.getState()): { len: number; ang: number; dx: number; dy: number; end: Vec2 } | null {
  const end = livePoint(s);
  if (!s.base || !end) return null;
  const dx = end.x - s.base.x, dy = end.y - s.base.y;
  // Kept on its line: the angle shown is the element's own, the length the change.
  if (s.lock) return { len: Math.hypot(dx, dy), ang: ((Math.atan2(s.lock.dir.y, s.lock.dir.x) * 180) / Math.PI + 360) % 360, dx, dy, end };
  return { len: Math.hypot(dx, dy), ang: ((Math.atan2(dy, dx) * 180) / Math.PI + 360) % 360, dx, dy, end };
}

/**
 * A click while a length, angle, width or height is typed: the click takes what the preview shows, the cursor giving only
 * the direction (or the sign), and the typed fields are spent. Null when nothing is typed (the click is the cursor's point),
 * or when the box belongs to a gesture other than `owners`. `typed` is the length typed, for a tool measuring along its own path.
 */
export function liveTaken(cursor: Vec2, owners: readonly string[] = ['tool', 'cad']): { point: Vec2; typed?: number } | null {
  const s = useLive.getState();
  if (!s.owner || !owners.includes(s.owner) || !s.base || !liveTyped(s)) return null;
  const point = livePoint({ ...s, cursor });
  if (!point) return null;
  const typed = s.mode === 'rect' ? undefined : parseLength(s.len) ?? undefined;
  useLive.setState({ len: '', ang: '', rel: '' });
  return { point, typed };
}

/**
 * Enter in a held drag (autohold, issue 86) with nothing typed: the drag is put where the cursor is (its snap, as the last move
 * set it). True when the box had a base, a cursor and a commit to hand it to.
 */
export function liveAcceptAtCursor(): boolean {
  const s = useLive.getState();
  if (!s.commit || !s.base || !s.cursor) return false;
  const commit = s.commit;
  const p = livePoint(s) ?? s.cursor;
  useLive.setState({ len: '', ang: '', rel: '' });
  commit(p);
  return true;
}

/** Enter: the typed point is taken (true when there was something to take). */
export function liveCommit(): boolean {
  const s = useLive.getState();
  if (!s.commit || !s.base || !liveTyped(s)) return false;
  const p = livePoint(s);
  if (!p) return false;
  const commit = s.commit;
  const typed = s.mode === 'rect' ? undefined : parseLength(s.len) ?? undefined;
  // An angle to the reference typed is handed on as the angle from X it came to.
  const typedAngle = s.mode === 'rect' ? undefined : parseAngle(s.ang) ?? (s.rel && parseAngle(s.rel) != null ? (Math.atan2(p.y - s.base.y, p.x - s.base.x) * 180) / Math.PI : undefined);
  useLive.setState({ len: '', ang: '', rel: '' });
  commit(p, typed, typedAngle);
  return true;
}
