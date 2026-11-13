/**
 * The type configurators' preview (decision 529): what it asks the engine (`types.preview`), the settings it keeps on this
 * computer for every configurator (scale, level of detail, the sample's sizes, the pane shown when narrow), and how a
 * drawing is fitted into a pane. The drawing itself is the engine's, through the views' pipeline; nothing here draws a type.
 */

import { create } from 'zustand';
import type { DrawingDto } from '../../viewport/drawing';

export type PreviewDetail = 'coarse' | 'medium' | 'fine';
export type PreviewPane = 'plan' | 'section' | '3d';

export const PREVIEW_SCALES = [20, 50, 100] as const;
export const PREVIEW_DETAILS: PreviewDetail[] = ['coarse', 'medium', 'fine'];
export const PREVIEW_PANES: PreviewPane[] = ['plan', 'section', '3d'];

export interface PreviewSettings {
  scale: number;
  detail: PreviewDetail;
  /** The sample's length (m). */
  length: number;
  /** The sample's height (m); null: as a new element of the type takes it (a wall: its type's top, the storey above). */
  height: number | null;
  /** The pane shown when the dialog is too narrow for the three side by side. */
  pane: PreviewPane;
}

export const PREVIEW_DEFAULTS: PreviewSettings = { scale: 50, detail: 'fine', length: 3, height: null, pane: 'plan' };

export const useTypePreview = create<PreviewSettings>(() => ({ ...PREVIEW_DEFAULTS }));

/** A drawing of the preview: the view's, with each part's cut areas by its key (`layer:0`…), x y x y … per loop. */
export interface PreviewDrawing extends DrawingDto { parts?: Record<string, number[][]> }

export interface PreviewMeshPart { element: 'main' | 'other'; key: string | null; name: string; color: string; material?: string | null; positions: number[]; normals: number[]; indices: number[]; edges: number[] }

export interface TypePreviewDto { problem?: string; plan?: PreviewDrawing; section?: PreviewDrawing; mesh?: PreviewMeshPart[]; partKeys?: string[]; ms?: number; cached?: boolean }

/** The storeys' and the section's names as the sample's drawings write them, in the interface's language. */
export interface PreviewNames { storey: string; above: string; section: string }

/** What `types.preview` is asked for the type being edited (its create command's arguments, unsaved; `id` when it is a saved one). */
export function previewArgs(typeClass: string, type: Record<string, unknown>, s: PreviewSettings, names: PreviewNames) {
  const sample: Record<string, unknown> = { length: s.length, names };
  if (s.height != null && s.height > 0) sample.height = s.height;
  return { class: typeClass, type, sample, views: PREVIEW_PANES, scale: s.scale, detail: s.detail };
}

/** The part key of a type's layer (its row in the table, from 0). */
export const layerKey = (i: number) => `layer:${i}`;

/** Whether a part is the layer of row <code>i</code> (none lit when nothing is hovered). */
export function keyMatches(key: string | null | undefined, i: number | null | undefined): boolean {
  if (key == null || i == null || i < 0) return false;
  const k = layerKey(i);
  return key === k || key.startsWith(`${k}:`);
}

/** The panes shown: the three side by side when the dialog is wide enough, else the one picked (tabs). */
export function shownPanes(width: number, picked: PreviewPane): PreviewPane[] {
  return width >= 640 ? PREVIEW_PANES : [picked];
}

export interface Fitted { k: number; toScreen: (p: { x: number; y: number }) => { x: number; y: number } }

/**
 * A drawing's bounds (x0 y0 x1 y1, metres, y up) laid into a pane w × h px with a margin, the same scale both ways and
 * centred; y turned down as the screen has it.
 */
export function fitBounds(bounds: readonly number[] | undefined, w: number, h: number, margin = 14): Fitted {
  const [x0, y0, x1, y1] = bounds && bounds.length === 4 ? bounds : [0, 0, 1, 1];
  const bw = Math.max(1e-6, x1 - x0), bh = Math.max(1e-6, y1 - y0);
  const k = Math.max(1e-6, Math.min((w - 2 * margin) / bw, (h - 2 * margin) / bh));
  const ox = (w - bw * k) / 2, oy = (h - bh * k) / 2;
  return { k, toScreen: (p) => ({ x: ox + (p.x - x0) * k, y: oy + (y1 - p.y) * k }) };
}
