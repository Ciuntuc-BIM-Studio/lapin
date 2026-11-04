/**
 * The project's profiles (decision 297), as the engine keeps them (Lapin.Platform/Profiles.cs): a closed outline in its
 * own (u, v) plane in metres, each corner `[u, v]` or `[u, v, bulge]` (the side leaving it an arc, DXF's bulge: tan of a
 * quarter of its turn, positive anticlockwise), and the insertion point laid on what it is swept along. What the dialogs
 * draw and read: the outline with its arcs as chords, its preview as an SVG path, the corners typed in millimetres.
 */

export type ProfilePoint = [number, number] | [number, number, number];
export interface ProfileJson { name?: string; points: ProfilePoint[]; insertion?: [number, number] }
export interface CatalogueParam { key: string; label: string; default: number }
export interface CatalogueProfile { id: string; label: string; use: 'sweep' | 'reveal' | 'footing'; params: CatalogueParam[]; shape: ProfileJson }

type P = [number, number];

/** The outline relative to the insertion point, anticlockwise, arcs as chords (about 1 mm off at most, as the engine's). */
export function profileOutline(shape: ProfileJson): P[] {
  const pts = shape.points;
  const [iu, iv] = shape.insertion ?? [0, 0];
  const o: P[] = [];
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i];
    const b = pts[(i + 1) % pts.length];
    o.push([a[0] - iu, a[1] - iv]);
    const bulge = a[2] ?? 0;
    const c = Math.hypot(b[0] - a[0], b[1] - a[1]);
    if (Math.abs(bulge) < 1e-9 || c < 1e-9) continue;
    const theta = 4 * Math.atan(bulge);
    const ux = (b[0] - a[0]) / c;
    const uy = (b[1] - a[1]) / c;
    const d = c / 2 / Math.tan(theta / 2);
    const cx = (a[0] + b[0]) / 2 - uy * d;
    const cy = (a[1] + b[1]) / 2 + ux * d;
    const r = Math.hypot(a[0] - cx, a[1] - cy);
    const a0 = Math.atan2(a[1] - cy, a[0] - cx);
    const step = r > 0.002 ? Math.min(Math.PI / 12, Math.max(Math.PI / 360, 2 * Math.acos(1 - 0.001 / r))) : Math.PI / 12;
    const n = Math.max(2, Math.ceil(Math.abs(theta) / step - 1e-9));
    for (let k = 1; k < n; k++) {
      const t = a0 + (theta * k) / n;
      o.push([cx + r * Math.cos(t) - iu, cy + r * Math.sin(t) - iv]);
    }
  }
  return signedArea(o) < 0 ? o.reverse() : o;
}

export function signedArea(pts: P[]): number {
  let s = 0;
  for (let i = 0; i < pts.length; i++) {
    const [x0, y0] = pts[i];
    const [x1, y1] = pts[(i + 1) % pts.length];
    s += x0 * y1 - x1 * y0;
  }
  return s / 2;
}

export const profileArea = (shape: ProfileJson): number => Math.abs(signedArea(profileOutline(shape)));

/** The outline drawn in a box of w × h px with a margin, at one scale, v up; the insertion point as (ix, iy) in the box. */
export function profileSvg(shape: ProfileJson, w: number, h: number, pad = 8): { path: string; ix: number; iy: number } {
  const o = profileOutline(shape);
  if (o.length < 3) return { path: '', ix: w / 2, iy: h / 2 };
  const us = [...o.map((p) => p[0]), 0];
  const vs = [...o.map((p) => p[1]), 0];
  const [u0, u1, v0, v1] = [Math.min(...us), Math.max(...us), Math.min(...vs), Math.max(...vs)];
  const k = Math.min((w - 2 * pad) / Math.max(u1 - u0, 1e-9), (h - 2 * pad) / Math.max(v1 - v0, 1e-9));
  const ox = (w - (u1 - u0) * k) / 2;
  const oy = (h - (v1 - v0) * k) / 2;
  const X = (u: number) => +(ox + (u - u0) * k).toFixed(2);
  const Y = (v: number) => +(h - oy - (v - v0) * k).toFixed(2);
  return { path: `${o.map((p, i) => `${i ? 'L' : 'M'}${X(p[0])} ${Y(p[1])}`).join(' ')} Z`, ix: X(0), iy: Y(0) };
}

const mm = (m: number) => String(Math.round(m * 10000) / 10);

/** The corners as typed: `u v` in millimetres (a space between, a decimal comma or point) (and the bulge, when the side is an arc), a semicolon between corners. */
export function formatPoints(points: ProfilePoint[]): string {
  return points.map((p) => (p[2] ? `${mm(p[0])} ${mm(p[1])} ${+p[2].toFixed(4)}` : `${mm(p[0])} ${mm(p[1])}`)).join('; ');
}

/** The corners read back from what was typed; null when a corner is not two or three numbers, or fewer than three corners. */
export function parsePoints(text: string): ProfilePoint[] | null {
  const parts = text.split(';').map((s) => s.trim()).filter(Boolean);
  const out: ProfilePoint[] = [];
  for (const part of parts) {
    const n = part.split(/\s+/).filter(Boolean).map((x) => Number(x.replace(',', '.')));
    if ((n.length !== 2 && n.length !== 3) || n.some((x) => !Number.isFinite(x))) return null;
    const p: ProfilePoint = n.length === 3 && n[2] !== 0 ? [n[0] / 1000, n[1] / 1000, Math.max(-1, Math.min(1, n[2]))] : [n[0] / 1000, n[1] / 1000];
    out.push(p);
  }
  return out.length >= 3 || (out.length === 2 && out.some((p) => p[2])) ? out : null;
}

/** The profile's size across (u) and up (v), metres. */
export function profileSize(shape: ProfileJson): { width: number; height: number } {
  const o = profileOutline(shape);
  if (!o.length) return { width: 0, height: 0 };
  const us = o.map((p) => p[0]);
  const vs = o.map((p) => p[1]);
  return { width: Math.max(...us) - Math.min(...us), height: Math.max(...vs) - Math.min(...vs) };
}
