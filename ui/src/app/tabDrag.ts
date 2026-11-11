/**
 * Dragging a view's tab (decision 445), as in Visual Studio or a browser: out of the window (or onto its edge) it
 * opens in a window of its own where it is dropped (desktop); onto another pane it opens there; onto the right or
 * the bottom quarter of a view it splits the view and opens beside or under it. While it is dragged, the tab's name
 * follows the pointer and the place it would go is lit. A plain click still just opens the tab.
 */

import { create } from 'zustand';
import { closeTab, openTab, splitPane, tabKey, useUi, type ViewTab } from './store';
import { openViewWindow } from './windows';
import { isDesktop } from '../engine/files';
import { notify } from './store';

export type DropKind = 'here' | 'right' | 'below' | 'window';
export interface Drop { kind: DropKind; pane: number; rect: { left: number; top: number; width: number; height: number } }

export const useTabDrag = create<{ label: string | null; at: { x: number; y: number } | null; drop: Drop | null }>(() => ({ label: null, at: null, drop: null }));

/** Within this many pixels of the window's edge counts as out of it (a window can be maximised: no outside to drop on). */
export const EDGE = 14;

/** Where a tab dropped at a client point goes: the window's edge or outside it, a pane, a pane's right or bottom quarter. */
export function dropAt(x: number, y: number, size: { w: number; h: number }, panes: { index: number; rect: Drop['rect'] }[], count: number): Drop | null {
  if (x <= EDGE || y <= EDGE || x >= size.w - EDGE || y >= size.h - EDGE) return { kind: 'window', pane: -1, rect: { left: 0, top: 0, width: size.w, height: size.h } };
  const p = panes.find((q) => x >= q.rect.left && x <= q.rect.left + q.rect.width && y >= q.rect.top && y <= q.rect.top + q.rect.height);
  if (!p) return null;
  const r = p.rect;
  if (count < 2) {
    if (x > r.left + r.width * 0.75) return { kind: 'right', pane: p.index, rect: { left: r.left + r.width / 2, top: r.top, width: r.width / 2, height: r.height } };
    if (y > r.top + r.height * 0.75) return { kind: 'below', pane: p.index, rect: { left: r.left, top: r.top + r.height / 2, width: r.width, height: r.height / 2 } };
  }
  return { kind: 'here', pane: p.index, rect: r };
}

function panesOnScreen(): { index: number; rect: Drop['rect'] }[] {
  return [...document.querySelectorAll<HTMLElement>('[data-testid^="pane-"]')].map((el) => {
    const r = el.getBoundingClientRect();
    return { index: Number(el.dataset.testid!.slice(5)), rect: { left: r.left, top: r.top, width: r.width, height: r.height } };
  }).filter((p) => Number.isFinite(p.index));
}

/** A tab in a window of its own, the main window keeping its other tabs. */
export async function tabToWindow(tab: ViewTab, label: string, at?: { x: number; y: number }): Promise<void> {
  if (!isDesktop()) { notify('A view in a window of its own needs the desktop app.'); return; }
  const { storeyId: _s, ...pane } = tab;
  await openViewWindow(pane, label, at);
  if (useUi.getState().viewTabs.length > 1) closeTab(tabKey(tab));
}

/** Start following a pointer pressed on a tab; returns whether the press became a drag (then the click is not a click). */
export function beginTabDrag(e: React.PointerEvent<HTMLElement>, tab: ViewTab, label: string): { dragged: () => boolean } {
  const start = { x: e.clientX, y: e.clientY };
  let on = false;
  const el = e.currentTarget;
  const id = e.pointerId;
  try { el.setPointerCapture(id); } catch { /* not every element takes it */ }
  const move = (ev: PointerEvent) => {
    if (!on && Math.hypot(ev.clientX - start.x, ev.clientY - start.y) < 8) return;
    on = true;
    const drop = dropAt(ev.clientX, ev.clientY, { w: window.innerWidth, h: window.innerHeight }, panesOnScreen(), useUi.getState().panes.length);
    useTabDrag.setState({ label, at: { x: ev.clientX, y: ev.clientY }, drop: drop?.kind === 'window' && !isDesktop() ? null : drop });
  };
  const up = (ev: PointerEvent) => {
    el.removeEventListener('pointermove', move);
    el.removeEventListener('pointerup', up);
    el.removeEventListener('pointercancel', cancel);
    try { el.releasePointerCapture(id); } catch { /* released already */ }
    const drop = useTabDrag.getState().drop;
    useTabDrag.setState({ label: null, at: null, drop: null });
    if (!on || !drop) return;
    if (drop.kind === 'window') { void tabToWindow(tab, label, { x: ev.screenX, y: ev.screenY }); return; }
    if (drop.kind === 'right' || drop.kind === 'below') { splitPane(drop.kind === 'right' ? 'row' : 'column'); openTab(1, tab); return; }
    openTab(drop.pane, tab);
  };
  const cancel = () => {
    el.removeEventListener('pointermove', move);
    el.removeEventListener('pointerup', up);
    el.removeEventListener('pointercancel', cancel);
    useTabDrag.setState({ label: null, at: null, drop: null });
  };
  el.addEventListener('pointermove', move);
  el.addEventListener('pointerup', up);
  el.addEventListener('pointercancel', cancel);
  return { dragged: () => on };
}

/** The tab's name at the pointer and the place it would go, lit. */
export const DROP_LABELS: Record<DropKind, string> = { here: 'Open here', right: 'Split: beside', below: 'Split: below', window: 'New window' };
