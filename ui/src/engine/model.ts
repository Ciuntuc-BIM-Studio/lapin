/**
 * Reading the IFC model as the session holds it: storeys, types, layer sets,
 * usages, materials. Pure functions over the session state, so panels and
 * tests share them.
 */

import type { NodeDto, RelationDto } from './protocol';
import { arcLength, arcOf } from '../viewport/arc';
import { formatLength } from './unitsStore';

export interface ModelState { nodes: Map<string, NodeDto>; relations: Map<string, RelationDto> }

export interface Layer { material: string; thickness: number; core?: boolean; name?: string | null; from?: number | null; to?: number | null }

export const num = (n: NodeDto | undefined, key: string, fallback = 0): number => {
  const v = n?.props[key];
  return typeof v === 'number' ? v : fallback;
};

export const ofClass = (s: ModelState, cls: string): NodeDto[] => [...s.nodes.values()].filter((n) => n.type === cls);

/** The storeys, lowest first. */
export const storeys = (s: ModelState): NodeDto[] => ofClass(s, 'IfcBuildingStorey').sort((a, b) => num(a, 'elevation') - num(b, 'elevation'));

/** The entity relating `id` through a relationship of `cls` (an element's storey, its type). */
export function relatingOf(s: ModelState, id: string, cls: string): NodeDto | null {
  for (const r of s.relations.values()) if (r.class === cls && r.related.includes(id)) return s.nodes.get(r.relating) ?? null;
  return null;
}

/** The entities `id` relates through relationships of `cls`. */
export function relatedOf(s: ModelState, id: string, cls: string): NodeDto[] {
  const o: NodeDto[] = [];
  for (const r of s.relations.values()) if (r.class === cls && r.relating === id) for (const x of r.related) { const n = s.nodes.get(x); if (n) o.push(n); }
  return o;
}

export const typeOf = (s: ModelState, id: string): NodeDto | null => relatingOf(s, id, 'IfcRelDefinesByType');

/** What an element or a type is made of: a layer set, a usage, a material. */
export const materialOf = (s: ModelState, id: string): NodeDto | null => relatingOf(s, id, 'IfcRelAssociatesMaterial');

export const layersOf = (set: NodeDto | null | undefined): Layer[] => {
  const v = set?.props.layers;
  return Array.isArray(v) ? (v as unknown as Layer[]) : [];
};

/** A type's layers, through its layer set. */
export function typeLayers(s: ModelState, typeId: string): Layer[] {
  const set = materialOf(s, typeId);
  return set?.type === 'IfcMaterialLayerSet' ? layersOf(set) : [];
}

/** An element's own usage: which way its layers run. */
export function usageOf(s: ModelState, id: string): { negative: boolean; offset: number } | null {
  const u = materialOf(s, id);
  if (u?.type !== 'IfcMaterialLayerSetUsage') return null;
  return { negative: u.props.directionSense === 'NEGATIVE', offset: num(u, 'offsetFromReferenceLine') };
}

export const materialName = (s: ModelState, id: string): string => s.nodes.get(id)?.name ?? '?';
export const materialColor = (s: ModelState, id: string): string => {
  const c = s.nodes.get(id)?.props.color;
  return typeof c === 'string' ? c : '#c9ced6';
};

/** A length in metres as the interface shows it (the project's unit: mm unless set otherwise). */
export const mm = (m: number) => formatLength(m);

/** A wall's length along its axis (an arc's along the arc). */
export const wallLength = (w: NodeDto): number => {
  const c = arcOf(w.props);
  return c ? arcLength(c) : 0;
};
