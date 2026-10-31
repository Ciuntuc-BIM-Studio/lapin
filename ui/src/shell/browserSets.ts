/**
 * The project browser's sets (docs/plan-views-and-joinery.md §1): a level of
 * the tree above the kinds of drawings. A set takes every view and sheet its
 * filter matches (each field given must hold); a project without sets of its
 * own reads as one set per discipline. An item may sit in several sets; one
 * no set takes goes under "Other". The engine keeps the sets on the project
 * (`browserSets`); this decides what goes where.
 */

import type { NodeDto } from '../engine/protocol';
import { productInfo } from '../platform/brand';

/** `site`: a site plan or a location plan, listed under the browser's Site rather than in the sets. */
export type ItemKind = 'plan' | 'section' | 'elevation' | 'plane' | '3d' | 'sheet' | 'schedule' | 'site';

export interface SetFilter {
  disciplines?: string[];
  kinds?: string[];
  storeys?: string[];
  scales?: string[];
  phases?: string[];
  nameContains?: string;
  params?: Record<string, unknown>;
}

export interface BrowserSet { id: string; name: string; filter: SetFilter }

/** A view, sheet or schedule as the sets see it. */
export interface BrowserItem {
  id: string;
  name: string;
  kind: ItemKind;
  discipline: string;
  storeyId: string | null;
  scale: number | null;
  phaseId: string | null;
  params: Record<string, unknown>;
  /** The entity, or null for an implicit one (a storey's plan, {3D}). */
  node: NodeDto | null;
}

export interface SetGroup { set: BrowserSet | null; items: BrowserItem[] }

/** The product's disciplines (products/<id>.json), else architecture, structure and MEP; the first is a view's when it names none. */
export const DISCIPLINES: string[] = productInfo.disciplines?.length ? productInfo.disciplines : ['Architecture', 'Structure', 'MEP'];
export const DEFAULT_DISCIPLINE = DISCIPLINES[0];
export const KINDS: ItemKind[] = ['plan', 'section', 'elevation', 'plane', '3d', 'sheet', 'schedule'];
export const DEFAULT_SETS: BrowserSet[] = DISCIPLINES.map((d) => ({ id: d.toLowerCase(), name: d, filter: { disciplines: [d] } }));

const same = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

export function matches(filter: SetFilter | undefined, item: BrowserItem): boolean {
  if (!filter) return true;
  if (filter.disciplines?.length && !filter.disciplines.some((d) => same(d, item.discipline))) return false;
  if (filter.kinds?.length && !filter.kinds.includes(item.kind)) return false;
  if (filter.storeys?.length && !(item.storeyId && filter.storeys.includes(item.storeyId))) return false;
  if (filter.scales?.length && !(item.scale != null && filter.scales.some((s) => Number(s) === item.scale))) return false;
  if (filter.phases?.length && !(item.phaseId && filter.phases.includes(item.phaseId))) return false;
  if (filter.nameContains && !item.name.toLowerCase().includes(filter.nameContains.toLowerCase())) return false;
  for (const [k, v] of Object.entries(filter.params ?? {})) {
    const own = item.params[k];
    if (own == null || String(own) !== String(v)) return false;
  }
  return true;
}

/** The sets the project holds, or the defaults. */
export function setsOf(nodes: Map<string, NodeDto>): BrowserSet[] {
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  const own = project?.props.browserSets;
  if (!Array.isArray(own) || own.length === 0) return DEFAULT_SETS;
  return own.flatMap((s) => {
    if (!s || typeof s !== 'object') return [];
    const o = s as { id?: unknown; name?: unknown; filter?: unknown };
    return typeof o.id === 'string' && typeof o.name === 'string' ? [{ id: o.id, name: o.name, filter: (o.filter && typeof o.filter === 'object' ? o.filter : {}) as SetFilter }] : [];
  });
}

/** The user's own parameters of an entity, name to value. */
export function paramsOf(node: NodeDto | null | undefined): Record<string, unknown> {
  const list = node?.props.params;
  if (!Array.isArray(list)) return {};
  const o: Record<string, unknown> = {};
  for (const p of list) {
    if (!p || typeof p !== 'object') continue;
    const { name, value } = p as { name?: unknown; value?: unknown };
    if (typeof name === 'string') o[name] = value;
  }
  return o;
}

const str = (v: unknown): string | null => (typeof v === 'string' && v.length > 0 ? v : null);
const number = (v: unknown): number | null => (typeof v === 'number' ? v : typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v)) ? Number(v) : null);

/** A view, sheet or schedule entity as an item; null for any other entity. */
export function itemOf(node: NodeDto): BrowserItem | null {
  const kind: ItemKind | null = node.type === 'LapinView' ? (node.props.kind === 'plan' && typeof node.props.site === 'string' ? 'site'
    : ['plan', 'section', 'elevation', 'plane', '3d'].includes(String(node.props.kind)) ? (node.props.kind as ItemKind) : null)
    : node.type === 'LapinSheet' ? 'sheet' : node.type === 'LapinSchedule' ? 'schedule' : null;
  if (!kind) return null;
  return {
    id: node.id, name: node.name, kind, discipline: str(node.props.discipline) ?? DEFAULT_DISCIPLINE,
    storeyId: str(node.props.storeyId), scale: number(node.props.scale), phaseId: str(node.props.phaseId), params: paramsOf(node), node,
  };
}

/** A storey's own plan, which no entity holds. */
export const implicitPlan = (storey: NodeDto): BrowserItem => ({
  id: 'plan:' + storey.id, name: storey.name, kind: 'plan', discipline: DEFAULT_DISCIPLINE, storeyId: storey.id, scale: 100, phaseId: null, params: {}, node: null,
});

/** The 3D view the project always has. */
export const THREE_D: BrowserItem = { id: '3d', name: '3D', kind: '3d', discipline: DEFAULT_DISCIPLINE, storeyId: null, scale: null, phaseId: null, params: {}, node: null };

/** Each set with what it takes, in order, then "Other" (a null set) with what none took, when there is any. */
export function groupItems(sets: BrowserSet[], items: BrowserItem[]): SetGroup[] {
  const groups = sets.map((set) => ({ set, items: items.filter((i) => matches(set.filter, i)) }));
  const taken = new Set(groups.flatMap((g) => g.items.map((i) => i.id)));
  const other = items.filter((i) => !taken.has(i.id));
  return other.length ? [...groups, { set: null, items: other }] : groups;
}

/** The discipline a view or sheet made in a set takes: the one its filter names, else the default. */
export const disciplineOf = (set: BrowserSet | null): string => (set?.filter.disciplines?.length === 1 ? set.filter.disciplines[0] : DEFAULT_DISCIPLINE);
