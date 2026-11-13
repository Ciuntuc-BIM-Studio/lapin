/**
 * The Graphs panel's state (docs/plan-visual-graphs.md §5): which graph is open, the nodes' specs, the last evaluation (its
 * values and ghost), and the commands it sends. The graph itself is the engine's (a LapinGraph entity read from the
 * session's nodes): every finished change is a graphs.save, so Ctrl+Z undoes it like any step. The ghost is drawn by every
 * window of the app: the one that evaluates tells the others (Tauri events), so a graph edited in a floating window shows
 * its ghost in the main window's 3D and plans.
 */

import { create } from 'zustand';
import { activeStorey, engine, execute, query, useSession } from '../../engine/session';
import { focusPane, notify, useUi } from '../../app/store';
import { isDesktop } from '../../engine/files';
import { isGuest } from '../../engine/share';
import { emptyGraph, graphOf, nodeSpecs, type Catalogue, type EngineNodes, type GraphJson, type NodeSpec } from './graph';

export const GRAPH_CLASS = 'LapinGraph';
export const VIEW_KIND = 'graphs';

export interface NodeResult { outputs: Record<string, unknown>; error?: string; warning?: string; items?: number; counts?: Record<string, number> }

export interface Ghost {
  parts: { element: string; class: string; storey: string | null; color: string; positions: number[]; indices: number[]; edges: number[] }[];
  plan: { storey: string | null; closed: boolean; points: number[] }[];
  points: number[];
  lines: { closed: boolean; points: number[] }[];
  cut?: boolean;
}

export interface Evaluation { nodes: Record<string, NodeResult>; ghost: Ghost; ms: number; calls: number }

interface GraphsState {
  graphId: string | null;
  specs: Map<string, NodeSpec> | null;
  preview: boolean;
  result: Evaluation | null;
  /** The ghost shown in this window: this window's evaluation, or another window's. */
  ghost: Ghost | null;
  busy: boolean;
}

export const useGraphs = create<GraphsState>(() => ({ graphId: null, specs: null, preview: true, result: null, ghost: null, busy: false }));

/** The project's graphs, by name. */
export function projectGraphs(nodes: Map<string, { id: string; type: string; name: string }>): { id: string; name: string }[] {
  return [...nodes.values()].filter((n) => n.type === GRAPH_CLASS).map((n) => ({ id: n.id, name: n.name })).sort((a, b) => a.name.localeCompare(b.name));
}

/** The open graph as the engine has it now. */
export function currentGraph(): GraphJson {
  const id = useGraphs.getState().graphId;
  const n = id ? useSession.getState().nodes.get(id) : undefined;
  return n ? graphOf(n.props as Record<string, unknown>) : emptyGraph();
}

let loading: Promise<void> | null = null;

/** The nodes' specs, asked once per connection (the product's commands are the engine's). */
export function loadSpecs(): Promise<void> {
  if (useGraphs.getState().specs) return Promise.resolve();
  loading ??= (async () => {
    const own = await query<EngineNodes>('graphs.nodes');
    let catalogue: Catalogue | null = null;
    try { catalogue = await engine.call<Catalogue>('session.catalogue'); } catch { /* the ports of commands unknown: their wires still work */ }
    if (own) useGraphs.setState({ specs: nodeSpecs(own, catalogue) });
  })().finally(() => { loading = null; });
  return loading;
}

/** A graph changed: saved, so the change is one undo step. */
export async function saveGraph(graph: GraphJson): Promise<void> {
  const id = useGraphs.getState().graphId;
  if (!id) return;
  await execute('graphs.save', { id, graph });
}

/** A new graph, named Graph N, opened. */
export async function newGraph(): Promise<void> {
  const taken = new Set(projectGraphs(useSession.getState().nodes).map((g) => g.name.toLowerCase()));
  let i = 1;
  while (taken.has(`graph ${i}`)) i++;
  const r = await execute('graphs.save', { name: `Graph ${i}`, graph: emptyGraph() }) as { id?: string } | null;
  if (r?.id) openGraph(r.id);
}

export function openGraph(id: string | null): void {
  // The graph open already keeps its evaluation (opened again by a link or the guide, nothing changes).
  if (id === useGraphs.getState().graphId) return;
  useGraphs.setState({ graphId: id, result: null });
  shareGhost(null);
}

export async function renameGraph(name: string): Promise<void> {
  const id = useGraphs.getState().graphId;
  if (id && name.trim()) await execute('graphs.rename', { id, name: name.trim() });
}

export async function deleteGraph(): Promise<void> {
  const id = useGraphs.getState().graphId;
  if (!id) return;
  await execute('graphs.delete', { id });
  openGraph(projectGraphs(useSession.getState().nodes)[0]?.id ?? null);
}

let asked = 0;

/** The open graph evaluated by the engine (its values, and its ghost when the preview is on): the model untouched. */
export async function evaluate(): Promise<void> {
  const { graphId } = useGraphs.getState();
  if (!graphId || isGuest()) return;
  const mine = ++asked;
  useGraphs.setState({ busy: true });
  const r = await query<Evaluation>('graphs.evaluate', { id: graphId, storey: activeStorey()?.id });
  if (mine !== asked) return;
  useGraphs.setState({ result: r, busy: false });
  shareGhost(useGraphs.getState().preview ? r?.ghost ?? null : null);
}

export function setPreview(on: boolean): void {
  useGraphs.setState({ preview: on });
  shareGhost(on ? useGraphs.getState().result?.ghost ?? null : null);
}

/** The graph made in the model, one undo step. */
export async function bake(): Promise<void> {
  const id = useGraphs.getState().graphId;
  if (!id) return;
  const r = await execute('graphs.bake', { id, storey: activeStorey()?.id }) as { made?: string[] } | null;
  // Made: the ghost goes (it would draw the same again over them); the eye brings it back.
  if (r) { setPreview(false); notify('Baked: the elements are made. Undo takes them back in one step.'); }
}

/** Graphs opened beside the view (the panel: split on the web, a window of its own on the desktop from its tab). */
/** What a guest of a shared session is told: the project's graphs are the host's, like its scripts (decision 527). */
export const GUEST_REFUSAL = "Graphs are the host's: in a shared session only the host opens, previews and bakes them.";

export function openPanel(): void {
  if (isGuest()) { notify(GUEST_REFUSAL); return; }
  void loadSpecs();
  if (!useGraphs.getState().graphId) openGraph(projectGraphs(useSession.getState().nodes)[0]?.id ?? null);
  const s = useUi.getState();
  const at = s.panes.findIndex((p) => p.view === VIEW_KIND);
  if (at >= 0) { focusPane(at); return; }
  if (s.panes.length < 2) {
    const first = s.panes[0]!;
    useUi.setState({ panes: [first, { ...first, view: VIEW_KIND }], split: 'row', pane: 1 });
    useUi.setState({ view: VIEW_KIND });
    return;
  }
  focusPane(1);
  useUi.setState({ view: VIEW_KIND });
}

// ── the ghost, shared between the app's windows ──

const GHOST_EVENT = 'graphs-ghost';

function shareGhost(ghost: Ghost | null): void {
  useGraphs.setState({ ghost });
  if (!isDesktop()) return;
  void import('@tauri-apps/api/event').then(({ emit }) => emit(GHOST_EVENT, { ghost })).catch(() => undefined);
}

let listening = false;

/** Another window's ghost drawn here too (the module's background, in every window). */
export function listenForGhosts(): void {
  if (listening || !isDesktop()) return;
  listening = true;
  void import('@tauri-apps/api/event').then(({ listen }) => listen<{ ghost: Ghost | null }>(GHOST_EVENT, (e) => useGraphs.setState({ ghost: e.payload.ghost }))).catch(() => { listening = false; });
}
