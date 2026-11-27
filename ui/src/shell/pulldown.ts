/**
 * A pulldown tool (decision 528; `ToolDef.items`: a script's `lapin.ui.menu`): its button opens the right-click menu's
 * list of its tools under it, each pressed as itself. Pressed from elsewhere (Commands…, a quick slot) the list opens
 * where the pointer last went down. Any other tool is just pressed.
 */

import { createElement } from 'react';
import { create } from 'zustand';
import type { ToolDef } from '../platform/module';
import { activate } from '../platform/modules';
import { openContextMenu } from './ContextMenu';

let pointer = { x: 240, y: 120 };
if (typeof window !== 'undefined') window.addEventListener('pointerdown', (e) => { pointer = { x: e.clientX, y: e.clientY }; }, true);

/** Last picked sub-tool per pulldown id (forum topic 296: selecting an item from a secondary tool dropdown makes it the active tool). */
export const usePulldownActive = create<Record<string, string>>(() => ({}));

/** Resolve the currently displayed sub-tool for a pulldown (active tool if in items, else last picked, else the pulldown itself). */
export function shownPulldownTool(tl: ToolDef, activeToolId: string | null, lastById: Record<string, string>): ToolDef {
  if (!tl.items?.length) return tl;
  const active = activeToolId ? tl.items.find((x) => x.id === activeToolId) : undefined;
  if (active) return active;
  const last = lastById[tl.id] ? tl.items.find((x) => x.id === lastById[tl.id]) : undefined;
  return last ?? tl;
}

/** A pulldown's list opened at a point of the window (by default where the pointer last went down). */
export function openPulldown(tl: ToolDef, at = pointer): void {
  openContextMenu(at.x, at.y, (tl.items ?? []).map((t) => ({
    label: t.label,
    icon: createElement(t.icon, { size: 14, strokeWidth: 1.6 }),
    onSelect: () => {
      usePulldownActive.setState({ [tl.id]: t.id });
      activate(t);
    },
  })));
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

/**
 * A module's pulldown (audit S14: a crowded tab's tools gathered under fewer buttons): a button of its own id whose list is
 * `items`, each still the tool it was (its id, letters, place in My tools and Commands…: platform/modules.ts `allTools`).
 */
export function pulldownTool(id: string, label: string, icon: ToolDef['icon'], items: readonly ToolDef[]): ToolDef {
  const t: ToolDef = { id, label, icon, items, run: () => openPulldown(t) };
  return t;
}
