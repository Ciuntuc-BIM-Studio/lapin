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

/** The section (or elevation) the 3D view shows, by its view's id (null: none), and whether its annotations show. */
export const useSection3d = create<{ viewId: string | null; annotations: boolean }>(() => ({ viewId: null, annotations: true }));

/** Show a view in 3D, or stop showing it when it already is. */
export function toggleSection3d(viewId: string): void {
  useSection3d.setState((s) => ({ viewId: s.viewId === viewId ? null : viewId }));
}

/** A drawing point in the model, `lift` metres into the scene from the drawing's plane. */
export function drawingPoint(f: ViewFrameDto, x: number, y: number, lift = 0): Vec3 {
  const at = (k: number) => f.origin[k] + f.right[k] * x + f.up[k] * y + f.dir[k] * lift;
  return { x: at(0), y: at(1), z: at(2) };
}

/** A clipping plane as three.js takes it: what lies where normal·p + constant < 0 is cut away. */
export interface ClipPlane { normal: [number, number, number]; constant: number }

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
