/**
 * A bar's anchor grips (decision 226), as the engine drew them in the mark's data (`anchors: [{key, at}]`): orange
 * grips standing the cover outside the bar's origin and its ends, for the formwork's face. Dragged onto the element's
 * cut (its corner, its face), `rebar.grip` lands the bar the cover inside.
 */

import type { Grip, GripStep, Vec2 } from '../../platform/module';
import { pt } from '../../viewport/gripKit';

export interface AnchorData { key: string; at: number[] }

export const ANCHOR_TITLE = 'Drag onto the formwork face: the bar stops at the cover inside';

/** The anchors as grips of kind `anchor`; `show` maps a point through an inset view, `step` makes the drop's command. */
export function anchorGrips(anchors: AnchorData[] | undefined, step: (key: string) => (to: Vec2) => GripStep | null, show: (p: Vec2) => Vec2 = (p) => p): Grip[] {
  const grips: Grip[] = [];
  for (const a of anchors ?? []) {
    if (typeof a.key !== 'string' || !a.key.startsWith('anchor:')) continue;
    const p = pt(a.at);
    if (p) grips.push({ key: a.key, kind: 'anchor', at: show(p), title: ANCHOR_TITLE, step: step(a.key) });
  }
  return grips;
}
