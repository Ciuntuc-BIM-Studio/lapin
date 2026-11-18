/**
 * Object Snap Override (forum topic 154, as in AutoCAD): Ctrl + right click while a tool or command asks for a point opens a menu of the
 * snap kinds; the one chosen is the only snap the NEXT point uses ('none': no object snap for it). After that point, or on Esc, the
 * running snaps (the SNAP menu's) return. The state is `snapOverride` in the CAD store; the aim reads it (aim.ts), the overlay shows it.
 */

import { SNAP_MODES } from '../../app/prefs';
import { useUi } from '../../app/store';
import { openContextMenu, type MenuItem } from '../../shell/ContextMenu';
import { SNAP_LABELS, openSnapMenu } from '../../shell/CursorCoords';
import { useCad } from './session';
import { type SnapOverride } from './snaps';

export const overrideLabel = (o: SnapOverride): string => (o === 'none' ? 'No snap (for this point)' : SNAP_LABELS[o]);

export type { SnapOverride };
export const armSnapOverride = (o: SnapOverride | null): void => useCad.setState({ snapOverride: o });
export const clearSnapOverride = (): void => { if (useCad.getState().snapOverride) useCad.setState({ snapOverride: null }); };

/** Whether a point is being asked for: a command's step or a tool running. */
export const askingForPoint = (): boolean => { const s = useCad.getState().step; return s ? s.want === 'point' : !!useUi.getState().tool; };

/** Whether a pointer press is the override's gesture: Ctrl (or Cmd) + right button; on macOS Ctrl + click is a right click already. */
export function isOverrideGesture(e: { button: number; ctrlKey: boolean; metaKey: boolean }, mac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform)): boolean {
  return (e.button === 2 && (e.ctrlKey || e.metaKey)) || (mac && e.button === 0 && e.ctrlKey);
}

export function openSnapOverrideMenu(x: number, y: number): void {
  const cur = useCad.getState().snapOverride;
  const items: MenuItem[] = [
    ...SNAP_MODES.map((m) => ({ label: SNAP_LABELS[m], checked: cur === m, onSelect: () => armSnapOverride(m) })),
    { label: overrideLabel('none'), checked: cur === 'none', onSelect: () => armSnapOverride('none') },
    { label: 'Snap settings…', onSelect: () => openSnapMenu(x, y) },
  ];
  openContextMenu(x, Math.max(8, y - items.length * 14), items);
}

/** A pointer press in a 2D view: true when it was the override's gesture (the menu is open; the press does nothing else). Ctrl + right click is intercepted before the right click's own meaning (finish a step, the context menu). */
export function overridePress(e: { button: number; ctrlKey: boolean; metaKey: boolean; clientX: number; clientY: number; preventDefault: () => void }): boolean {
  if (!isOverrideGesture(e) || !askingForPoint()) return false;
  e.preventDefault();
  openSnapOverrideMenu(e.clientX, e.clientY);
  return true;
}

/** A left click placed a point in a view (a tool's, which has no step to clear it): the override is spent once the point has been aimed. */
export function spendOverride(e: { button: number }): void {
  if (e.button === 0 && useCad.getState().snapOverride) setTimeout(clearSnapOverride, 0);
}
