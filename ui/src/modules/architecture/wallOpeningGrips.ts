/**
 * A bare wall opening's grips (a tester's report, decision 312), as a door's or a window's: dragged along its wall
 * by its middle (architecture.moveOpening), widened or narrowed by either jamb, the other jamb staying where it was
 * (architecture.updateWallOpening, its width and middle at once). Its height and sill are in its properties.
 */

import type { Grip, Vec2 } from '../../platform/module';
import { arcLength, pointAt, project, type Arc } from '../../viewport/arc';
import { frameAt } from '../../viewport/gripKit';

/** The narrowest an opening is dragged to (m). */
export const MIN_WIDTH = 0.05;

/** The opening's along and width after its jamb `side` (+1 the far one, −1 the near one) is dragged to `to`; null when nothing is left. */
export function jambTo(c: Arc, along: number, width: number, side: 1 | -1, to: Vec2): { along: number; width: number } | null {
  const fixed = along - (side * width) / 2;
  const s = Math.max(0, Math.min(arcLength(c), project(c, to)));
  const w = side * (s - fixed);
  if (w < MIN_WIDTH) return null;
  return { along: fixed + (side * w) / 2, width: w };
}

/**
 * The opening's two jambs, near then far: each dragged sets its width and middle at once. A door's or window's opening
 * passes the width to the element as its own size (forum topic 140), so its jambs are its grips too.
 */
export function joineryJambGrips(id: string, c: Arc, along: number, width: number): Grip[] {
  const jamb = (side: 1 | -1): Grip => ({
    key: side > 0 ? 'jamb+' : 'jamb-', kind: 'end', at: pointAt(c, along + (side * width) / 2), title: 'Drag the jamb: the opening\'s width',
    step: (to) => { const r = jambTo(c, along, width, side, to); return r ? { command: 'architecture.updateWallOpening', args: { id, along: r.along, width: r.width } } : null; },
  });
  return [jamb(-1), jamb(1)];
}

export function wallOpeningGrips(id: string, c: Arc, along: number, width: number): Grip[] {
  const f = frameAt(c, along);
  return [
    { key: 'along', kind: 'centre', at: f.at, title: 'Drag along the wall', step: (to) => ({ command: 'architecture.moveOpening', args: { id, along: project(c, to) } }) },
    ...joineryJambGrips(id, c, along, width),
  ];
}
