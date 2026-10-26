/**
 * The IFC tree of the model, for the browser's IFC tab: the storeys, under
 * each the classes of what stands on it, under each class the types, under
 * each type the elements, and under an element its construction: the
 * representation the file gave (an extrusion with its profile, a B-rep with
 * its faces, a boolean with its operands), its openings and what fills them.
 */

import type { NodeDto, RelationDto } from '../engine/protocol';
import { num, storeys, type ModelState } from '../engine/model';

export interface TreeElement { node: NodeDto; openings: { opening: NodeDto; fill: NodeDto | null }[]; shape: ShapeItem[] }
export interface TreeType { id: string; name: string; node: NodeDto | null; elements: TreeElement[] }
export interface TreeClass { cls: string; types: TreeType[]; count: number }
export interface TreeStorey { node: NodeDto | null; name: string; classes: TreeClass[]; count: number }

/** One representation item as the import described it (Lapin.Ifc, IfcModel.Describe). */
export interface ShapeItem { type: string; [k: string]: unknown }

const SPATIAL = new Set(['IfcProject', 'IfcSite', 'IfcBuilding', 'IfcBuildingStorey']);
/** What the tree leaves out: views, sheets, schedules, types, materials, layer sets, usages, phases (the browser shows those elsewhere). */
const NOT_ELEMENTS = new Set(['LapinView', 'LapinSheet', 'LapinSchedule', 'LapinViewSet', 'IfcMaterial', 'IfcMaterialLayerSet', 'IfcMaterialLayerSetUsage', 'LapinPhase', 'LapinDrawing', 'LapinGraphicsSet']);

export const isType = (n: NodeDto) => /Type$|Style$/.test(n.type);
export const isElement = (n: NodeDto) => !SPATIAL.has(n.type) && !NOT_ELEMENTS.has(n.type) && !isType(n) && n.type !== 'IfcOpeningElement' && !n.type.startsWith('Lapin');

/** The representation items kept on an imported element (a reference's `shape`, a recognised one's `imported.shape`). */
export function shapeOf(n: NodeDto): ShapeItem[] {
  const p = n.props as Record<string, unknown>;
  const raw = (p.shape ?? (p.imported as Record<string, unknown> | undefined)?.shape) as unknown;
  return Array.isArray(raw) ? (raw as ShapeItem[]) : [];
}

export function ifcTree(s: ModelState): TreeStorey[] {
  const typeOfId = new Map<string, NodeDto>();
  const openingsOf = new Map<string, string[]>();
  const fillOf = new Map<string, string>();
  const fills = new Set<string>();
  for (const r of s.relations.values()) {
    if (r.class === 'IfcRelDefinesByType') { const t = s.nodes.get(r.relating); if (t) for (const id of r.related) typeOfId.set(id, t); }
    else if (r.class === 'IfcRelVoidsElement') openingsOf.set(r.relating, [...(openingsOf.get(r.relating) ?? []), ...r.related]);
    else if (r.class === 'IfcRelFillsElement') for (const id of r.related) { fillOf.set(r.relating, id); fills.add(id); }
  }
  const levels = storeys(s).slice().reverse();
  const byStorey = new Map<string | null, NodeDto[]>();
  for (const n of s.nodes.values()) {
    if (!isElement(n) || fills.has(n.id)) continue; // a door or window hangs under its wall's opening
    const key = n.parentId && s.nodes.get(n.parentId)?.type === 'IfcBuildingStorey' ? n.parentId : null;
    byStorey.set(key, [...(byStorey.get(key) ?? []), n]);
  }
  const element = (n: NodeDto): TreeElement => ({
    node: n,
    openings: (openingsOf.get(n.id) ?? []).map((oid) => s.nodes.get(oid)).filter((o): o is NodeDto => !!o).map((o) => ({ opening: o, fill: fillOf.has(o.id) ? s.nodes.get(fillOf.get(o.id)!) ?? null : null })),
    shape: shapeOf(n),
  });
  const classesOf = (list: NodeDto[]): TreeClass[] => {
    const byClass = new Map<string, NodeDto[]>();
    for (const n of list) byClass.set(n.type, [...(byClass.get(n.type) ?? []), n]);
    return [...byClass.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([cls, ns]) => {
      const byType = new Map<string, { node: NodeDto | null; name: string; elements: NodeDto[] }>();
      for (const n of ns) {
        const t = typeOfId.get(n.id) ?? null;
        const key = t?.id ?? '';
        const g = byType.get(key) ?? { node: t, name: t?.name ?? '(no type)', elements: [] };
        g.elements.push(n);
        byType.set(key, g);
      }
      const types = [...byType.entries()].sort(([, a], [, b]) => a.name.localeCompare(b.name)).map(([id, g]) => ({ id: id || `${cls}:untyped`, name: g.name, node: g.node, elements: g.elements.sort((a, b) => a.name.localeCompare(b.name)).map(element) }));
      return { cls, types, count: ns.length };
    });
  };
  const out: TreeStorey[] = levels.map((st) => {
    const list = byStorey.get(st.id) ?? [];
    return { node: st, name: `${st.name} · ${num(st, 'elevation') >= 0 ? '+' : '−'}${Math.abs(num(st, 'elevation')).toFixed(2)}`, classes: classesOf(list), count: list.length };
  });
  const loose = byStorey.get(null) ?? [];
  if (loose.length) out.push({ node: null, name: 'Not on a level', classes: classesOf(loose), count: loose.length });
  return out;
}

/** A representation item in a line: "IfcExtrudedAreaSolid · IfcRectangleProfileDef 5000 × 300 · depth 2800 · ↑". */
export function describeItem(item: ShapeItem): string {
  const mm = (v: unknown) => (typeof v === 'number' ? `${Math.round(v * 1000)}` : '');
  const parts: string[] = [item.type];
  const profile = item.profile as ShapeItem | undefined;
  if (profile) {
    const dims = profile.x != null ? `${mm(profile.x)} × ${mm(profile.y)}` : profile.radius != null ? `r ${mm(profile.radius)}` : profile.width != null ? `${mm(profile.width)} × ${mm(profile.depth)}` : profile.points != null ? `${profile.points} points${profile.holes ? `, ${profile.holes} holes` : ''}` : '';
    parts.push(`${profile.type}${dims ? ' ' + dims : ''}`);
  }
  if (item.depth != null) parts.push(`depth ${mm(item.depth)}`);
  if (Array.isArray(item.direction)) { const d = item.direction as number[]; parts.push(d[2] > 0.99 ? '↑' : d[2] < -0.99 ? '↓' : `→ (${d.map((v) => v.toFixed(2)).join(', ')})`); }
  if (item.faces != null) parts.push(`${item.faces} faces`);
  if (item.triangles != null) parts.push(`${item.triangles} triangles`);
  if (item.operator) parts.push(String(item.operator).toLowerCase());
  if (item.radius != null && !profile) parts.push(`r ${mm(item.radius)}`);
  if (item.angle != null) parts.push(`${item.angle}°`);
  return parts.join(' · ');
}

/** What Lapin itself built an element from, when it is not imported: its own attributes that read as numbers or short text. */
export function ownParameters(n: NodeDto): [string, string][] {
  const out: [string, string][] = [];
  for (const [k, v] of Object.entries(n.props as Record<string, unknown>)) {
    if (k === 'imported' || k === 'body' || k === 'properties' || k === 'shape' || k === 'reference') continue;
    if (typeof v === 'number') out.push([k, Number.isInteger(v) ? String(v) : v.toFixed(3)]);
    else if (typeof v === 'string' && v.length <= 40) out.push([k, v]);
    else if (typeof v === 'boolean') out.push([k, v ? 'yes' : 'no']);
    else if (Array.isArray(v) && v.length <= 3 && v.every((x) => typeof x === 'number')) out.push([k, `(${(v as number[]).map((x) => x.toFixed(3)).join(', ')})`]);
    if (out.length >= 10) break;
  }
  return out;
}

export type { RelationDto };
