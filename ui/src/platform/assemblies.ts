/**
 * Drill-down picking (View → Drill into assemblies; off by default, kept on this computer, display.ts). An element
 * that is a part of a whole (IfcRelAggregates: a steel hall's members, an IfcElementAssembly's parts, an assembly in
 * another) is picked from the top: the first click takes the outermost whole, a click inside the selected whole steps
 * into it and takes the part on the way to what is under the cursor, and so on to the element itself. Esc steps one
 * level up and lets go at the top, a click on nothing lets go, Ctrl/Cmd+click takes the element directly, and a
 * breadcrumb over the view (DrillCrumbs) names the levels. While inside a whole, what is outside it is dimmed.
 *
 * Groups (docs/groups.md) are the top of the same chain: an element's group, then its wholes, then the element
 * (the engine's `core.assemblyPath`, Assemblies.cs, is the same rule). A group opened by a double-click stays the edit
 * scope (scope.ts) it is: inside it the chain starts below the group. Off, picking is what it was: a group's member
 * picks the group, everything else picks itself (groups.ts' pickTarget).
 */

import { create } from 'zustand';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { select, useUi } from '../app/store';
import { useDisplay } from '../app/display';
import { clickMode, picksLeaf, type Mods, type NavStyle } from '../app/navStyle';
import { addSelectionExpander, expansionChanged, GROUP, groupOf, membersOf, pickTarget } from './groups';
import { useScope } from './scope';

export const AGGREGATES = 'IfcRelAggregates';

/** Where a chain ends: the spatial structure (an element's storey, a storey's building), never a whole to pick. */
export const SPATIAL = new Set(['IfcProject', 'IfcSite', 'IfcBuilding', 'IfcBuildingStorey', 'IfcSpace', 'IfcFacility', 'IfcFacilityPart',
  'IfcBridge', 'IfcBridgePart', 'IfcRoad', 'IfcRoadPart', 'IfcRailway', 'IfcRailwayPart', 'IfcMarineFacility', 'IfcMarinePart',
  'IfcSpatialZone', 'IfcExternalSpatialElement']);

type Nodes = ReadonlyMap<string, NodeDto>;
type Relations = ReadonlyMap<string, RelationDto>;

const wholes = new WeakMap<Relations, { up: Map<string, string>; down: Map<string, string[]> }>();

/** Each part's whole and each whole's parts, from the aggregation relationships (kept per relationships map). */
function aggregation(relations: Relations) {
  let a = wholes.get(relations);
  if (a) return a;
  a = { up: new Map(), down: new Map() };
  for (const r of relations.values()) {
    if (r.class !== AGGREGATES) continue;
    for (const id of r.related) a.up.set(id, r.relating);
    a.down.set(r.relating, [...(a.down.get(r.relating) ?? []), ...r.related]);
  }
  wholes.set(relations, a);
  return a;
}

/** The whole `id` is a part of, below the spatial structure. */
export function wholeOf(id: string, nodes: Nodes = useSession.getState().nodes, relations: Relations = useSession.getState().relations): string | undefined {
  const w = aggregation(relations).up.get(id);
  const n = w ? nodes.get(w) : undefined;
  return n && !SPATIAL.has(n.type) ? w : undefined;
}

/** The chain from the top to `id`: its group (when the chain is in one), the outermost whole, …, `id` itself. */
export function assemblyPath(id: string, nodes: Nodes = useSession.getState().nodes, relations: Relations = useSession.getState().relations): string[] {
  const path = [id];
  const seen = new Set(path);
  for (let w = wholeOf(id, nodes, relations); w && !seen.has(w); w = wholeOf(w, nodes, relations)) { seen.add(w); path.unshift(w); }
  if (nodes.get(path[0])?.type !== GROUP) {
    for (const x of path) { const g = groupOf(x, relations); if (g) { path.unshift(g); break; } }
  }
  return path;
}

/** Everything under `id`: its parts and theirs, a group's members and theirs. */
export function descendantsOf(id: string, nodes: Nodes = useSession.getState().nodes, relations: Relations = useSession.getState().relations): Set<string> {
  const out = new Set<string>();
  const down = aggregation(relations).down;
  const stack = [id];
  while (stack.length) {
    const x = stack.pop()!;
    const kids = nodes.get(x)?.type === GROUP ? membersOf(x, relations) : SPATIAL.has(nodes.get(x)?.type ?? '') ? [] : down.get(x) ?? [];
    for (const k of kids) if (!out.has(k) && k !== id) { out.add(k); stack.push(k); }
  }
  return out;
}

/** The chain a pick of `id` drills along now: below an open edit scope when it is on the chain; just `id` inside another scope. */
export function chainFor(id: string): string[] {
  const chain = assemblyPath(id);
  const scope = useScope.getState().scope;
  if (!scope) return chain;
  const i = chain.indexOf(scope.id);
  return i >= 0 && i < chain.length - 1 ? chain.slice(i + 1) : [id];
}

/**
 * What a plain click on the last element of `chain` selects, the selection being `selected` and the chain of the
 * click before `path`: one level below the selected whole when it is on the chain (the element itself at the bottom);
 * else the part at the deepest level the two chains share inside the selection's wholes (a sibling of the selection,
 * or of one of its wholes); else the top. `leaf` (Ctrl/Cmd) takes the element itself.
 */
export function drillTarget(chain: readonly string[], path: readonly string[] | null, selected: readonly string[], leaf = false): string | null {
  if (!chain.length) return null;
  const last = chain.length - 1;
  if (leaf) return chain[last];
  const sel = selected.length === 1 ? selected[0] : null;
  if (!sel) return chain[0];
  const i = chain.indexOf(sel);
  if (i >= 0) return chain[Math.min(i + 1, last)];
  const level = path ? path.indexOf(sel) : -1;
  if (level <= 0) return chain[0];
  let shared = 0;
  while (shared < level && chain[shared] === path![shared]) shared++;
  return chain[Math.min(shared, last)];
}

/** The chain of the last drill pick (the selection is one of its levels while it is in it). */
export const useDrill = create<{ path: string[] | null }>(() => ({ path: null }));

/** The levels shown: the last pick's chain and the selection's place in it, while the selection is one element on it (and the chain more than one). */
export function drillLevel(path: readonly string[] | null, selected: readonly string[]): number {
  if (!path || path.length < 2 || selected.length !== 1) return -1;
  return path.indexOf(selected[0]);
}

/** What Esc does to a drilled selection: the whole above it, or nothing selected at the top; undefined when it is not drilled. */
export function drillUp(path: readonly string[] | null, selected: readonly string[]): string | null | undefined {
  const level = drillLevel(path, selected);
  if (level < 0) return undefined;
  return level > 0 ? path![level - 1] : null;
}

/** Whether drill-down picking is on. */
export const drilling = (): boolean => useDisplay.getState().drillSelect;

/**
 * A pick of `id` (null: nothing under the cursor) with no tool on. Off: as before (a group's member picks the group;
 * Shift adds or takes out). On: along the element's chain (drillTarget); Shift adds the top (with Ctrl/Cmd the element).
 */
export function pickClick(id: string | null | undefined, mods: { add?: boolean; leaf?: boolean } = {}): void {
  const add = !!mods.add;
  if (!id && add) return;
  if (!drilling()) { select(pickTarget(id), add); return; }
  if (!id) { if (add) return; useDrill.setState({ path: null }); select(null); return; }
  const chain = chainFor(id);
  if (add) { select(mods.leaf ? chain[chain.length - 1] : chain[0], true); return; }
  const target = drillTarget(chain, useDrill.getState().path, useUi.getState().selected, mods.leaf);
  useDrill.setState({ path: chain });
  select(target);
}

/**
 * A pick as a navigation style reads its keys (app/navStyle.ts): replace, toggle (what Shift always did), or, in the other
 * programs' ways, add only or remove only. Lapin's own keeps pickClick's behaviour to the letter. Empty space with a key
 * that adds, removes or toggles leaves the selection as it is (outside Lapin's style).
 */
export function pickByMods(id: string | null | undefined, mods: Mods, style: NavStyle): void {
  const mode = clickMode(style, mods), leaf = picksLeaf(style, mods);
  // Shift on empty space leaves the selection as it is, in every style (only a plain click on nothing lets go).
  if (!id && mode !== 'replace') return;
  if (style.native || mode === 'replace') { pickClick(id, { add: mode === 'toggle', leaf }); return; }
  if (!id) return;
  const before = useUi.getState().selected;
  pickClick(id, { add: true, leaf });
  if (mode === 'toggle') return;
  const after = useUi.getState().selected;
  const added = after.some((x) => !before.includes(x)), removed = before.some((x) => !after.includes(x));
  // The pick flipped what it met; add keeps it when it was already in, remove keeps it out when it was not.
  if ((mode === 'add' && removed && !added) || (mode === 'remove' && added && !removed)) useUi.setState({ selected: [...before], selection: before[before.length - 1] ?? null });
}

/** Several picks (a box, a lasso): off, each as its group; on, each as the top of its chain. Once each. */
export function pickRegion(ids: readonly string[]): string[] {
  if (!drilling()) return [...new Set(ids.map(pickTarget).filter((x): x is string => !!x))];
  return [...new Set(ids.map((id) => chainFor(id)[0]))];
}

/** Esc on a drilled selection: one level up (true), or not drilled (false: Esc goes on to what else it does). */
export function drillEscape(): boolean {
  if (!drilling()) return false;
  const up = drillUp(useDrill.getState().path, useUi.getState().selected);
  if (up === undefined) return false;
  if (up === null) useDrill.setState({ path: null });
  select(up);
  return true;
}

/** The whole the drilled selection is inside (its parent on the chain), or null at the top or when not drilled. */
export function drillScope(path: readonly string[] | null, selected: readonly string[]): string | null {
  const level = drillLevel(path, selected);
  return level > 0 ? path![level - 1] : null;
}

/** What a drilled selection leaves bright: the whole it is inside and everything in it; null when nothing is dimmed. */
export function drillInside(path: readonly string[] | null, selected: readonly string[], nodes: Nodes = useSession.getState().nodes, relations: Relations = useSession.getState().relations): Set<string> | null {
  if (!drilling()) return null;
  const whole = drillScope(path, selected);
  if (!whole) return null;
  const inside = descendantsOf(whole, nodes, relations);
  inside.add(whole);
  return inside;
}

// A whole selected while drilling draws its parts highlighted (a group's members already are).
addSelectionExpander((id, node) => (drilling() && node && node.type !== GROUP && !SPATIAL.has(node.type) ? [...descendantsOf(id)] : null));
// Turned on or off: the views draw the selection again; off, nothing stays dimmed or drilled.
useDisplay.subscribe((s, before) => { if (s.drillSelect !== before.drillSelect) { if (!s.drillSelect) useDrill.setState({ path: null }); expansionChanged(); } });
