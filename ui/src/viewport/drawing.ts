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
  texts?: { x: number; y: number; text: string; height: number; angle: number; anchor: string; baseline: string; bold: boolean; font: string }[];
  /** Pictures (an orthophoto): a LapinImage's id, its corners (top left, top right, bottom right, bottom left: x y …), its opacity. */
  images?: { id: string; corners: number[]; opacity: number }[];
  /** On a sheet: the viewport the pen draws for, so the view's pens move alone while it is dragged. */
  viewport?: string;
  /** The box the pen is clipped to: x0 y0 x1 y1 (a sheet's viewport). */
  clip?: number[];
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
  marks?: { id: string; segments: number[]; snaps?: number[]; data?: Record<string, unknown> }[];
  problems?: string[];
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
