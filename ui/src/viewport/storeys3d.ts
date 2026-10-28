/**
 * Which storeys a 3D view shows (report #29): every storey (the default),
 * those ticked, the active storey alone, or the active storey and those below
 * it (a building cut at a level, to look inside). It is the 3D view's own
 * setting (view.setStoreys; {3D}'s on the project, `view3d.storeys`), so a
 * saved 3D view keeps it; "active" and "below" follow the active storey as it
 * changes. An element belongs to a storey by its containment (or its
 * aggregate's); what stands on no storey (the site, the terrain, links) shows
 * unless the setting says not. Optionally the model is cut above the highest
 * storey shown, that far over its level (1.20 m: as a plan cuts).
 */

import type { NodeDto } from '../engine/protocol';

export type Storeys3dMode = 'all' | 'some' | 'active' | 'below';

export interface Storeys3d {
  mode: Storeys3dMode;
  ids: string[];
  /** What stands on no storey is shown. */
  unplaced: boolean;
  /** The model cut this far above the highest storey shown's level; null: not cut. */
  cut: number | null;
}

export const ALL_STOREYS: Storeys3d = { mode: 'all', ids: [], unplaced: true, cut: null };

const MODES: readonly Storeys3dMode[] = ['all', 'some', 'active', 'below'];

/** A 3D view's setting as its props keep it. */
export function storeys3dOf(props: Record<string, unknown> | undefined): Storeys3d {
  const s = props?.storeys as Partial<Storeys3d> | undefined;
  if (!s || typeof s !== 'object') return ALL_STOREYS;
  return {
    mode: MODES.includes(s.mode as Storeys3dMode) ? s.mode as Storeys3dMode : 'all',
    ids: Array.isArray(s.ids) ? s.ids.filter((x): x is string => typeof x === 'string') : [],
    unplaced: s.unplaced !== false,
    cut: typeof s.cut === 'number' && Number.isFinite(s.cut) ? s.cut : null,
  };
}

export const isAll = (s: Storeys3d) => s.mode === 'all' && s.unplaced && s.cut === null;

/** The props a 3D view keeps its setting in: a saved view's own, {3D}'s on the project. */
export function view3dProps(nodes: Map<string, NodeDto>, viewId: string | null): Record<string, unknown> | undefined {
  if (viewId && viewId !== '3d') return nodes.get(viewId)?.props;
  for (const n of nodes.values()) if (n.type === 'IfcProject') return n.props.view3d as Record<string, unknown> | undefined;
  return undefined;
}

export const settingOf = (nodes: Map<string, NodeDto>, viewId: string | null): Storeys3d => storeys3dOf(view3dProps(nodes, viewId));

const elevation = (n: NodeDto) => Number(n.props.elevation ?? 0);

/** The storeys shown, by id, given the storeys lowest first and the active one; null: every storey. */
export function shownStoreys(s: Storeys3d, storeys: NodeDto[], activeId: string | null): Set<string> | null {
  switch (s.mode) {
    case 'all': return null;
    case 'some': return new Set(s.ids.filter((id) => storeys.some((st) => st.id === id)));
    case 'active': return new Set(activeId ? [activeId] : []);
    case 'below': {
      const active = storeys.find((st) => st.id === activeId);
      if (!active) return new Set();
      const top = elevation(active);
      return new Set(storeys.filter((st) => st.id === active.id || elevation(st) < top - 1e-6).map((st) => st.id));
    }
  }
}

const SPATIAL = new Set(['IfcProject', 'IfcSite', 'IfcBuilding']);

/** The storey an element stands on: its containment, else its aggregate's (a curtain wall's panel, a stair's flight); null: none. */
export function storeyOf(nodes: Map<string, NodeDto>, node: NodeDto): string | null {
  let n: NodeDto | undefined = node;
  for (let depth = 0; n && depth < 8; depth++) {
    if (n.storeyId) return n.storeyId;
    const parent: NodeDto | undefined = n.parentId ? nodes.get(n.parentId) : undefined;
    if (!parent || SPATIAL.has(parent.type)) return null;
    if (parent.type === 'IfcBuildingStorey') return parent.id;
    n = parent;
  }
  return null;
}

/** The elements (of those given) a 3D view leaves out by their storeys. */
export function hiddenByStoreys(nodes: Map<string, NodeDto>, ids: Iterable<string>, s: Storeys3d, storeys: NodeDto[], activeId: string | null): string[] {
  const shown = shownStoreys(s, storeys, activeId);
  if (!shown && s.unplaced) return [];
  const hidden: string[] = [];
  for (const id of ids) {
    const node = nodes.get(id);
    if (!node || node.type === 'IfcBuildingStorey' || SPATIAL.has(node.type)) continue;
    const storey = storeyOf(nodes, node);
    if (storey === null ? !s.unplaced : shown !== null && !shown.has(storey)) hidden.push(id);
  }
  return hidden;
}

/** Where the model is cut (a height, metres): the cut above the highest storey shown's level; null: not cut. */
export function cutZ(s: Storeys3d, storeys: NodeDto[], activeId: string | null): number | null {
  if (s.cut === null) return null;
  const shown = shownStoreys(s, storeys, activeId);
  const list = shown ? storeys.filter((st) => shown.has(st.id)) : storeys;
  if (!list.length) return null;
  return Math.max(...list.map(elevation)) + s.cut;
}

/** What the 3D view's chip says when not everything is shown ("Ground floor" "only", "cut at 1.20 m"); null: everything is. */
export function chipText(s: Storeys3d, storeys: NodeDto[], activeId: string | null): { name: string | null; text: string; cut: string | null } | null {
  if (isAll(s)) return null;
  const active = storeys.find((st) => st.id === activeId);
  const shown = shownStoreys(s, storeys, activeId);
  const cut = s.cut !== null ? `cut at ${s.cut.toFixed(2)} m` : null;
  if (s.mode === 'active') return { name: active?.name ?? null, text: 'only', cut };
  if (s.mode === 'below') return { name: active?.name ?? null, text: 'and below', cut };
  if (s.mode === 'some' && shown) {
    if (shown.size === 1) return { name: storeys.find((st) => shown.has(st.id))?.name ?? null, text: 'only', cut };
    return { name: null, text: `${shown.size} of ${storeys.length} storeys`, cut };
  }
  return { name: null, text: s.unplaced ? 'All storeys' : 'Without what is on no storey', cut };
}
