/**
 * The straight piece being drawn in a plan (decision 693): the tool, the point placed and where the cursor would put the next,
 * set by the plan as the cursor moves; null when nothing is being drawn. A module's plan overlay reads it (a wall's temporary
 * dimensions to the walls beside it before the click).
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';

export interface DrawnPath { tool: string; storeyId: string | null; a: Vec2; b: Vec2 }

export const useDrawnPath = create<{ now: DrawnPath | null }>(() => ({ now: null }));

/** Set what is being drawn (null: nothing), leaving the store alone when it is the same. */
export function setDrawnPath(now: DrawnPath | null): void {
  const was = useDrawnPath.getState().now;
  if (was === now || (was && now && was.tool === now.tool && was.storeyId === now.storeyId && was.a.x === now.a.x && was.a.y === now.a.y && was.b.x === now.b.x && was.b.y === now.b.y)) return;
  useDrawnPath.setState({ now });
}
