/**
 * An element's hatch origin (docs/plan-material-graphics.md, stage 1): where its surface hatches and cut bands start, `[u, v]`
 * metres in the element's own frame (u along its axis from the start, else right from its leftmost point; v up from its base in
 * elevations and sections, to the left of the axis in a plan). Stored as the attribute `hatchOrigin`; `element.setHatchOrigin`.
 */

import type { NodeDto } from '../../engine/protocol';

/** The classes whose surfaces and cuts hatch with their materials, so a hatch origin means something. */
export const HATCH_ORIGIN_TYPES = new Set([
  'IfcWall', 'IfcWallStandardCase', 'IfcCurtainWall', 'IfcSlab', 'IfcRoof', 'IfcCovering', 'IfcPlate', 'IfcColumn', 'IfcBeam', 'IfcMember', 'IfcFooting',
]);

export const hasHatchOrigin = (node: NodeDto): boolean => HATCH_ORIGIN_TYPES.has(node.type);

/** The origin an element carries, or null (none: the view's, or a real-size pattern's base). */
export function hatchOriginOf(node: NodeDto): [number, number] | null {
  const o = node.props.hatchOrigin;
  return Array.isArray(o) && o.length >= 2 && typeof o[0] === 'number' && typeof o[1] === 'number' ? [o[0], o[1]] : null;
}

/** One coordinate changed: the other stays (0 when the element has no origin yet); empty (null) counts as 0. */
export function withCoordinate(current: [number, number] | null, axis: 0 | 1, metres: number | null): [number, number] {
  const next: [number, number] = current ? [current[0], current[1]] : [0, 0];
  next[axis] = metres ?? 0;
  return next;
}

/** element.setHatchOrigin's arguments: many elements, one undo step; null clears. */
export function setOriginArgs(ids: readonly string[], origin: [number, number] | null): Record<string, unknown> {
  return { ...(ids.length === 1 ? { id: ids[0] } : { ids: [...ids] }), origin };
}
