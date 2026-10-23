/**
 * Lapin's side of the engine session: the IFC entities, their relationships
 * and the meshes as the Lapin engine last sent them, kept in step by its
 * diffs. An entity travels as a node whose type is its IFC class. Every edit
 * is an engine command (one undo step); nothing is modelled here.
 */

import { create } from 'zustand';
import { EngineClient, engineUrl } from './client';
import type { Diff, DocState, NodeDto, RelationDto, RenderItem, SessionInfo } from './protocol';
import { storeys } from './model';
import { useUi } from '../app/store';

export interface SessionState {
  connected: boolean;
  info: SessionInfo | null;
  nodes: Map<string, NodeDto>;
  relations: Map<string, RelationDto>;
  render: Map<string, RenderItem>;
  doc: DocState | null;
  version: number;
  error: string | null;
}

export const useSession = create<SessionState>(() => ({
  connected: false, info: null, nodes: new Map(), relations: new Map(), render: new Map(), doc: null, version: -1, error: null,
}));

export const engine = new EngineClient(engineUrl);

/** Fold an engine diff into the session (a reset replaces everything). */
export function applyDiff(d: Diff): void {
  const s = useSession.getState();
  const nodes = d.reset ? new Map<string, NodeDto>() : new Map(s.nodes);
  const relations = d.reset ? new Map<string, RelationDto>() : new Map(s.relations);
  const render = d.reset ? new Map<string, RenderItem>() : new Map(s.render);
  for (const id of d.removedNodes) nodes.delete(id);
  for (const n of d.nodes) nodes.set(n.id, n);
  for (const id of d.removedRelations) relations.delete(id);
  for (const r of d.relations) relations.set(r.id, r);
  for (const id of d.renderRemoved) render.delete(id);
  for (const r of d.render) render.set(r.nodeId, r);
  useSession.setState({ nodes, relations, render, doc: d.state, version: d.version });
  // A selection the edit took away goes too.
  const { selection: sel, selected } = useUi.getState();
  if (sel && !nodes.has(sel)) useUi.setState({ selection: null });
  if (selected.some((id) => !nodes.has(id))) useUi.setState({ selected: selected.filter((id) => nodes.has(id)) });
  const storey = useUi.getState().storeyId;
  if (storey && !nodes.has(storey)) useUi.setState({ storeyId: null });
}

/** Run an engine command; its diff arrives as an event. Errors land in the session. */
export async function execute(command: string, args: Record<string, unknown> = {}): Promise<unknown> {
  try {
    const result = await engine.call('doc.execute', { command, args });
    useSession.setState({ error: null });
    return result;
  } catch (e) {
    useSession.setState({ error: e instanceof Error ? e.message : String(e) });
    return null;
  }
}

export const undo = () => engine.call('doc.undo').catch(() => undefined);
export const redo = () => engine.call('doc.redo').catch(() => undefined);

/** The storey the plan shows and new elements go on: the chosen one, else the lowest. */
export function activeStorey(s: SessionState = useSession.getState()): NodeDto | null {
  const chosen = useUi.getState().storeyId;
  return (chosen ? s.nodes.get(chosen) : undefined) ?? storeys(s)[0] ?? null;
}

/** The storey to build on: the active one, or a new one when the project has none yet. */
export async function ensureStorey(): Promise<string | null> {
  const existing = activeStorey();
  if (existing) return existing.id;
  const result = await execute('storey.create', {}) as { id?: string } | null;
  return activeStorey()?.id ?? result?.id ?? null;
}

/** Connect to the engine and keep the session in step with it. */
export async function connect(): Promise<void> {
  engine.onDiff(applyDiff);
  engine.onStatus(async (connected) => {
    useSession.setState({ connected });
    if (!connected) return;
    const info = await engine.call<SessionInfo>('session.info');
    const snapshot = await engine.call<Diff>('doc.snapshot');
    useSession.setState({ info });
    applyDiff(snapshot);
  });
  await engine.start();
}
