/**
 * What a door's, window's or opening's sill is measured from (forum topic 192, decision 745): the wall's structural
 * base (the old and IFC way) or the finished floor over it. The finished floor is derived as the engine does:
 * the wall's own `finishedFloor`, else its type's, else 0.
 */

import type { NodeDto } from '../../engine/protocol';

export type SillFrom = 'structural' | 'finished';

/** The reference an opening says it uses (absent: structural). */
export const sillFromOf = (opening: Pick<NodeDto, 'props'> | null | undefined): SillFrom => (opening?.props.sillFrom === 'finished' ? 'finished' : 'structural');

const floorOf = (n: Pick<NodeDto, 'props'> | null | undefined): number | null => {
  const v = n?.props.finishedFloor;
  return typeof v === 'number' && Number.isFinite(v) ? v : null;
};

/** The finished floor over a wall's base, metres: the wall's own, else its type's, else 0. */
export const finishedFloorOf = (wall: Pick<NodeDto, 'props'> | null | undefined, wallType: Pick<NodeDto, 'props'> | null | undefined): number => floorOf(wall) ?? floorOf(wallType) ?? 0;

/** Whether to hint that "Finished floor" measures from the structural base for now: chosen, and the wall's finished floor is 0. */
export const floorHintShown = (from: SillFrom, floor: number): boolean => from === 'finished' && Math.abs(floor) < 1e-9;
