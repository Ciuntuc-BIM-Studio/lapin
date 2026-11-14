/** A view's 2D drawing from the engine (`view.drawing`): pens in the view's coordinates (metres), widths and dashes in paper mm. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../engine/session';

export interface PenDto {
  layer: string;
  stroke: string | null;
  /** Paper millimetres. */
  width: number;
  fill: string | null;
  /** Paper millimetres. */
  dash?: number[];
  /** x1 y1 x2 y2 … */
  segments?: number[];
  /** Closed loops, x y x y … each. */
  loops?: number[][];
  texts?: { x: number; y: number; text: string; height: number; angle: number; anchor: string; baseline: string; bold: boolean; font: string; italic?: boolean }[];
  /** Pictures (an orthophoto): a LapinImage's id, its corners (top left, top right, bottom right, bottom left: x y …), its opacity; a point cloud's slice carries its own bytes (data, base64). */
  images?: { id: string; corners: number[]; opacity: number; data?: string; mime?: string }[];
  /** On a sheet: the viewport the pen draws for, so the view's pens move alone while it is dragged. */
  viewport?: string;
  /** The box the pen is clipped to: x0 y0 x1 y1 (a sheet's viewport). */
  clip?: number[];
}

/**
 * Whether a view's drawing is drawn straight on the theme's dark background:
 * plans, sections, elevations and the other drawing panes in the graphite
 * theme (report #90). Sheets and schedules stay on their white paper, as they print.
 */
export function drawsOnDark(theme: string | null | undefined): boolean {
  return theme === 'graphite';
}

/** What a dark plan shows for black: the graphite theme's ink. */
export const DARK_INK = '#e9e4da';

/**
 * A pen's colour on screen. On a dark background black and near-black pens
 * (luminance under 4 %) are drawn in the theme's ink, as CAD shows its black
 * on a dark model space; colours keep their own. Paper (sheets, PDF) keeps black.
 */
export function screenInk(color: string | null | undefined, onDark: boolean): string | null {
  if (!color || !onDark) return color ?? null;
  const m = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(color.trim());
  if (!m) return /^(black|rgb\(\s*0\s*,\s*0\s*,\s*0\s*\))$/i.test(color.trim()) ? DARK_INK : color;
  const h = m[1].length === 3 ? m[1].replace(/./g, (c) => c + c) : m[1];
  const lin = (k: number) => { const v = parseInt(h.slice(k, k + 2), 16) / 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  const lum = 0.2126 * lin(0) + 0.7152 * lin(2) + 0.0722 * lin(4);
  return lum < 0.04 ? DARK_INK : color;
}

/** The paper a dark plan is drawn on, for fills of the paper's white (a Studio solid's cut). */
export const DARK_PAPER = '#3a3732';

/** A fill on screen: on a dark background the paper's white (luminance over 90 %) becomes the dark paper, so a cut stays a quiet surface; other fills as screenInk. */
export function screenFill(color: string | null | undefined, onDark: boolean): string | null {
  if (!color || !onDark) return color ?? null;
  const m = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(color.trim());
  if (!m) return /^white$/i.test(color.trim()) ? DARK_PAPER : screenInk(color, onDark);
  const h = m[1].length === 3 ? m[1].replace(/./g, (c) => c + c) : m[1];
  const lin = (k: number) => { const v = parseInt(h.slice(k, k + 2), 16) / 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  return 0.2126 * lin(0) + 0.7152 * lin(2) + 0.0722 * lin(4) > 0.9 ? DARK_PAPER : screenInk(color, onDark);
}

/**
 * A colour's opacity as drawings carry it (forum #79: a room colour's, #rrggbbaa, as the engine's HexColor writes it):
 * the colour alone and its opacity 0–1 (1 for any other colour).
 */
export function splitAlpha(color: string | null | undefined): { color: string | null; opacity: number } {
  if (!color) return { color: color ?? null, opacity: 1 };
  const m = /^#([0-9a-f]{6})([0-9a-f]{2})$/i.exec(color.trim());
  return m ? { color: `#${m[1]}`, opacity: Math.round((parseInt(m[2], 16) / 255) * 1000) / 1000 } : { color, opacity: 1 };
}

/** #rrggbb with an opacity in per cent: #rrggbbaa below 100 %, the colour alone otherwise (the engine's ColorSchemes.WithOpacity). */
export function withOpacity(color: string, percent: number | null | undefined): string {
  const base = splitAlpha(color).color ?? color;
  if (percent == null || !Number.isFinite(percent) || percent >= 100 || !/^#[0-9a-f]{6}$/i.test(base)) return base;
  const a = Math.round(Math.max(0, percent) / 100 * 255);
  return `${base}${a.toString(16).padStart(2, '0')}`;
}

export interface DrawingDto {
  kind?: string;
  scale?: number;
  /** How lines end and meet (the project's standards): round, or square with mitred corners. */
  lineCap?: 'round' | 'square';
  units?: string;
  pens: PenDto[];
  bounds?: [number, number, number, number];
  problem?: string;
  /** Annotations a click selects: their entity, segments and snap points. */
  marks?: { id: string; segments: number[]; snaps?: number[]; data?: Record<string, unknown>; hit?: { solid: boolean; loops: number[][] } }[];
  problems?: string[];
  /** The categories (walls, slabs) whose finish layers the view leaves out (topic #30): their cut pieces offer no snap. */
  hiddenFinishes?: string[];
  /** The view's partial structure display (decision 500), and the layers' roles it leaves out of every layered element: their cut pieces offer no snap. */
  structure?: 'all' | 'noFinishes' | 'core' | 'coreLoadBearing';
  hiddenSubs?: string[];
  /** Line edits (Linework) whose edge is gone. */
  lineworkOrphans?: string[];
  /** Where the drawing lies in the model: (x, y) is origin + right·x + up·y; dir looks into the scene. */
  frame?: { origin: number[]; right: number[]; up: number[]; dir: number[] };
  /** A section's depth past its line (none: unlimited). */
  depth?: number;
  /** A developed section (jogged, curved, along an element): each piece's frame and the stretch of x (its stations) it covers; a point (x, y) lies on the piece whose stretch holds x, at x − x0. */
  frames?: { origin: number[]; right: number[]; up: number[]; dir: number[]; x0: number; x1: number }[];
}

/** The drawing of a view, fetched again (debounced) after every edit. */
export function useDrawing(viewId: string | null, enabled = true): DrawingDto | null {
  const nodes = useSession((s) => s.nodes);
  const [drawing, setDrawing] = useState<{ id: string; d: DrawingDto } | null>(null);
  useEffect(() => {
    if (!viewId || !enabled) { setDrawing(null); return; }
    let live = true;
    const t = setTimeout(() => {
      void query<DrawingDto>('view.drawing', { id: viewId }).then((d) => { if (live && d) setDrawing({ id: viewId, d }); });
    }, 120);
    return () => { live = false; clearTimeout(t); };
  }, [viewId, nodes, enabled]);
  // Another view's drawing is not this one's, even while this one is on its way.
  return drawing && drawing.id === viewId ? drawing.d : null;
}
