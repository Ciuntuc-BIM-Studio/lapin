/**
 * Lapin's side of the engine session: the IFC entities, their relationships
 * and the meshes as the Lapin engine last sent them, kept in step by its
 * diffs. An entity travels as a node whose type is its IFC class. Every edit
 * is an engine command (one undo step); nothing is modelled here.
 */

import { create } from 'zustand';
import { EngineClient, engineUrl, LONG_TIMEOUT_MS } from './client';
import { workerTransport } from './workerTransport';
import { notify } from '../app/store';
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
  /** Why the engine is unreachable, once that has lasted (null while it is fine). */
  engineProblem: string | null;
}

export const useSession = create<SessionState>(() => ({
  connected: false, info: null, nodes: new Map(), relations: new Map(), render: new Map(), doc: null, version: -1, error: null, engineProblem: null,
}));

/** The web build runs the engine in a worker (VITE_ENGINE=worker); the desktop app and pnpm dev reach the local engine over its WebSocket. */
export const engineInBrowser = import.meta.env.VITE_ENGINE === 'worker';

export const engine = new EngineClient(engineInBrowser ? workerTransport() : engineUrl);

/** Fold an engine diff into the session (a reset replaces everything). A diff older than what is shown is skipped. */
export function applyDiff(d: Diff): void {
  const s = useSession.getState();
  if (!d.reset && d.version <= s.version) return;
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
  // A grid axis is selected as grid#tag: it stays while its grid does.
  const alive = (id: string) => nodes.has(id) || (id.includes('#') && nodes.has(id.slice(0, id.indexOf('#'))));
  if (sel && !alive(sel)) useUi.setState({ selection: null });
  if (selected.some((id) => !alive(id))) useUi.setState({ selected: selected.filter(alive) });
  const storey = useUi.getState().storeyId;
  if (storey && !nodes.has(storey)) useUi.setState({ storeyId: null });
  // A type that went is no longer what the tools build with.
  const ui = useUi.getState();
  for (const k of ['wallTypeId', 'slabTypeId', 'doorTypeId', 'windowTypeId'] as const)
    if (ui[k] && !nodes.has(ui[k]!)) useUi.setState({ [k]: null });
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

/** Ask the engine a question (no edit): null when it cannot answer. */
export async function query<T = unknown>(name: string, args: Record<string, unknown> = {}): Promise<T | null> {
  try { return await engine.call<T>('doc.query', { name, args }); } catch { return null; }
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

/** How long the engine may be unreachable before the window says so. */
const LOST_NOTICE_MS = 10_000;
const LOST_NOTICE = 'The engine stopped; restarting…';

/** Connect to the engine and keep the session in step with it. */
export async function connect(): Promise<void> {
  engine.onDiff(applyDiff);
  let lost: ReturnType<typeof setTimeout> | undefined;
  engine.onStatus((connected) => {
    useSession.setState({ connected });
    clearTimeout(lost);
    if (connected) {
      useSession.setState({ engineProblem: null });
      void resync();
    } else {
      lost = setTimeout(() => { if (!useSession.getState().engineProblem) useSession.setState({ engineProblem: LOST_NOTICE }); }, LOST_NOTICE_MS);
    }
  });
  void watchDesktopEngine();
  await engine.start();
}

/** In the desktop app the shell restarts a stopped engine and says how it went. */
async function watchDesktopEngine(): Promise<void> {
  if (!(window as unknown as { __TAURI_INTERNALS__?: unknown }).__TAURI_INTERNALS__) return;
  const { listen } = await import('@tauri-apps/api/event');
  await listen('engine-restarted', () => notify('The engine stopped and was restarted.'));
  await listen<string>('engine-failed', (e) => useSession.setState({ engineProblem: e.payload || 'The engine could not be restarted: quit and reopen Lapin.' }));
}

/**
 * After (re)connecting: the engine's description and the whole model. A
 * connection that drops meanwhile is left to the next status change; any
 * other failure is said, and tried again shortly, so the screen never keeps
 * a previous engine's model for good.
 */
async function resync(attempt = 0): Promise<void> {
  try {
    const info = await engine.call<SessionInfo>('session.info');
    const snapshot = await engine.call<Diff>('doc.snapshot', {}, { timeoutMs: LONG_TIMEOUT_MS });
    useSession.setState({ info });
    applyDiff(snapshot);
  } catch (e) {
    if (!useSession.getState().connected) return;
    if (attempt >= 5) { notify(`The engine's model could not be read: ${e instanceof Error ? e.message : String(e)}`); return; }
    setTimeout(() => void resync(attempt + 1), 500 * (attempt + 1));
  }
}
