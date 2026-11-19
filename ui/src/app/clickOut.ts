/**
 * "A click outside an input box accepts it" (Preferences → Keyboard, forum 155; off by default).
 *
 * What happens without it: a value typed in the box by the cursor (length, angle, width, height) stays pending while
 * the mouse goes elsewhere; a left click in a plan or drawing takes the typed point (the click only gives the
 * direction), a click anywhere else leaves the value pending. Text on the command line stays pending too, and a click
 * in the view feeds its own point. Esc clears a typed live value, or cancels the command line's text with its command.
 * Properties fields commit on blur and put their text back on Esc, so they are not touched here.
 *
 * With it, while something is typed, outside the boxes and dialogs:
 *  - a left click accepts the typed value as Enter would, and the click is used up (it places no point of its own);
 *    in a plan or drawing the live value keeps its own rule (the click is the typed point's direction), so only the
 *    command line's text is taken there;
 *  - a right click throws the typed value away (only the value: the command goes on), the click used up. Esc is as ever.
 */

import { useCad, submit } from '../viewport/cad/session';
import { liveCommit, useLive } from '../viewport/liveInput';
import { usePrefs } from './prefs';
import { useUi } from './store';

/** What a pointer press is on, as far as this needs: `closest` of a DOM element. */
export interface PressTarget { closest?: (selector: string) => unknown }

/** The boxes themselves: the command line, the live bar and cells, any field, and dialogs and menus. */
const ON_BOX = '.cad-prompt, .cad-line, .live-bar, .live-box, input, textarea, select, [contenteditable="true"], [role="dialog"], [role="menu"]';
/** The views whose own click already takes a typed live value. */
const LIVE_VIEW = '.plan-svg';

export type ClickOutAction = 'submit' | 'live' | 'discard' | null;

/** What a press does: decided apart from the DOM events, so it can be tested. */
export function clickOutAction(button: number, target: PressTarget | null, o: { enabled: boolean; buffer: string; liveTyped: boolean; dialog: boolean }): ClickOutAction {
  if (!o.enabled || o.dialog || (button !== 0 && button !== 2)) return null;
  if (!o.buffer && !o.liveTyped) return null;
  if (target?.closest?.(ON_BOX)) return null;
  if (button === 2) return 'discard';
  if (o.buffer) return 'submit';
  return target?.closest?.(LIVE_VIEW) ? null : 'live';
}

/** Does it; true when the press was used up. */
export function applyClickOut(action: ClickOutAction): boolean {
  switch (action) {
    case 'submit': { const c = useCad.getState(); void submit(c.buffer); return true; }
    case 'live': return liveCommit();
    case 'discard': {
      const hadBuffer = !!useCad.getState().buffer;
      if (hadBuffer) useCad.setState({ buffer: '' });
      const l = useLive.getState();
      if (l.len || l.ang || l.rel) useLive.setState({ len: '', ang: '', rel: '' });
      return true;
    }
    default: return false;
  }
}

/** Listens for the presses (capture, so a view's own handler never sees one that was used up). Returns the remover. */
export function installClickOut(): () => void {
  const stop = (e: Event) => e.stopPropagation();
  const onDown = (e: PointerEvent) => {
    const l = useLive.getState();
    const action = clickOutAction(e.button, e.target as Element | null, {
      enabled: usePrefs.getState().clickOutAccepts, buffer: useCad.getState().buffer, liveTyped: !!(l.owner && (l.len || l.ang || l.rel)), dialog: !!useUi.getState().dialog,
    });
    if (!action || !applyClickOut(action)) return;
    e.stopPropagation();
    // The rest of this press (its release and click) is used up too.
    const kinds = ['pointerup', 'mouseup', 'click', 'auxclick'];
    const off = () => kinds.forEach((k) => window.removeEventListener(k, stop, true));
    kinds.forEach((k) => window.addEventListener(k, stop, true));
    window.addEventListener('pointerup', () => setTimeout(off, 0), { once: true });
    setTimeout(off, 1500);
  };
  window.addEventListener('pointerdown', onDown, true);
  return () => window.removeEventListener('pointerdown', onDown, true);
}
