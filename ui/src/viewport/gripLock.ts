/**
 * An end grip kept on its element's own line (decision 865, docs/plan-grip-angle-lock.md): what a drag asks of the aim.
 */

import type { Grip } from '../platform/module';

/**
 * The line a dragged grip may be kept on, and whether it is kept now (Ctrl/⌘ held): null for a grip with no line, a copy (Ctrl/⌘ on a
 * move grip) or an inserted point. Alt (one element only) does not change it.
 */
export function ownGuideOf(grip: Grip | undefined, o: { ctrl: boolean; alt: boolean; copy: boolean }): { from: { x: number; y: number }; dir: { x: number; y: number }; locked: boolean } | null {
  if (!grip?.along || o.copy) return null;
  return { ...grip.along, locked: o.ctrl };
}
