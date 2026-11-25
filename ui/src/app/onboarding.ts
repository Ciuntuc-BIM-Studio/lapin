/**
 * Role-differentiated interactive onboarding & procedural Sandbox missions
 * (docs/plan-ux-game-like.md, P2).
 *
 * - Two short tracks: Universal Basics (interface, shortcuts, undo) and a
 *   discipline-specific Role Mission (Architect, Structural, Roads & site,
 *   MEP, Drafting 2D).
 * - Steps auto-complete when real engine/UI state changes occur.
 * - Procedural ~70% pre-built Sandbox starters run via lightweight engine
 *   commands (zero binary assets, respecting the 24 MB web build budget).
 */

import { create } from 'zustand';
import type { WorkspaceId } from './interface';
import type { NodeDto } from '../engine/protocol';
import { activeStorey, execute, useSession } from '../engine/session';
import { newProject } from '../engine/files';
import { notifyProblem, useUi } from './store';
import { hasModule } from '../platform/brand';
import { useWalk } from '../viewport/walk';
import { playSound } from './sounds';

export type MissionTrack = 'basics' | 'role';

export interface MissionStepDef {
  id: string;
  track: MissionTrack;
  title: string;
  hint: string;
  shortcut?: string;
  toolId?: string;
  dialog?: string;
  check: (ctx: MissionContext) => boolean;
}

export interface MissionBaseline {
  total: number;
  walls: number;
  openings: number;
  columns: number;
  beams: number;
  terrains: number;
  mep: number;
  drafts: number;
  dims: number;
  sheets: number;
}

export interface MissionContext {
  baseline: MissionBaseline;
  counts: MissionBaseline;
  view: string;
  panes: number;
  tool: string | null;
  dialog: string | null;
  walking: boolean;
  canUndo: boolean;
  canRedo: boolean;
  events: ReadonlySet<string>;
}

export function countNodes(nodes: Iterable<NodeDto>): MissionBaseline {
  const c: MissionBaseline = { total: 0, walls: 0, openings: 0, columns: 0, beams: 0, terrains: 0, mep: 0, drafts: 0, dims: 0, sheets: 0 };
  for (const n of nodes) {
    c.total++;
    if (n.type === 'IfcWall') c.walls++;
    else if (n.type === 'IfcDoor' || n.type === 'IfcWindow') c.openings++;
    else if (n.type === 'IfcColumn') c.columns++;
    else if (n.type === 'IfcBeam') c.beams++;
    else if (n.type === 'IfcGeographicElement' && n.props.predefinedType === 'TERRAIN') c.terrains++;
    else if (n.type.startsWith('IfcPipe') || n.type.startsWith('IfcDuct') || n.type.startsWith('IfcFlow')) c.mep++;
    else if (n.type === 'IfcAnnotation' && n.props.kind === 'dimension') c.dims++;
    else if (n.type === 'IfcAnnotation') c.drafts++;
    else if (n.type === 'LapinSheet') c.sheets++;
  }
  return c;
}

const BASICS_STEPS: MissionStepDef[] = [
  {
    id: 'basics.preset', track: 'basics', title: 'Customize your workspace',
    hint: 'Open Interface… (Ctrl+.) and try Flow, Compact or Comfortable.', shortcut: 'Ctrl+.', dialog: 'interface',
    check: (ctx) => ctx.events.has('preset') || ctx.dialog === 'interface',
  },
  {
    id: 'basics.palette', track: 'basics', title: 'Find any tool with Commands…',
    hint: 'Press Ctrl+K and type a tool name (fuzzy search works too).', shortcut: 'Ctrl+K', dialog: 'palette',
    check: (ctx) => ctx.events.has('palette') || ctx.dialog === 'palette',
  },
  {
    id: 'basics.undo', track: 'basics', title: 'Make an edit & test Undo',
    hint: 'Every command is one undo step (Ctrl+Z).', shortcut: 'Ctrl+Z',
    check: (ctx) => ctx.canUndo || ctx.canRedo,
  },
];

export function roleStepsFor(workspace: WorkspaceId): MissionStepDef[] {
  switch (workspace) {
    case 'structure':
      return [
        { id: 'str.column', track: 'role', title: 'Place a column', hint: 'Press CL and snap to a corner or grid intersection.', shortcut: 'CL', toolId: 'structure.column', check: (c) => c.counts.columns > c.baseline.columns },
        { id: 'str.beam', track: 'role', title: 'Connect with a beam', hint: 'Press BM and draw between two columns (type exact length).', shortcut: 'BM', toolId: 'structure.beam', check: (c) => c.counts.beams > c.baseline.beams },
        { id: 'str.3d', track: 'role', title: 'Inspect the frame in 3D', hint: 'Click 3D or Plan + 3D in the view bar.', toolId: 'view.3d', check: (c) => c.view === '3d' || c.panes >= 2 },
        { id: 'str.xray', track: 'role', title: 'Peek inside with X-Ray', hint: 'Hold X in 3D to peek through surfaces, then release.', shortcut: 'X', check: (c) => c.events.has('xrayPeek') },
      ];
    case 'site':
      return [
        { id: 'site.terrain', track: 'role', title: 'Create a terrain surface', hint: 'Press TF (Flat terrain) or TO (Terrain on outline).', shortcut: 'TF', toolId: 'civil.flatTerrain', check: (c) => c.counts.terrains > c.baseline.terrains },
        { id: 'site.spot', track: 'role', title: 'Place a spot level', hint: 'Press SP and click on the site plan.', shortcut: 'SP', toolId: 'civil.spot', check: (c) => c.counts.total > c.baseline.total + 1 },
        { id: 'site.3d', track: 'role', title: 'Inspect the site in 3D', hint: 'Switch to 3D or open Plan + 3D live split view.', toolId: 'view.3d', check: (c) => c.view === '3d' || c.panes >= 2 },
        { id: 'site.measure', track: 'role', title: 'Measure a distance', hint: 'Click Measure in QuickViews and pick two points.', toolId: 'measure', check: (c) => c.tool === 'measure' || c.events.has('measure') },
      ];
    case 'mep':
      return [
        { id: 'mep.run', track: 'role', title: 'Route a pipe or duct run', hint: 'Press RN and click two points (type exact length).', shortcut: 'RN', toolId: 'mep.run', check: (c) => c.counts.mep > c.baseline.mep || c.counts.total > c.baseline.total },
        { id: 'mep.3d', track: 'role', title: 'Check routing in 3D', hint: 'Click 3D in QuickViews to inspect elevations.', toolId: 'view.3d', check: (c) => c.view === '3d' || c.panes >= 2 },
        { id: 'mep.xray', track: 'role', title: 'Peek behind walls (hold X)', hint: 'Hold X in 3D to see systems through finishes.', shortcut: 'X', check: (c) => c.events.has('xrayPeek') },
        { id: 'mep.split', track: 'role', title: 'Open Plan + 3D split view', hint: 'Click Plan + 3D in QuickViews to work in both at once.', check: (c) => c.panes >= 2 },
      ];
    case 'drafting':
      return [
        { id: 'cad.line', track: 'role', title: 'Draw an exact line', hint: 'Press L, click start, type 4000 and press Enter.', shortcut: 'L', toolId: 'cad.line', check: (c) => c.counts.drafts > c.baseline.drafts || c.counts.total > c.baseline.total },
        { id: 'cad.dim', track: 'role', title: 'Place an associative dimension', hint: 'Press DI and pick two points or an edge.', shortcut: 'DI', toolId: 'drafting.dimension', check: (c) => c.counts.dims > c.baseline.dims || c.tool === 'drafting.dimension' },
        { id: 'cad.sheet', track: 'role', title: 'Create a sheet', hint: 'Open Annotate → Sheet to lay out a drawing at scale.', toolId: 'sheet', check: (c) => c.counts.sheets > c.baseline.sheets || c.view === 'sheet' },
        { id: 'cad.measure', track: 'role', title: 'Verify with Measure', hint: 'Click Measure in QuickViews to check any length.', toolId: 'measure', check: (c) => c.tool === 'measure' || c.events.has('measure') },
      ];
    default: // architecture, all, mine
      return [
        { id: 'arch.wall', track: 'role', title: 'Close the 4th wall', hint: 'Press WA, click a corner, type 6000 and press Enter.', shortcut: 'WA', toolId: 'architecture.wall', check: (c) => c.counts.walls > c.baseline.walls },
        { id: 'arch.opening', track: 'role', title: 'Place a door or window', hint: 'Press DR (Door) or WN (Window) and click on any wall.', shortcut: 'DR', toolId: 'architecture.door', check: (c) => c.counts.openings > c.baseline.openings },
        { id: 'arch.3d', track: 'role', title: 'Inspect in 3D or Plan + 3D', hint: 'Click 3D or Plan + 3D in the view bar.', toolId: 'view.3d', check: (c) => c.view === '3d' || c.panes >= 2 },
        { id: 'arch.walk', track: 'role', title: 'Walk inside your room', hint: 'Click Walk in 3D (footprint icon) and move with WASD.', check: (c) => c.walking || c.events.has('walk') },
      ];
  }
}

export const allStepsFor = (workspace: WorkspaceId): MissionStepDef[] => [...roleStepsFor(workspace), ...BASICS_STEPS];

const DONE_KEY = 'lapin.onboarding.done';
const DISMISSED_KEY = 'lapin.onboarding.dismissed';

function readSet(key: string): string[] {
  try {
    const raw = localStorage.getItem(key);
    const arr = raw ? JSON.parse(raw) : [];
    return Array.isArray(arr) ? arr.filter((x): x is string => typeof x === 'string') : [];
  } catch { return []; }
}

interface OnboardingState {
  open: boolean;
  sandbox: boolean;
  track: MissionTrack;
  done: string[];
  events: string[];
  baseline: MissionBaseline;
}

const emptyBaseline = (): MissionBaseline => ({ total: 0, walls: 0, openings: 0, columns: 0, beams: 0, terrains: 0, mep: 0, drafts: 0, dims: 0, sheets: 0 });

export const useOnboarding = create<OnboardingState>(() => ({
  open: false,
  sandbox: false,
  track: 'role',
  done: readSet(DONE_KEY),
  events: [],
  baseline: emptyBaseline(),
}));

export function noteMissionEvent(event: string): void {
  useOnboarding.setState((s) => (s.events.includes(event) ? s : { events: [...s.events, event] }));
}

export function openOnboarding(track: MissionTrack = 'role'): void {
  const baseline = countNodes(useSession.getState().nodes.values());
  useOnboarding.setState({ open: true, track, baseline });
  try { localStorage.removeItem(DISMISSED_KEY); } catch { /* session only */ }
}

export function closeOnboarding(): void {
  useOnboarding.setState({ open: false });
  try { localStorage.setItem(DISMISSED_KEY, '1'); } catch { /* session only */ }
}

export function markStepsDone(ids: string[]): void {
  if (!ids.length) return;
  useOnboarding.setState((s) => {
    const merged = [...new Set([...s.done, ...ids])];
    if (merged.length === s.done.length) return s;
    try { localStorage.setItem(DONE_KEY, JSON.stringify(merged)); } catch { /* session only */ }
    playSound('complete');
    return { done: merged };
  });
}

export function resetOnboarding(): void {
  const baseline = countNodes(useSession.getState().nodes.values());
  try { localStorage.removeItem(DONE_KEY); } catch { /* session only */ }
  useOnboarding.setState({ done: [], events: [], baseline });
}

/** The starter model's engine steps for a role (pure): three corners of a 6 m × 5 m bay, or three walls and the floor of a room. */
export function sandboxSteps(workspace: WorkspaceId, storeyId: string | undefined, has: (module: string) => boolean): { command: string; args: Record<string, unknown> }[] {
  if (workspace === 'structure' && has('structure')) {
    // Three corners of the bay: the user places the 4th column and connects the beams.
    return ([[0, 0], [6, 0], [0, 5]] as const).map(([x, y]) => ({ command: 'structure.addColumn', args: { storeyId, x, y } }));
  }
  if ((workspace === 'architecture' || workspace === 'all' || workspace === 'mine' || workspace === 'mep') && has('architecture')) {
    // Three walls of the room (a U): the user closes the 4th, adds a door or a window, and walks in 3D.
    const walls = [[0, 0, 6, 0], [6, 0, 6, 5], [0, 5, 0, 0]].map(([x1, y1, x2, y2]) => ({ command: 'architecture.addWall', args: { storeyId, x1, y1, x2, y2 } }));
    return [...walls, { command: 'architecture.addSlab', args: { storeyId, points: [[0, 0], [6, 0], [6, 5], [0, 5]] } }];
  }
  return [];
}

/**
 * Starts a procedural ~70% pre-built Sandbox mini-project for the chosen role: a new project (nothing happens when the
 * user keeps the one open), the starter model as one engine edit (`edit.batch`: one undo step), then the mission card.
 * An edition without the role's module starts empty, with the missions; a failed edit is said, never swallowed.
 */
export async function launchRoleSandbox(workspace: WorkspaceId): Promise<void> {
  if (!(await newProject())) return;
  const steps = sandboxSteps(workspace, activeStorey()?.id, hasModule);
  if (steps.length) {
    const done = await execute('edit.batch', { steps });
    const error = useSession.getState().error;
    if (done == null && error) notifyProblem(`The sandbox model could not be made: ${error}`);
  }
  const baseline = countNodes(useSession.getState().nodes.values());
  useOnboarding.setState({ open: true, sandbox: true, track: 'role', baseline, events: [] });
}

/** Evaluates all mission steps for `workspace` against current live app state. */
export function evaluateMissions(workspace: WorkspaceId): string[] {
  const ob = useOnboarding.getState();
  const ui = useUi.getState();
  const sess = useSession.getState();
  const ctx: MissionContext = {
    baseline: ob.baseline,
    counts: countNodes(sess.nodes.values()),
    view: ui.view,
    panes: ui.panes.length,
    tool: ui.tool,
    dialog: ui.dialog,
    walking: useWalk.getState().start !== null,
    canUndo: Boolean(sess.doc?.canUndo),
    canRedo: Boolean(sess.doc?.canRedo),
    events: new Set(ob.events),
  };
  return allStepsFor(workspace).filter((s) => !ob.done.includes(s.id) && s.check(ctx)).map((s) => s.id);
}
