/**
 * A graph as a tool (docs/plan-visual-graphs.md §8 stage 2, decision 536): a graph whose "Show in My tools" is on gives a
 * tool (platform/extraTools.ts) that opens or closes its palette (PaletteWindow.tsx), put in My tools the first time it is
 * seen, in its own group or "Graphs" (app/myTools.ts's addNewToolsTo; one the user takes out stays out). Its settings are
 * the graph's (format 2's `tool`), so whoever opens the project gets the same tool. Works on the web as on the desktop:
 * the graph is the engine's, no Python. A guest of a shared session gets no tool and no palette (graphs are the host's).
 */

import { create } from 'zustand';
import { useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { addNewToolsTo, changeMyTab, myTab } from '../../app/myTools';
import { setExtraTools } from '../../platform/extraTools';
import type { ToolDef } from '../../platform/module';
import { isGuest } from '../../engine/share';
import { scriptIcon } from '../../shell/toolIcons';
import { graphOf } from './graph';
import { GRAPHS_GROUP, toolIdOf, toolOf } from './palette';
import { GRAPH_CLASS, GUEST_REFUSAL, loadSpecs } from './store';

interface PalettesState {
  /** The graphs whose palettes are open, by id, in the order they were opened. */
  open: string[];
}

export const usePalettes = create<PalettesState>(() => ({ open: [] }));

/** A graph's palette opened, or closed when it is open. */
export function togglePalette(graphId: string): void {
  if (isGuest()) { notify(GUEST_REFUSAL); return; }
  void loadSpecs();
  usePalettes.setState((s) => ({ open: s.open.includes(graphId) ? s.open.filter((x) => x !== graphId) : [...s.open, graphId] }));
}

const ADDED_KEY = 'lapin.graphTools.added';

function readAdded(): Set<string> {
  try { const raw = localStorage.getItem(ADDED_KEY); const v = raw ? JSON.parse(raw) : []; return new Set(Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : []); } catch { return new Set(); }
}

/** The tools put in My tools once (a tool the user takes out stays out). */
let added = readAdded();

/** The project's graphs' tools now: given to every tool, and the new ones put in My tools; a palette whose graph is gone closes. */
export function refreshGraphTools(nodes = useSession.getState().nodes): void {
  const defs: ToolDef[] = [];
  const groups: { id: string; panel: string }[] = [];
  if (!isGuest()) {
    for (const n of nodes.values()) {
      if (n.type !== GRAPH_CLASS) continue;
      const t = toolOf(graphOf(n.props as Record<string, unknown>), n.name);
      if (!t) continue;
      const id = toolIdOf(n.id);
      defs.push({ id, label: t.name, icon: scriptIcon(t.icon, t.name), tooltip: t.tooltip ?? 'Open the palette of this graph', run: () => togglePalette(n.id) });
      groups.push({ id, panel: t.group });
    }
  }
  defs.sort((a, b) => a.label.localeCompare(b.label));
  setExtraTools('graphs', defs);
  const open = usePalettes.getState().open.filter((id) => nodes.get(id)?.type === GRAPH_CLASS && !isGuest());
  if (open.length !== usePalettes.getState().open.length) usePalettes.setState({ open });
  const r = addNewToolsTo(myTab(), groups, added, GRAPHS_GROUP);
  if (r.added.size !== added.size) {
    added = r.added;
    try { localStorage.setItem(ADDED_KEY, JSON.stringify([...added])); } catch { /* this session only */ }
    changeMyTab(() => r.tab);
  }
}

let started = false;

/** The graphs' tools kept in step with the project (the module's background, in the main window only: My tools is its). */
export function startGraphTools(): void {
  if (started || useUi.getState().windowRole === 'view') return;
  started = true;
  let sig = '';
  const check = (nodes: ReturnType<typeof useSession.getState>['nodes']) => {
    const now = [...nodes.values()].filter((n) => n.type === GRAPH_CLASS)
      .map((n) => `${n.id}\u0000${n.name}\u0000${JSON.stringify((n.props as { graph?: { tool?: unknown } }).graph?.tool ?? null)}`).sort().join('\n') + (isGuest() ? '\u0000guest' : '');
    if (now !== sig) { sig = now; refreshGraphTools(nodes); }
  };
  check(useSession.getState().nodes);
  useSession.subscribe((s, was) => { if (s.nodes !== was.nodes) check(s.nodes); });
}
