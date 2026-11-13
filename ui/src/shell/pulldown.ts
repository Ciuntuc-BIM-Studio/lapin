/**
 * A pulldown tool (decision 528; `ToolDef.items`: a script's `lapin.ui.menu`): its button opens the right-click menu's
 * list of its tools under it, each pressed as itself. Pressed from elsewhere (Commands…, a quick slot) the list opens
 * where the pointer last went down. Any other tool is just pressed.
 */

import { createElement } from 'react';
import type { ToolDef } from '../platform/module';
import { activate } from '../platform/modules';
import { openContextMenu } from './ContextMenu';

let pointer = { x: 240, y: 120 };
if (typeof window !== 'undefined') window.addEventListener('pointerdown', (e) => { pointer = { x: e.clientX, y: e.clientY }; }, true);

/** A pulldown's list opened at a point of the window (by default where the pointer last went down). */
export function openPulldown(tl: ToolDef, at = pointer): void {
  openContextMenu(at.x, at.y, (tl.items ?? []).map((t) => ({ label: t.label, icon: createElement(t.icon, { size: 14, strokeWidth: 1.6 }), onSelect: () => activate(t) })));
}

/** A tool's button pressed: a pulldown opens its list under the button, any other tool is pressed. */
export function pressTool(tl: ToolDef, button?: HTMLElement | null): void {
  if (tl.items?.length) {
    const r = button?.getBoundingClientRect();
    openPulldown(tl, r ? { x: r.left, y: r.bottom + 2 } : undefined);
    return;
  }
  activate(tl);
}
