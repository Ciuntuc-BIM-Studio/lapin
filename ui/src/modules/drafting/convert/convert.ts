/** The engine's Convert drafting to… (docs/plan-user-extensibility.md §7.4–7.5): the converters of a view, the preview, the conversion; and what the dialogs open with. */

import { create } from 'zustand';
import { notify as notifyText, useUi } from '../../../app/store';
import { engine, execute, useSession } from '../../../engine/session';
import type { ConvOption } from '../../core/tables/tableModel';

export interface Converter { id: string; label: string; module: string; takes: string; views: string[]; description: string; options: ConvOption[] }
export interface DraftLayer { layer: string; shapes: number }
export interface ConverterList { converters: Converter[]; layers: DraftLayer[] }
export interface ShapePreview { id: string; converter: string; error: string | null; steps: string[] }
export interface ConvertPreview { shapes: ShapePreview[]; ok: number; left: number; steps: number }
export interface ConvertOutcome { ids: string[]; converted: number; left: number }

export type ConvertSelection = { ids: string[] } | { layers: string[] } | { map: { layer: string; converter: string; options: Record<string, unknown> }[] };

async function ask<T>(name: string, args: Record<string, unknown>): Promise<{ value: T | null; error: string | null }> {
  try { return { value: await engine.call<T>('doc.query', { name, args }), error: null }; } catch (e) { return { value: null, error: e instanceof Error ? e.message : String(e) }; }
}

export const listConverters = (viewId: string) => ask<ConverterList>('drafting.converters', { viewId });
export const previewConversion = (viewId: string, what: ConvertSelection & { converter?: string; options?: Record<string, unknown> }) => ask<ConvertPreview>('drafting.convertPreview', { viewId, ...what });
export async function convertDrafting(viewId: string, what: ConvertSelection & { converter?: string; options?: Record<string, unknown> }, keep: boolean): Promise<ConvertOutcome | { error: string }> {
  const r = await execute('drafting.convert', { viewId, ...what, keep }) as ConvertOutcome | null;
  return r ?? { error: useSession.getState().error ?? 'Nothing was converted.' };
}

/** What the dialogs convert: the view and, for Convert to…, the shapes chosen. */
export const useConvert = create<{ viewId: string | null; ids: string[] }>(() => ({ viewId: null, ids: [] }));

export function openConvert(viewId: string, ids: string[]): void { useConvert.setState({ viewId, ids }); useUi.setState({ dialog: 'convertDrafting' }); }
export function openConvertLayers(viewId: string): void { useConvert.setState({ viewId, ids: [] }); useUi.setState({ dialog: 'convertLayers' }); }

/** Drafted shapes (lines, polylines, arcs, circles) among the entities: what Convert to… takes. */
export const convertible = (ids: string[], nodes: Map<string, { type: string; props: Record<string, unknown> }>): string[] =>
  ids.filter((id) => { const n = nodes.get(id); return !!n && n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string' && !!n.props.shape && n.props.predefinedType !== 'SYMBOL'; });

/** Convert to… on the selection in the view in front. */
export async function convertSelection(): Promise<void> {
  const { frontView } = await import('../../../viewport/frontView');
  const viewId = await frontView();
  if (!viewId || viewId === '3d') { notifyText('Open a plan, a section or an elevation to convert its drafting.'); return; }
  const ids = convertible(useUi.getState().selected, useSession.getState().nodes);
  if (!ids.length) { notifyText('Select drafted lines, polylines, arcs or circles to convert.'); return; }
  openConvert(viewId, ids);
}

/** Convert layers… in the view in front. */
export async function convertLayersInFront(): Promise<void> {
  const { frontView } = await import('../../../viewport/frontView');
  const viewId = await frontView();
  if (!viewId || viewId === '3d') { notifyText('Open a plan, a section or an elevation to convert its drafting.'); return; }
  openConvertLayers(viewId);
}


/** What a conversion made, in words. */
export const convertedMessage = (r: ConvertOutcome): string =>
  r.left ? `${r.ids.length} elements made from ${r.converted} shapes, ${r.left} left as drawn.` : `${r.ids.length} elements made from ${r.converted} shapes.`;
