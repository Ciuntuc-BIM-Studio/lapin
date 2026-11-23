/**
 * The tunnel lining configurator's state (docs/plan-wing-walls.md, S4): the engine's catalogue of parameters and presets
 * (`civil.tunnel`, the same shape as the portal configurator's, so its helpers serve it), a spec as the configurator holds it
 * (each number a value or a formula), and what the inspector shows of a lining.
 */

import { create } from 'zustand';
import type { PortalCatalog, PortalSpec } from './portal';
import { fullSpec } from './portal';

export const TUNNEL_PRESET_LABELS: Record<string, string> = {
  horseshoe: 'Horseshoe (NATM)', circular: 'Circular (TBM ring)', box: 'Cut-and-cover box', vault: 'Vault on walls',
};

export const TUNNEL_PRESET_HINTS: Record<string, string> = {
  horseshoe: 'A mined tunnel: crown, side, corner and invert arcs, tangent; an outer shell of shotcrete',
  circular: 'A bored tunnel: a ring of one thickness, in rings as wide as the machine builds them',
  box: 'A cut-and-cover box with haunches under its roof',
  vault: 'A vault on upright walls over a flat floor',
};

/** The configurator open: on a new lining, or on one made before (its id). */
export const useTunnelDialog = create<{ id: string | null; spec: PortalSpec }>(() => ({ id: null, spec: { preset: 'horseshoe' } }));

/** The spec whole, the horseshoe when it names no preset. */
export const fullTunnelSpec = (catalog: PortalCatalog, spec: PortalSpec) => fullSpec(catalog, spec, 'horseshoe');

/** Whether the lining is cut into a TBM's rings (its preset circular, no drawn section) rather than casting blocks. */
export function isRings(spec: PortalSpec): boolean {
  return spec.preset === 'circular' && !(typeof spec.profileId === 'string' && spec.profileId);
}

/**
 * Where a lining's axis came from: the element and its two stations, when it was taken from one; whether it follows that element
 * (linked) or is a copy of it (unlinked, or made before linings could follow), and what its link says of itself (a note).
 */
export function axisSourceOf(props: Record<string, unknown>): { id: string; from: number; to: number; linked: boolean; note: string | null } | null {
  const s = props.axisSource as { id?: unknown; from?: unknown; to?: unknown; linked?: unknown; note?: unknown } | undefined;
  return s && typeof s.id === 'string' && typeof s.from === 'number' && typeof s.to === 'number'
    ? { id: s.id, from: s.from, to: s.to, linked: s.linked === true, note: typeof s.note === 'string' ? s.note : null }
    : null;
}
