/**
 * The grip being dragged in a 2D view, for what is drawn along with it (a door's temporary dimensions follow it as it
 * slides along its wall, decision 511): the grip's owner and key and where the drag has it now. Null when none is.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';

export interface GripDragNow { owners: string[]; key: string; at: Vec2 }

export const useGripDrag = create<{ now: GripDragNow | null }>(() => ({ now: null }));

export const setGripDrag = (now: GripDragNow | null): void => {
  if (now === null && useGripDrag.getState().now === null) return;
  useGripDrag.setState({ now });
};
