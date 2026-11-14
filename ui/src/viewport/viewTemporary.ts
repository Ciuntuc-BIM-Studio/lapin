/**
 * Hide and isolate in plans, sections and elevations (forum topic 73, Revit's "Hide in view" and its temporary hide and
 * isolate): Hide in view is kept on the view (`view.hideElements`, one undo step; a level's default plan is saved as its
 * plan view then), the temporary state is not saved and not undone, kept here per view and sent with the drawing asked for
 * (`view.drawing` with `temp` and `reveal`), so the engine's one resolver still draws it. Reveal hidden draws what the view
 * hides in magenta, to pick it and show it again (Unhide in view). The 3D view keeps its own (temporary.ts).
 */

import { create } from 'zustand';
import { notify, select, useUi } from '../app/store';
import { activeStorey, execute, useSession } from '../engine/session';
import { shownSelection } from '../platform/groups';
import { planViewIdOf } from './storeyPlan';
import { hideSelection, isolateSelection, resetTemporary } from './temporary';

export interface ViewTemp { hidden: string[]; isolated: string[] | null; reveal: boolean }

export const useViewTemporary = create<{ byView: Record<string, ViewTemp> }>(() => ({ byView: {} }));

const NONE: ViewTemp = { hidden: [], isolated: null, reveal: false };

/** A view's temporary state (nothing hidden for now when it has none). */
export const tempOf = (viewId: string | null | undefined): ViewTemp => (viewId && useViewTemporary.getState().byView[viewId]) || NONE;

/** Whether a view hides or isolates anything for now, or reveals what it hides. */
export const tempOn = (t: ViewTemp): boolean => t.hidden.length > 0 || t.isolated !== null || t.reveal;

/** What the drawing of a view is asked with: its temporary hide and isolate, and Reveal hidden; nothing when it has none. */
export function tempArgs(t: ViewTemp): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  if (t.hidden.length || t.isolated) out.temp = { ...(t.hidden.length ? { hide: t.hidden } : {}), ...(t.isolated ? { isolate: t.isolated } : {}) };
  if (t.reveal) out.reveal = true;
  return out;
}

/** The plan, section or elevation in front (a level's default plan as `plan:<storey>`); null in 3D, on a sheet, elsewhere. */
export function front2dView(): string | null {
  const ui = useUi.getState();
  if (ui.view === 'drawing') return ui.drawingViewId ?? null;
  if (ui.view !== 'plan') return null;
  const storey = activeStorey();
  return storey ? planViewIdOf(useSession.getState().nodes, storey.id, ui.planViewId) : null;
}

function setTemp(viewId: string, change: (t: ViewTemp) => ViewTemp): void {
  useViewTemporary.setState((s) => {
    const next = change(s.byView[viewId] ?? NONE);
    const byView = { ...s.byView };
    if (tempOn(next)) byView[viewId] = next; else delete byView[viewId];
    return { byView };
  });
}

const selection = (): string[] => [...shownSelection(useUi.getState().selected)];

/** Hide for now in the view (HH there): the selection left out of its drawing until reset. */
export function hideTemporarily(viewId: string): void {
  const s = selection();
  if (!s.length) { notify('Select what to hide first.'); return; }
  setTemp(viewId, (t) => ({ ...t, hidden: [...new Set([...t.hidden, ...s])] }));
  select(null);
}

/** Isolate for now in the view (HI there): everything but the selection left out until reset. */
export function isolateTemporarily(viewId: string): void {
  const s = selection();
  if (!s.length) { notify('Select what to isolate first.'); return; }
  setTemp(viewId, (t) => ({ ...t, isolated: s }));
}

/** Reset temporary hide/isolate (HR there): the view as it is kept again. */
export function resetTemporarily(viewId: string): void {
  setTemp(viewId, (t) => ({ ...t, hidden: [], isolated: null }));
}

/** Reveal hidden on or off: what the view hides drawn in magenta, to pick and show again. */
export function toggleReveal(viewId: string): void {
  setTemp(viewId, (t) => ({ ...t, reveal: !t.reveal }));
}

/** Hide in view, kept with the project: the selection hidden in this view (one undo step). */
export async function hideInView(viewId: string): Promise<void> {
  const s = selection();
  if (!s.length) { notify('Select what to hide in the view first.'); return; }
  const r = await execute('view.hideElements', { viewId, ids: s }) as { id?: string } | null;
  // A level's default plan was saved as its plan view to keep them: it is the plan shown now.
  if (r?.id && r.id !== viewId && useUi.getState().view === 'plan') useUi.setState({ planViewId: r.id });
  select(null);
}

/** Unhide in view: the selection shown again (with Reveal hidden on, the hidden ones can be picked). */
export async function unhideInView(viewId: string, all = false): Promise<void> {
  const s = selection();
  if (!all && !s.length) { notify('Select what to show again first (Reveal hidden shows what the view hides).'); return; }
  await execute('view.unhideElements', all ? { viewId, all: true } : { viewId, ids: s });
}

/** What the view keeps hidden (its own `hiddenElements`): none for a level's default plan. */
export function hiddenInView(viewId: string | null): string[] {
  const v = viewId ? useSession.getState().nodes.get(viewId)?.props.hiddenElements : undefined;
  return Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : [];
}

/** HH, HI, HR: in a plan, section or elevation in front, its own temporary state; in 3D, the 3D view's (temporary.ts). */
export const hideHere = (): void => { const v = front2dView(); if (v) hideTemporarily(v); else hideSelection(); };
export const isolateHere = (): void => { const v = front2dView(); if (v) isolateTemporarily(v); else isolateSelection(); };
export const resetHere = (): void => { const v = front2dView(); if (v) resetTemporarily(v); else resetTemporary(); };
