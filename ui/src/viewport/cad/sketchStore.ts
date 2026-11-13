/**
 * What the views know of sketch constraints: each shown view's constraints and solved status as the engine answers
 * (`studio.constraints`, `studio.sketchStatus`), the constraints selected, the live preview of a constrained drag and the views
 * whose constraints are hidden. Kept here, beside the CAD layer, so the drafting grips can ask whether a view is constrained
 * without Studio's code; Studio's overlay is what fills it.
 */

import { create } from 'zustand';
import { query } from '../../engine/session';
import { fromJson, type Shape } from './shapes';
import type { ConstraintDto, SketchStatus } from './constraints';

export interface SketchView { constraints: ConstraintDto[]; status: SketchStatus | null }
export interface DragPreview { viewId: string; ok: boolean; reason?: string; shapes: { id: string; shape: Shape }[] }

interface SketchState {
  views: Record<string, SketchView>;
  /** The 2D view a constraint overlay is showing (the chip speaks of it). */
  shown: string | null;
  selected: string[];
  /** The constraint whose value is being edited in place. */
  editing: string | null;
  preview: DragPreview | null;
  hidden: Record<string, boolean>;
}

const KEY = 'lapin.sketch.hidden';
const loadHidden = (): Record<string, boolean> => {
  try { const o = JSON.parse(localStorage.getItem(KEY) ?? '{}'); return o && typeof o === 'object' ? o : {}; } catch { return {}; }
};

export const useSketch = create<SketchState>(() => ({ views: {}, shown: null, selected: [], editing: null, preview: null, hidden: loadHidden() }));

/** Show or hide a view's constraints (remembered per view). */
export function setConstraintsHidden(viewId: string, hidden: boolean): void {
  const next = { ...useSketch.getState().hidden };
  if (hidden) next[viewId] = true; else delete next[viewId];
  useSketch.setState({ hidden: next });
  try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* a private window: not remembered */ }
}

/** Whether a view has constraints, as last read. */
export const viewHasConstraints = (viewId: string | undefined | null): boolean => !!viewId && (useSketch.getState().views[viewId]?.constraints.length ?? 0) > 0;

const asked = new Map<string, number>();
/** Asks the engine for a view's constraints and status again (the latest answer wins). */
export async function refreshSketch(viewId: string): Promise<void> {
  const n = (asked.get(viewId) ?? 0) + 1;
  asked.set(viewId, n);
  const [c, s] = await Promise.all([
    query<{ constraints: ConstraintDto[] }>('studio.constraints', { viewId }),
    query<SketchStatus>('studio.sketchStatus', { viewId }),
  ]);
  if (asked.get(viewId) !== n || !c) return;
  const constraints = c.constraints ?? [];
  useSketch.setState((st) => ({
    views: { ...st.views, [viewId]: { constraints, status: s ?? null } },
    selected: st.selected.filter((id) => constraints.some((x) => x.id === id)),
  }));
}

let previewing = 0;
/** What dragging a point would do, asked while the pointer moves (a slower answer than the latest is dropped). */
export async function previewDrag(viewId: string, shape: string, part: string, to: { x: number; y: number }): Promise<void> {
  const n = ++previewing;
  const r = await query<{ ok: boolean; reason?: string; shapes?: { id: string; shape: Record<string, unknown> }[] }>('studio.dragPreview', { viewId, shape, part, to: [to.x, to.y] });
  if (n !== previewing) return;
  if (!r) { useSketch.setState({ preview: null }); return; }
  useSketch.setState({ preview: { viewId, ok: r.ok, reason: r.reason, shapes: (r.shapes ?? []).flatMap((x) => { const s = fromJson(x.shape); return s ? [{ id: x.id, shape: s }] : []; }) } });
}
export function endPreview(): void { previewing++; if (useSketch.getState().preview) useSketch.setState({ preview: null }); }
