/**
 * Which layers of layered elements a 3D view shows (docs/view-styles.md):
 * every layer, all but the finishes, or the cores only, as for plans and
 * sections. A 3D view takes its style's (the one it names, else its
 * discipline's: a Structural 3D view shows the cores alone) and may set its
 * own (`styleOverrides.layers`, view.setStyle); {3D} keeps these on the
 * project (`view3d`), a saved 3D view on itself. The engine marks each render
 * part with the modes that leave it out (`hiddenBy`), so the 3D drops those
 * parts: the kept layers are closed solids of their own. The rule mirrors
 * ViewStyles.Of in the engine.
 */

import type { NodeDto, RenderItem } from '../engine/protocol';
import { view3dProps } from './storeys3d';

export type LayerMode = 'all' | 'noFinishes' | 'core';

const MODES: readonly LayerMode[] = ['all', 'noFinishes', 'core'];
const isMode = (v: unknown): v is LayerMode => MODES.includes(v as LayerMode);

interface StyleLayers { id: string; discipline: string | null; layers: LayerMode }

/** The styles a project without its own reads as (ViewStyles.Defaults). */
const DEFAULTS: StyleLayers[] = [
  { id: 'architectural', discipline: 'Architecture', layers: 'all' },
  { id: 'structural', discipline: 'Structure', layers: 'core' },
  { id: 'mep', discipline: 'MEP', layers: 'all' },
];

function stylesOf(nodes: Map<string, NodeDto>): StyleLayers[] {
  let own: unknown;
  for (const n of nodes.values()) if (n.type === 'IfcProject') { own = n.props.viewStyles; break; }
  if (!Array.isArray(own) || !own.length) return DEFAULTS;
  const list = own.filter((s): s is Record<string, unknown> => !!s && typeof s === 'object' && typeof s.id === 'string' && s.id.length > 0)
    .map((s) => ({ id: s.id as string, discipline: typeof s.discipline === 'string' ? s.discipline : null, layers: isMode(s.layers) ? s.layers : 'all' as LayerMode }));
  return list.length ? list : DEFAULTS;
}

export interface Layers3d {
  /** What the view shows. */
  mode: LayerMode;
  /** What its style says. */
  style: LayerMode;
  /** The view sets its own. */
  own: boolean;
}

/** A 3D view's layers ("3d" or null: {3D}). */
export function layers3dOf(nodes: Map<string, NodeDto>, viewId: string | null): Layers3d {
  const props = view3dProps(nodes, viewId) ?? {};
  const styles = stylesOf(nodes);
  const discipline = typeof props.discipline === 'string' && props.discipline.trim() ? props.discipline.trim() : 'Architecture';
  const style = (typeof props.viewStyle === 'string' ? styles.find((s) => s.id === props.viewStyle) : undefined)
    ?? styles.find((s) => (s.discipline ?? '').toLowerCase() === discipline.toLowerCase()) ?? styles[0];
  const over = (props.styleOverrides as Record<string, unknown> | undefined)?.layers;
  return isMode(over) ? { mode: over, style: style.layers, own: true } : { mode: style.layers, style: style.layers, own: false };
}

/** What the 3D's chip says when not every layer shows; null: every layer does. */
export function layersChip(l: Layers3d): string | null {
  return l.mode === 'noFinishes' ? 'without finishes' : l.mode === 'core' ? 'the core only' : null;
}

/** The patch that brings a 3D view back to every layer: its style's when that is every layer, else its own. */
export function everyLayerPatch(l: Layers3d): { layers: LayerMode | null } {
  return { layers: l.style === 'all' ? null : 'all' };
}

const kept = new WeakMap<RenderItem, Map<LayerMode, RenderItem>>();

/** An element's render as a 3D view in this mode shows it: without the parts the mode leaves out (the same item when none go). */
export function shownIn(item: RenderItem, mode: LayerMode): RenderItem {
  if (mode === 'all' || !item.parts?.some((p) => p.hiddenBy?.includes(mode))) return item;
  let byMode = kept.get(item);
  if (!byMode) kept.set(item, byMode = new Map());
  let shown = byMode.get(mode);
  if (!shown) byMode.set(mode, shown = { ...item, parts: item.parts.filter((p) => !p.hiddenBy?.includes(mode)) });
  return shown;
}
