/**
 * Grading (site grading T5, docs/plan-site-grading.md; forum 230 §4): what the tools and the inspector work out before asking the
 * engine — which side of a line a click is on, a slope typed as 1:N, the edges picked so far for a grading between them.
 */

import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';
import { fromPairs } from '../../viewport/contour';

export const GRADING = 'LapinGrading';

/** What a grading's edge can follow: a hardscape, a feature line, a platform, a path or road, a retaining wall, a ditch. */
export const EDGE_TYPES = ['LapinHardscape', 'LapinFeatureLine', 'IfcEarthworksCut', 'IfcPavement', 'LapinRetainingWall'];

/** The line in plan an element offers as an edge (a hardscape's or a platform's outline, a feature line's corners, a path's axis), and whether it is closed. */
export function lineOfEdge(n: NodeDto): { pts: Vec2[]; closed: boolean } {
  if (n.type === 'LapinHardscape' || (n.type === 'IfcEarthworksCut' && Array.isArray(n.props.contour))) return { pts: fromPairs(n.props.contour), closed: true };
  if (n.type === 'LapinFeatureLine') return { pts: fromPairs(n.props.points), closed: n.props.closed === true };
  return { pts: fromPairs(n.props.path), closed: false };
}

/** Which side of an open polyline a point lies on, by its nearest segment: left or right as the line runs. */
export function sideOf(pts: Vec2[], at: Vec2): 'left' | 'right' {
  let best = Infinity;
  let side: 'left' | 'right' = 'right';
  for (let i = 0; i + 1 < pts.length; i++) {
    const [a, b] = [pts[i], pts[i + 1]];
    const dx = b.x - a.x, dy = b.y - a.y;
    const len2 = dx * dx + dy * dy;
    if (len2 < 1e-18) continue;
    const t = Math.max(0, Math.min(1, ((at.x - a.x) * dx + (at.y - a.y) * dy) / len2));
    const qx = a.x + dx * t, qy = a.y + dy * t;
    const d = Math.hypot(at.x - qx, at.y - qy);
    if (d < best) { best = d; side = dx * (at.y - a.y) - dy * (at.x - a.x) > 0 ? 'left' : 'right'; }
  }
  return side;
}

/** The side a slope runs to from an element picked at a point: outwards from a closed outline, else the side clicked. */
export function slopeSide(n: NodeDto, at: Vec2): 'out' | 'left' | 'right' {
  const { pts, closed } = lineOfEdge(n);
  return closed || pts.length < 2 ? 'out' : sideOf(pts, at);
}

/** A slope 1 : N shown as „1:N”. */
export const slopeText = (n: number): string => `1:${Math.round(n * 100) / 100}`;

/** A slope typed: „1:2”, „2” (its N), „50 %” (rise over run); null when it does not read or is not over 0. */
export function parseSlope(text: string): number | null {
  const t = text.trim().replace(/\s+/g, '').replace(',', '.');
  if (!t) return null;
  if (t.startsWith('1:')) { const n = Number(t.slice(2)); return Number.isFinite(n) && n > 0 ? n : null; }
  if (t.endsWith('%')) { const p = Number(t.slice(0, -1)); return Number.isFinite(p) && p > 0 ? 100 / p : null; }
  const n = Number(t);
  return Number.isFinite(n) && n > 0 ? n : null;
}

/** The edges picked so far with one more: picked again, it is let go. */
export const withPick = (picked: string[], id: string): string[] => (picked.includes(id) ? picked.filter((x) => x !== id) : [...picked, id]);

/** Whether a grading between the edges picked is made now: two picked, unless more are to come. */
export const readyToGrade = (picked: string[], more: boolean): boolean => !more && picked.length >= 2;

export interface GradingEdge { own: boolean; source?: string; name?: string; points?: (number | null)[][] }

/** A grading's edges as its node carries them. */
export const gradingEdges = (n: NodeDto): GradingEdge[] => (Array.isArray(n.props.gradingEdges) ? n.props.gradingEdges as unknown as GradingEdge[] : []);

/** An edge's own points' heights for its table: [x, y, z | null]. */
export function ownPoints(e: GradingEdge): { x: number; y: number; z: number | null }[] {
  return (e.points ?? []).map((p) => ({ x: Number(p[0]), y: Number(p[1]), z: p.length > 2 && p[2] != null && Number.isFinite(Number(p[2])) ? Number(p[2]) : null }));
}
