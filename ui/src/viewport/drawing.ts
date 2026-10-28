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
