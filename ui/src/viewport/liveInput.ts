/**
 * Live input while drawing or dragging: the segment's length and angle shown
 * at the cursor and in the bar over the view, and typed there. Lengths are
 * shown and typed in millimetres like the rest of the interface (a typed
 * value may carry its unit: 2400, 240cm, 2.4m, 3000+250); angles in degrees
 * from +X, counter-clockwise. A typed length holds while the cursor sets the
 * direction; a typed angle holds the direction; Enter takes the point.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';

/** A length in metres as the interface shows it (mm). */
export const formatLength = (m: number) => `${Math.round(m * 1000)}`;
export const formatAngle = (deg: number) => `${(Math.round(deg * 10) / 10).toFixed(1)}`;

const UNIT: Record<string, number> = { mm: 0.001, cm: 0.01, dm: 0.1, m: 1 };

/** A typed length (mm unless it says), sums and differences allowed; metres, or null. */
export function parseLength(text: string, unit: keyof typeof UNIT = 'mm'): number | null {
  const t = text.replace(/\s+/g, '').replace(',', '.');
  if (!t) return null;
  const terms = t.match(/[+-]?[^+-]+/g);
  if (!terms) return null;
  let sum = 0;
  for (const term of terms) {
    const m = /^([+-]?)(\d+\.?\d*|\.\d+)(mm|cm|dm|m)?$/.exec(term);
    if (!m) return null;
    sum += (m[1] === '-' ? -1 : 1) * Number(m[2]) * UNIT[m[3] ?? unit];
  }
  return sum;
}

/** A typed angle in degrees (a ° allowed, sums too), or null. */
export function parseAngle(text: string): number | null {
  const t = text.replace(/\s+/g, '').replace(/°/g, '').replace(',', '.');
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

export type LiveField = 'len' | 'ang';

interface LiveState {
  /** Who asked for it ("tool", "grip", "cad"); null: nothing is being drawn. */
  owner: string | null;
  base: Vec2 | null;
  cursor: Vec2 | null;
  len: string;
  ang: string;
  focus: LiveField;
  /** What a typed point does. */
  commit: ((p: Vec2) => void) | null;
}

export const useLive = create<LiveState>(() => ({ owner: null, base: null, cursor: null, len: '', ang: '', focus: 'len', commit: null }));

/** A segment starts at `base`: its length and angle can now be typed. */
export function liveBegin(owner: string, base: Vec2, commit: (p: Vec2) => void): void {
  const s = useLive.getState();
  if (s.owner === owner && s.base && Math.hypot(s.base.x - base.x, s.base.y - base.y) < 1e-12) { useLive.setState({ commit }); return; }
  useLive.setState({ owner, base, commit, len: '', ang: '', focus: 'len' });
}
export const liveCursor = (p: Vec2 | null) => useLive.setState({ cursor: p });
export function liveEnd(owner?: string): void {
  if (owner && useLive.getState().owner !== owner) return;
  useLive.setState({ owner: null, base: null, commit: null, len: '', ang: '' });
}

/** Where the segment ends: the typed length and angle, else the cursor's. */
export function livePoint(s: Pick<LiveState, 'base' | 'cursor' | 'len' | 'ang'> = useLive.getState()): Vec2 | null {
  if (!s.base) return null;
  const c = s.cursor ?? { x: s.base.x + 1, y: s.base.y };
  const dx = c.x - s.base.x, dy = c.y - s.base.y;
  const typedLen = parseLength(s.len), typedAng = parseAngle(s.ang);
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
export function liveMeasure(s: Pick<LiveState, 'base' | 'cursor' | 'len' | 'ang'> = useLive.getState()): { len: number; ang: number; dx: number; dy: number; end: Vec2 } | null {
  const end = livePoint(s);
  if (!s.base || !end) return null;
  const dx = end.x - s.base.x, dy = end.y - s.base.y;
  return { len: Math.hypot(dx, dy), ang: ((Math.atan2(dy, dx) * 180) / Math.PI + 360) % 360, dx, dy, end };
}

/** Enter: the typed point is taken (true when there was something to take). */
export function liveCommit(): boolean {
  const s = useLive.getState();
  if (!s.commit || !s.base || (!s.len && !s.ang)) return false;
  const p = livePoint(s);
  if (!p) return false;
  const commit = s.commit;
  useLive.setState({ len: '', ang: '' });
  commit(p);
  return true;
}
