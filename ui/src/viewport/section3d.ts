/**
 * A section seen in 3D: the model cut by the section's plane (what lies
 * between the viewer and the cut taken away, and past the section's depth
 * when it has one), with the section's own drawing (the engine's, the one
 * sheets print: its cut fills, hatches and lines) laid on the cut, so its
 * fills close the cut solids, and its annotations (dimensions, levels, tags,
 * texts) on the same plane, where the drawing has them, beside the building
 * too. An elevation only turns the camera to look the way it does. The drawing says where it lies (`frame`): a drawing point
 * (x, y) is origin + right·x + up·y, and `dir` looks into the scene.
 */

import { create } from 'zustand';
import type { CameraPose, Vec3 } from './camera';
import type { PenDto } from './drawing';

export interface ViewFrameDto { origin: number[]; right: number[]; up: number[]; dir: number[] }

/**
 * The section (or elevation) the 3D view shows, by its view's id (null: none), whether its annotations show,
 * and how the cut is closed: by caps in the cut elements' own materials (the default), or by the section's
 * drawing (its cut fills and hatches, as sheets print it).
 */
export const useSection3d = create<{ viewId: string | null; annotations: boolean; caps: 'material' | 'drawing' }>(() => ({ viewId: null, annotations: true, caps: 'material' }));

/** Show a view in 3D, or stop showing it when it already is. */
export function toggleSection3d(viewId: string): void {
  useSection3d.setState((s) => ({ viewId: s.viewId === viewId ? null : viewId }));
}

/** A drawing point in the model, `lift` metres into the scene from the drawing's plane. */
export function drawingPoint(f: ViewFrameDto, x: number, y: number, lift = 0): Vec3 {
  const at = (k: number) => f.origin[k] + f.right[k] * x + f.up[k] * y + f.dir[k] * lift;
  return { x: at(0), y: at(1), z: at(2) };
}

/** A piece of a drawing laid in 3D: where it lies, and the pens (in the piece's own x, from its start) that fall on it. */
export interface SectionPiece { frame: ViewFrameDto; pens: PenDto[] }

/**
 * A drawing as pieces: one, as it is, for a plain section; for a developed
 * section (`frames`) each piece's frame with the drawing's pens cut to the
 * stretch of x the piece covers (a segment split where it crosses, a loop
 * clipped, a text by its anchor) and moved to start at 0.
 */
export function sectionPieces(d: { frame?: ViewFrameDto; frames?: (ViewFrameDto & { x0: number; x1: number })[]; pens: PenDto[] }): SectionPiece[] {
  if (!d.frames || d.frames.length < 2) return d.frame ? [{ frame: d.frame, pens: d.pens }] : [];
  return d.frames.map((f) => {
    const { x0, x1 } = f;
    const pens: PenDto[] = [];
    for (const pen of d.pens) {
      const segments: number[] = [];
      const s = pen.segments ?? [];
      for (let k = 0; k + 3 < s.length; k += 4) {
        let [ax, ay, bx, by] = [s[k], s[k + 1], s[k + 2], s[k + 3]];
        if (Math.max(ax, bx) < x0 || Math.min(ax, bx) > x1) continue;
        const at = (x: number) => ay + ((by - ay) * (x - ax)) / (bx - ax || 1e-12);
        if (ax < x0) { ay = at(x0); ax = x0; } else if (ax > x1) { ay = at(x1); ax = x1; }
        if (bx < x0) { by = at(x0); bx = x0; } else if (bx > x1) { by = at(x1); bx = x1; }
        segments.push(ax - x0, ay, bx - x0, by);
      }
      const loops = (pen.loops ?? []).map((l) => clipLoopX(l, x0, x1).map((v, i) => (i % 2 === 0 ? v - x0 : v))).filter((l) => l.length >= 6);
      const texts = (pen.texts ?? []).filter((t) => t.x >= x0 - 1e-9 && t.x <= x1 + 1e-9).map((t) => ({ ...t, x: t.x - x0 }));
      if (segments.length || loops.length || texts.length) pens.push({ ...pen, segments, loops, texts });
    }
    return { frame: f, pens };
  });
}

/** A loop (x y x y …) kept between x0 and x1, Sutherland–Hodgman. */
function clipLoopX(loop: number[], x0: number, x1: number): number[] {
  const half = (pts: number[], inside: (x: number) => number): number[] => {
    const o: number[] = [];
    const n = pts.length / 2;
    for (let i = 0; i < n; i++) {
      const [px, py] = [pts[2 * i], pts[2 * i + 1]], [qx, qy] = [pts[(2 * (i + 1)) % pts.length], pts[(2 * (i + 1) + 1) % pts.length]];
      const dp = inside(px), dq = inside(qx);
      if (dp >= 0) o.push(px, py);
      if ((dp >= 0) !== (dq >= 0)) { const t = dp / (dp - dq); o.push(px + (qx - px) * t, py + (qy - py) * t); }
    }
    return o;
  };
  return half(half(loop, (x) => x - x0), (x) => x1 - x);
}

/** A clipping plane as three.js takes it: what lies where normal·p + constant < 0 is cut away. */
export interface ClipPlane { normal: [number, number, number]; constant: number }

/**
 * The planes the 3D view cuts the model by right now: the section shown, the
 * section box's faces and the cut above the storeys shown (what the renderer
 * clips by), for what works on the model as the view shows it (the 3D exports).
 */
export const useClip3d = create<{ planes: ClipPlane[] }>(() => ({ planes: [] }));

/** Depths at or past this are no limit (the engine's ViewRanges.Unlimited). */
const UNLIMITED = 1e4;

/** A section's planes: the cut (what is in front of it goes) and, with a depth, the far one (what is beyond it goes). */
export function sectionPlanes(f: ViewFrameDto, depth?: number | null): ClipPlane[] {
  const d = f.dir, o = f.origin;
  const at = d[0] * o[0] + d[1] * o[1] + d[2] * o[2];
  const planes: ClipPlane[] = [{ normal: [d[0], d[1], d[2]], constant: -at }];
  if (depth != null && depth > 0 && depth < UNLIMITED) planes.push({ normal: [-d[0], -d[1], -d[2]], constant: at + depth });
  return planes;
}

/** Pens of a drawing that are the cut itself: what is cut, its fills, hatches and membranes. */
export const CUT_LAYERS = new Set(['CUT', 'FILL', 'HATCH', 'MEMBRANE']);

/**
 * Pens never laid on the cut: what is seen beyond it, hidden or overhead (the
 * model itself shows it, in depth) and the rooms' fills (they would hide it).
 */
export const MODEL_LAYERS = new Set(['SEEN', 'BEYOND', 'HIDDEN', 'OVERHEAD', 'ROOM-FILL']);

/** Whether a pen is laid on the cut: the cut's always, every other (dimensions, levels, tags, texts) with the annotations. */
export function onCut(layer: string, annotations: boolean): boolean {
  if (MODEL_LAYERS.has(layer)) return false;
  return CUT_LAYERS.has(layer) || annotations;
}

/** Paper millimetres in the model's metres at a drawing's scale: 2.5 mm at 1:100 is 0.25 m. */
export const paperToModel = (mm: number, scale: number | undefined) => (mm * (scale ?? 100)) / 1000;

export type DrawingText = NonNullable<PenDto['texts']>[number];

/**
 * Where a drawing's text lies on its plane, as the 2D drawing sets it
 * (DrawingLayer): its height is the font's size on paper, its anchor the
 * start, middle or end of its line, on its baseline or (baseline "middle")
 * its middle, turned by its angle (degrees, anticlockwise) round the anchor.
 * `width` and the font's box are in ems (`ascent` over the anchor's line,
 * `descent` under it, `pad` round it all); the corners come bottom-left,
 * bottom-right, top-right, top-left in drawing metres: read left to right
 * along the text's way.
 */
export function textQuad(t: DrawingText, scale: number | undefined, width: number, ascent: number, descent: number, pad = 0): { x: number; y: number }[] {
  const em = paperToModel(t.height, scale);
  const x0 = (t.anchor === 'start' ? 0 : t.anchor === 'end' ? -width : -width / 2) - pad;
  const x1 = x0 + width + 2 * pad;
  const a = ((t.angle ?? 0) * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  const at = (u: number, v: number) => ({ x: t.x + (u * c - v * s) * em, y: t.y + (u * s + v * c) * em });
  return [at(x0, -descent - pad), at(x1, -descent - pad), at(x1, ascent + pad), at(x0, ascent + pad)];
}

/** A text's font on the canvas, as the 2D drawing's (DrawingLayer) at `px` pixels to the em. */
export function textFont(t: DrawingText, px: number): string {
  const family = t.font === 'serif' ? 'ui-serif, Georgia, serif' : t.font === 'mono' ? 'ui-monospace, Menlo, monospace' : 'ui-sans-serif, system-ui, sans-serif';
  return `${t.bold ? 700 : 400} ${px}px ${family}`;
}

/** A loop's x y pairs as points. */
const ptsOf = (loop: number[]) => Array.from({ length: loop.length >> 1 }, (_, i) => ({ x: loop[2 * i], y: loop[2 * i + 1] }));

function inside(p: { x: number; y: number }, poly: { x: number; y: number }[]): boolean {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i], b = poly[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) c = !c;
  }
  return c;
}

/**
 * Loops filled even-odd as shapes with holes: a loop inside an even number of
 * others is an outline, one inside an odd number a hole of the smallest loop
 * round it.
 */
export function evenOddShapes(loops: number[][]): { outer: number[]; holes: number[][] }[] {
  const kept = loops.filter((l) => l.length >= 6);
  const polys = kept.map(ptsOf);
  const area = (p: { x: number; y: number }[]) => Math.abs(p.reduce((s, a, i) => { const b = p[(i + 1) % p.length]; return s + a.x * b.y - b.x * a.y; }, 0) / 2);
  const areas = polys.map(area);
  // The loops each lies in (tested by a point of its own).
  const within = polys.map((p, i) => polys.map((q, j) => (j !== i && areas[j] > areas[i] && inside(p[0], q) ? j : -1)).filter((j) => j >= 0));
  const shapes = new Map<number, { outer: number[]; holes: number[][] }>();
  polys.forEach((_, i) => { if (within[i].length % 2 === 0) shapes.set(i, { outer: kept[i], holes: [] }); });
  polys.forEach((_, i) => {
    if (within[i].length % 2 === 0) return;
    const host = within[i].filter((j) => shapes.has(j)).sort((a, b) => areas[a] - areas[b])[0];
    if (host != null) shapes.get(host)!.holes.push(kept[i]);
  });
  return [...shapes.values()];
}

/**
 * The camera on a section shown in 3D: standing on the viewer's side of the
 * cut, looking along the section a little from the side and above, so the
 * cut and the depth behind it both show; the drawing's bounds framed.
 */
export function sectionPose(f: ViewFrameDto, bounds: [number, number, number, number] | undefined, fov: number): CameraPose {
  const b = bounds ?? [-5, 0, 5, 3];
  const target = drawingPoint(f, (b[0] + b[2]) / 2, (b[1] + b[3]) / 2);
  const radius = Math.max(Math.hypot(b[2] - b[0], b[3] - b[1]) / 2, 1);
  return { azimuth: Math.atan2(f.dir[1], f.dir[0]) + 0.45, pitch: -0.3, distance: (radius / Math.sin(fov / 2)) * 1.05, target };
}

/** A pen's colour as three.js takes it, and how opaque: #rgb / #rrggbb / rgb() / rgba(); null: none. */
export function cssColor(s: string | null | undefined): { hex: string; opacity: number } | null {
  if (!s || s === 'none' || s === 'transparent') return null;
  const m = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:[\s,/]+([\d.]+%?))?\s*\)$/i.exec(s.trim());
  if (m) {
    const h = (v: string) => Math.max(0, Math.min(255, Math.round(Number(v)))).toString(16).padStart(2, '0');
    const a = m[4] == null ? 1 : m[4].endsWith('%') ? Number(m[4].slice(0, -1)) / 100 : Number(m[4]);
    return { hex: `#${h(m[1])}${h(m[2])}${h(m[3])}`, opacity: a };
  }
  if (/^#[0-9a-f]{8}$/i.test(s)) return { hex: s.slice(0, 7), opacity: parseInt(s.slice(7), 16) / 255 };
  return { hex: s, opacity: 1 };
}

/** CSS pixels per paper millimetre: a pen drawn on the cut as wide as on a sheet at 100 %. */
const PX_PER_MM = 96 / 25.4;

/** A pen's width on the screen, in CSS pixels (a hairline still one pixel). */
export const penPixels = (mm: number): number => Math.max(1, (mm || 0) * PX_PER_MM);

/**
 * Segments (x1 y1 x2 y2 …, metres) cut into the dashes of a pattern (paper
 * mm at the view's scale: dash, gap, dash, gap…; a 0 dash is a dot), as the
 * 2D drawing dashes them; the pattern goes on where a segment starts at the
 * previous one's end, so a polyline is dashed as one line.
 */
export function dashSegments(segs: number[], pattern: number[], scale: number | undefined): number[] {
  const raw = pattern.map((v) => paperToModel(Math.abs(v), scale));
  if (raw.length < 2 || !(raw.reduce((a, b) => a + b, 0) > 1e-9)) return segs;
  // A dot (a 0 dash) takes no length, as SVG's: drawn as a dash a tenth of the shortest step, so it shows. An odd list repeats.
  const dot = Math.max(1e-4, Math.min(...raw.filter((v) => v > 0)) / 10);
  const steps = raw.length % 2 ? [...raw, ...raw] : raw;
  const out: number[] = [];
  let idx = 0, rem = steps[0], fresh = true;
  let lx = NaN, ly = NaN;
  for (let k = 0; k + 3 < segs.length; k += 4) {
    const [x1, y1, x2, y2] = segs.slice(k, k + 4);
    if (!(Math.hypot(x1 - lx, y1 - ly) <= 1e-6)) { idx = 0; rem = steps[0]; fresh = true; }
    lx = x2; ly = y2;
    const len = Math.hypot(x2 - x1, y2 - y1);
    if (len < 1e-9) continue;
    const ux = (x2 - x1) / len, uy = (y2 - y1) / len;
    const put = (a: number, b: number) => out.push(x1 + ux * a, y1 + uy * a, x1 + ux * b, y1 + uy * b);
    let at = 0;
    while (len - at > 1e-9) {
      if (fresh && idx % 2 === 0 && steps[idx] === 0) put(at, Math.min(len, at + dot));
      fresh = false;
      if (rem <= 1e-12) { idx = (idx + 1) % steps.length; rem = steps[idx]; fresh = true; continue; }
      const piece = Math.min(rem, len - at);
      if (idx % 2 === 0) put(at, at + piece);
      at += piece; rem -= piece;
    }
  }
  return out;
}
