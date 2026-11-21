/**
 * Drag without holding the mouse button (issue 86). Once a drag has begun (the body of the selection dragged, a grip), Tab
 * switches on AUTOHOLD: the drag goes on as if the button were still held, following the pointer, even after the button is
 * let go. A left click then ends the drag where the pointer and its snap are; Tab again goes through the live input box's
 * fields (length, angle, the angle to the reference) so exact values can be typed with the pointer detached, the preview
 * following the numbers; Enter, or a left click outside the boxes, accepts.
 *
 * The rule for Tab (it collides with the box's own Tab, which goes to the next field in every drawing and typing mode):
 *   - a drag is on and autohold is off: Tab = autohold on (the box's fields are not cycled yet);
 *   - autohold is on, or anything else drives the box (a tool drawing a segment, a measure): Tab = the next field.
 * Autohold lasts as long as the box belongs to the drag; when the drag ends (dropped, cancelled with Esc, its view gone) it
 * is switched off by itself. The drags that can be held are those that own the live box as "drag" (the selection's body)
 * or "grip".
 */

import { create } from 'zustand';
import { liveEnd, useLive } from '../viewport/liveInput';
import { notify, useUi } from './store';

/** The live box's owners that are drags (the others are tools drawing a segment). */
export const DRAG_OWNERS: readonly string[] = ['drag', 'grip'];
export const isDragOwner = (owner: string | null | undefined): boolean => !!owner && DRAG_OWNERS.includes(owner);

export type TabMeaning = 'autohold' | 'field';

/** What Tab means while the box belongs to `owner`: autohold on at the start of a drag, then the next field. */
export function tabMeaning(owner: string | null | undefined, autohold: boolean): TabMeaning {
  return isDragOwner(owner) && !autohold ? 'autohold' : 'field';
}

/** Whether letting go of the button ends a drag: not while autohold carries it. */
export const releaseEnds = (autohold: boolean): boolean => !autohold;

/** Whether a left click ends a held drag: only the autohold one (an ordinary drag was already ended by its release). */
export const clickEnds = (autohold: boolean): boolean => autohold;

/** What Enter does in a drag: with something typed it puts the drag at the typed point; with nothing typed, in autohold, at the pointer. */
export function enterEffect(autohold: boolean, typed: boolean): 'typed' | 'pointer' | 'none' {
  if (typed) return 'typed';
  return autohold ? 'pointer' : 'none';
}

export const useAutohold = create<{ on: boolean }>(() => ({ on: false }));
export const autoholdOn = (): boolean => useAutohold.getState().on;
export const autoholdSet = (on: boolean): void => {
  if (useAutohold.getState().on === on) return;
  useAutohold.setState({ on });
  if (!on) noticeClear();
};

/** The toast that says what Tab did: it goes when autohold ends, or after NOTICE_MS at the latest. */
export const AUTOHOLD_NOTICE = 'Autohold: the drag follows the pointer. Click to drop it, Tab for the next field, Enter to accept';
export const NOTICE_MS = 2000;
let noticeTimer: ReturnType<typeof setTimeout> | null = null;
function noticeClear(): void {
  if (noticeTimer) { clearTimeout(noticeTimer); noticeTimer = null; }
  if (useUi.getState().notice === AUTOHOLD_NOTICE) notify(null);
}
function noticeShow(): void {
  notify(AUTOHOLD_NOTICE);
  if (noticeTimer) clearTimeout(noticeTimer);
  noticeTimer = setTimeout(noticeClear, NOTICE_MS);
}

/** What ends a held drag in each view that owns one (the selection's body, a grip): put back where it was, no preview left. */
const cancellers = new Set<() => void>();
export function onAutoholdCancel(fn: () => void): () => void { cancellers.add(fn); return () => { cancellers.delete(fn); }; }

/**
 * Esc during autohold: the drag is cancelled (the element or grip stays where it was), the live box closes, autohold is off.
 * True when it was on (the Esc is spent), whether the focus is on the page or in one of the box's fields.
 */
export function autoholdCancel(): boolean {
  if (!autoholdOn()) return false;
  for (const fn of [...cancellers]) fn();
  liveEnd();
  autoholdSet(false);
  return true;
}

// Autohold belongs to the drag that owns the box: when the box goes (the drag was dropped, cancelled) or changes hands, it ends.
useLive.subscribe((cur, prev) => {
  if (useAutohold.getState().on && (cur.owner !== prev.owner || !isDragOwner(cur.owner))) autoholdSet(false);
});

/** Tab during a drag: autohold goes on (true) when the drag was not held yet; false: Tab is the box's. */
export function autoholdTab(): boolean {
  const owner = useLive.getState().owner;
  if (tabMeaning(owner, autoholdOn()) !== 'autohold') return false;
  autoholdSet(true);
  noticeShow();
  return true;
}
