/**
 * A view placed again at another scale beside itself on its sheet (the forum's topic #31, decision 547): the engine's
 * `sheet.placeAtScale` duplicates the view with its annotations, sets the scale, crops it to the part drawn on the viewport
 * (or the viewport as it is cropped) and places it next to the original, one undo step. Offered on a sheet's viewport
 * (right-click, then drag the part on it) and on a view in the project browser that is on a sheet (its crop as it is).
 */

import type { NodeDto } from '../engine/protocol';
import { scaleOptions } from './viewScale';

/** The scales offered to place a view again at: the project's standard ones but its own. */
export function otherScales(choices: readonly number[], current: number): number[] {
  return scaleOptions(choices, current).filter((s) => s !== current);
}

/** The menu's label for a scale. */
export const placeAgainLabel = (scale: number): string => `Place again at 1:${scale}…`;

/** The sheet a view is on (its first viewport's), or null. */
export function sheetOfView(nodes: Map<string, NodeDto>, viewId: string): NodeDto | null {
  for (const n of nodes.values()) {
    if (n.type !== 'LapinSheet' || !Array.isArray(n.props.viewports)) continue;
    if ((n.props.viewports as { viewId?: string }[]).some((v) => v?.viewId === viewId)) return n;
  }
  return null;
}

/** A rectangle dragged on the paper (mm) as the command's part, or none when it is only a click (the viewport as cropped). */
export function partOf(a: { x: number; y: number }, b: { x: number; y: number }): { x0: number; y0: number; x1: number; y1: number } | null {
  if (Math.abs(a.x - b.x) < 1 || Math.abs(a.y - b.y) < 1) return null;
  return { x0: Math.min(a.x, b.x), y0: Math.min(a.y, b.y), x1: Math.max(a.x, b.x), y1: Math.max(a.y, b.y) };
}
