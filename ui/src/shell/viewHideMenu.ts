/**
 * What a right-click on a plan, a section or an elevation offers to hide and find things (forum topic 73): Hide in view
 * (kept), Hide and Isolate for now (HH, HI), Reset temporary hide/isolate (HR), Reveal hidden and, with it, Unhide in view;
 * and Quick select… (the dialog choosing elements by class, type, storey, material or a property). Its own entries, put
 * after the selection's, so the menu's other providers stay as they are.
 */

import { Eye, EyeOff, Focus, ListFilter, RotateCcw, ScanEye } from 'lucide-react';
import { createElement } from 'react';
import { useUi } from '../app/store';
import { hiddenInView, hideInView, hideTemporarily, isolateTemporarily, resetTemporarily, tempOf, toggleReveal, unhideInView } from '../viewport/viewTemporary';
import type { MenuItem } from './ContextMenu';

const icon = (c: typeof Eye) => createElement(c, { size: 13 });

/** The entries for the 2D view `viewId` (none without one). */
export function viewHideItems(viewId: string | null): MenuItem[] {
  if (!viewId) return [];
  const { selected } = useUi.getState();
  const t = tempOf(viewId);
  const items: MenuItem[] = [];
  if (selected.length) {
    items.push(
      { label: 'Hide in view', icon: icon(EyeOff), onSelect: () => void hideInView(viewId) },
      { label: 'Hide for now', icon: icon(EyeOff), shortcut: 'HH', onSelect: () => hideTemporarily(viewId) },
      { label: 'Isolate for now', icon: icon(Focus), shortcut: 'HI', onSelect: () => isolateTemporarily(viewId) },
    );
  }
  if (t.hidden.length || t.isolated) items.push({ label: 'Reset temporary hide/isolate', icon: icon(RotateCcw), shortcut: 'HR', onSelect: () => resetTemporarily(viewId) });
  const kept = hiddenInView(viewId);
  if (kept.length || t.reveal) items.push({ label: 'Reveal hidden', icon: icon(ScanEye), checked: t.reveal, onSelect: () => toggleReveal(viewId) });
  if (t.reveal && selected.some((id) => kept.includes(id))) items.push({ label: 'Unhide in view', icon: icon(Eye), onSelect: () => void unhideInView(viewId) });
  items.push({ label: 'Quick select…', icon: icon(ListFilter), onSelect: () => useUi.setState({ dialog: 'quickSelect' }) });
  return items;
}
