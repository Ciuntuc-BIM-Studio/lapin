/**
 * An element's elevation (decision 437, a tester's report #117, who asked for coordinates as Bonsai has): its level's
 * elevation and the offset it stands at from the level, one an absolute figure the other follows. Each class keeps the
 * offset it always had (a wall's or column's base offset, a beam's top offset from the level's top, a floor's top offset,
 * a footing's offset from its level); the absolute elevation is the level's plus that, and editing either changes it.
 */

import type { NodeDto } from './protocol';

export interface Elevation {
  /** The attribute the offset is kept in. */
  key: 'baseOffset' | 'topOffset' | 'offset';
  /** What the absolute elevation is of: the base, the top or the bottom of the element. */
  of: 'base' | 'top' | 'bottom';
  /** The absolute elevation when the offset is zero (the level's, the level's top, a footing's bottom). */
  origin: number;
  /** The offset now (m). */
  offset: number;
  /** The absolute elevation now (m). */
  absolute: number;
}

const num = (v: unknown, fallback = 0) => (typeof v === 'number' && Number.isFinite(v) ? v : fallback);

/**
 * The elevation of an element on a storey, or null when its class has no offset to set (an annotation, a door in its wall).
 * <paramref name="type"/>: its type, for a footing's bottom level.
 */
export function elevationOf(node: NodeDto, storey: NodeDto | null, type: NodeDto | null, declared: readonly string[]): Elevation | null {
  if (!storey || storey.type !== 'IfcBuildingStorey') return null;
  const level = num(storey.props.elevation);
  const has = (k: string) => declared.includes(k) || typeof node.props[k] === 'number';
  const make = (key: Elevation['key'], of: Elevation['of'], origin: number): Elevation => {
    const offset = num(node.props[key]);
    return { key, of, origin, offset, absolute: origin + offset };
  };
  if (node.type === 'IfcFooting') return make('offset', 'bottom', level + num(type?.props.bottom, -1.8));
  if (node.type === 'IfcBeam' && has('topOffset')) return make('topOffset', 'top', level + num(storey.props.height, 3));
  if (node.type === 'IfcSlab' && has('offset')) return make('offset', 'top', level);
  if (has('baseOffset')) return make('baseOffset', 'base', level);
  return null;
}

/** The offset that puts the element at <paramref name="absolute"/>. */
export const offsetFor = (e: Elevation, absolute: number): number => Math.round((absolute - e.origin) * 1e6) / 1e6;

/** The elevation while the element is dragged up or down by `dz` in a section or an elevation (decision 454): what Properties shows live. */
export const raisedBy = (e: Elevation, dz: number): Elevation =>
  dz ? { ...e, offset: Math.round((e.offset + dz) * 1e6) / 1e6, absolute: Math.round((e.absolute + dz) * 1e6) / 1e6 } : e;
