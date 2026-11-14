/**
 * Reading the IFC model as the session holds it: storeys, types, layer sets,
 * usages, materials. Pure functions over the session state, so panels and
 * tests share them.
 */

import type { NodeDto, RelationDto } from './protocol';
import { arcLength, arcOf } from '../viewport/arc';
import { formatLength } from './unitsStore';

export interface ModelState { nodes: Map<string, NodeDto>; relations: Map<string, RelationDto> }

export interface Layer { material: string; thickness: number; core?: boolean; name?: string | null; from?: number | null; to?: number | null; parts?: { material: string; height: number; name?: string | null }[] | null; priority?: number | null; note?: string | null; category?: string | null; extendDown?: boolean | null; extendUp?: boolean | null }

export const num = (n: NodeDto | undefined, key: string, fallback = 0): number => {
  const v = n?.props[key];
  return typeof v === 'number' ? v : fallback;
};

/**
 * The nodes of a class. A class of types (…Type) comes in the project's order for it (forum #71, the engine's
 * TypeOrder.Ordered): those the project's typeOrder lists, in that order, then the others as they were made, so
 * every type list and picker reads the order the user gave.
 */
export const ofClass = (s: ModelState, cls: string): NodeDto[] => {
  const out: NodeDto[] = [];
  let project: NodeDto | undefined;
  for (const n of s.nodes.values()) {
    if (n.type === cls) out.push(n);
    else if (n.type === 'IfcProject' && !project) project = n;
  }
  return cls.endsWith('Type') ? inTypeOrder(out, project?.props.typeOrder, cls) : out;
};

/** Types in the order a project's typeOrder ({class: [ids]}) gives their class: those it lists first, in its order, then the rest as they came. */
export function inTypeOrder(types: NodeDto[], typeOrder: unknown, cls: string): NodeDto[] {
  const list = typeOrder && typeof typeOrder === 'object' ? (typeOrder as Record<string, unknown>)[cls] : undefined;
  if (!Array.isArray(list) || list.length === 0) return types;
  const rank = new Map<string, number>();
  list.forEach((id, i) => { if (typeof id === 'string' && !rank.has(id)) rank.set(id, i); });
  return types.map((n, i) => ({ n, i })).sort((a, b) => (rank.get(a.n.id) ?? Infinity) - (rank.get(b.n.id) ?? Infinity) || a.i - b.i).map((x) => x.n);
}

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
