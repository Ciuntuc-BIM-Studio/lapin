/**
 * How much interface stands around the view (View → Interface…, Ctrl+.):
 * three presets, Comfortable (the ribbon with names, a command line of its
 * own, the HUD over the view), Compact (an icon band, one status line for
 * the prompt, the command line and the HUD, the quick tools docked on the
 * view's edge) and Focus (the tabs and a command line; the ribbon drops
 * down from its tab, the panels hide), and each part chosen on its own,
 * which makes the choice Custom; and the workspace (decision 243): the ribbon
 * tabs a discipline works with, in its order (Architecture, Structure & rebar,
 * MEP, Site & site works, Drafting 2D), every tab (All), or the user's own,
 * which a tab shown, hidden or moved makes and which is kept. The project stays
 * one: a hidden tab's tools are still in Commands… and the shortcuts. Kept per
 * user in the browser's storage (the desktop app and the browser apart). The
 * web build starts in Compact on a short window, until the user chooses.
 */

import { create } from 'zustand';

export type InterfacePreset = 'comfortable' | 'compact' | 'focus';

export interface InterfaceParts {
  /** Large tools with their names, icons only, or a band that opens from its tab. */
  ribbon: 'large' | 'icons' | 'dropdown';
  /** The quick tools 1–9: floating at the bottom of the view, docked on its left edge by the project browser (the default since decision 358), or off (the number keys still work). */
  quickTools: 'floating' | 'docked' | 'off';
  /** The command line under each 2D view, or in the status line. */
  commandLine: 'own' | 'status';
  /** The mode, level, view and snap card: over the view, in the status line, or off. */
  hud: 'over' | 'status' | 'off';
  /** The tool's prompt and its length and angle inputs: a banner over the view, or in the command line. */
  prompt: 'banner' | 'commandLine';
  /** The key plan over the view, or a button that opens it. */
  keyPlan: 'shown' | 'button';
  /** The side panels: docked, the browser folded to a rail, both folded, or both hidden. */
  panels: 'docked' | 'browserRail' | 'bothRail' | 'hidden';
  /** The properties' rows: normal, or dense (22 px). */
  rows: 'normal' | 'dense';
}

export const PRESETS: Record<InterfacePreset, InterfaceParts> = {
  comfortable: { ribbon: 'large', quickTools: 'docked', commandLine: 'own', hud: 'over', prompt: 'banner', keyPlan: 'shown', panels: 'docked', rows: 'normal' },
  compact: { ribbon: 'icons', quickTools: 'docked', commandLine: 'status', hud: 'status', prompt: 'commandLine', keyPlan: 'button', panels: 'browserRail', rows: 'dense' },
  focus: { ribbon: 'dropdown', quickTools: 'docked', commandLine: 'status', hud: 'status', prompt: 'commandLine', keyPlan: 'button', panels: 'hidden', rows: 'dense' },
};

/** Each part's choices, in the order the dialog shows them. */
export const CHOICES: { [K in keyof InterfaceParts]: readonly InterfaceParts[K][] } = {
  ribbon: ['large', 'icons', 'dropdown'],
  quickTools: ['floating', 'docked', 'off'],
  commandLine: ['own', 'status'],
  hud: ['over', 'status', 'off'],
  prompt: ['banner', 'commandLine'],
  keyPlan: ['shown', 'button'],
  panels: ['docked', 'browserRail', 'bothRail', 'hidden'],
  rows: ['normal', 'dense'],
};

/** A workspace: the tabs of a discipline, every tab, or the user's own. */
export type WorkspaceId = 'all' | 'architecture' | 'structure' | 'mep' | 'site' | 'drafting' | 'mine';
export const WORKSPACE_IDS: readonly WorkspaceId[] = ['all', 'architecture', 'structure', 'mep', 'site', 'drafting', 'mine'];
export const WORKSPACE_LABELS: Record<WorkspaceId, string> = {
  all: 'All', architecture: 'Architecture', structure: 'Structure & rebar', mep: 'MEP', site: 'Site & site works', drafting: 'Drafting 2D', mine: 'My workspace',
};

/** Each discipline's tabs in its order; a tab the edition lacks is left out (Wood, Steel, Urban and Rail come with their modules). */
export const WORKSPACE_TABS: Record<Exclude<WorkspaceId, 'all' | 'mine'>, readonly string[]> = {
  architecture: ['draft', 'architecture', 'finishes', 'studio', 'annotate', 'manage', 'quantities', 'view'],
  structure: ['draft', 'structure', 'rebar', 'wood', 'steel', 'annotate', 'manage', 'quantities', 'view'],
  mep: ['draft', 'mep', 'annotate', 'manage', 'quantities', 'view'],
  site: ['draft', 'site', 'sitework', 'urban', 'rail', 'studio', 'annotate', 'manage', 'quantities', 'view'],
  drafting: ['draft', 'annotate', 'manage', 'view'],
};

export interface InterfaceSettings {
  preset: InterfacePreset | 'custom';
  parts: InterfaceParts;
  /** The user chose (a preset or a part): no default by the window's size any more. */
  chosen: boolean;
  /** The tabs shown: a discipline's, all of them, or the user's own (`mine`). */
  workspace: WorkspaceId;
  /** The user's own workspace, its tabs in order; null: every tab but the hidden ones (as kept before workspaces). */
  mine: string[] | null;
  /** The tabs an older Lapin kept hidden, read into the user's own workspace. */
  hiddenTabs: string[];
}

/** The settings' revision: before 2 the quick tools floated by default, so a floating bar kept from then is docked once (decision 358). */
export const INTERFACE_REV = 2;

/** A tab as the ribbon has it: its id and its place among the others. */
export interface TabRef { id: string; order: number }

/** Tabs that stay whatever is hidden: the Interface dialog lives in View. */
export const ALWAYS_SHOWN_TABS = ['view'];

const same = (a: InterfaceParts, b: InterfaceParts) => (Object.keys(CHOICES) as (keyof InterfaceParts)[]).every((k) => a[k] === b[k]);

/** The preset these parts are, or custom. */
export function presetOf(parts: InterfaceParts): InterfacePreset | 'custom' {
  return (Object.keys(PRESETS) as InterfacePreset[]).find((p) => same(PRESETS[p], parts)) ?? 'custom';
}

export const withPreset = (preset: InterfacePreset, from?: Pick<InterfaceSettings, 'workspace' | 'mine' | 'hiddenTabs'>): InterfaceSettings =>
  ({ preset, parts: { ...PRESETS[preset] }, chosen: true, workspace: from?.workspace ?? 'all', mine: from?.mine ?? null, hiddenTabs: from?.hiddenTabs ?? [] });

export function withPart<K extends keyof InterfaceParts>(s: InterfaceSettings, key: K, value: InterfaceParts[K]): InterfaceSettings {
  const parts = { ...s.parts, [key]: value };
  return { ...s, preset: presetOf(parts), parts, chosen: true };
}

/** The tabs a workspace shows, in its order, among those the edition has; View always (Interface… is there). */
export function visibleTabIds(s: Pick<InterfaceSettings, 'workspace' | 'mine' | 'hiddenTabs'>, available: readonly TabRef[]): string[] {
  const byOrder = [...available].sort((a, b) => a.order - b.order).map((t) => t.id);
  const has = new Set(byOrder);
  const wanted = s.workspace === 'all' ? byOrder
    : s.workspace === 'mine' ? (s.mine ?? byOrder.filter((id) => !s.hiddenTabs.includes(id)))
    : WORKSPACE_TABS[s.workspace] ?? byOrder;
  const ids = wanted.filter((id, i) => has.has(id) && wanted.indexOf(id) === i);
  for (const id of ALWAYS_SHOWN_TABS) if (has.has(id) && !ids.includes(id)) ids.push(id);
  return ids;
}

export const withWorkspace = (s: InterfaceSettings, workspace: WorkspaceId): InterfaceSettings => ({ ...s, workspace });

/** A tab shown or hidden: the shown tabs become the user's own workspace (a shown tab in its usual place); a tab that always shows stays. */
export function withTab(s: InterfaceSettings, tab: string, shown: boolean, available: readonly TabRef[]): InterfaceSettings {
  if (ALWAYS_SHOWN_TABS.includes(tab)) return s;
  const now = visibleTabIds(s, available);
  if (shown === now.includes(tab)) return s;
  if (!shown) return { ...s, workspace: 'mine', mine: now.filter((id) => id !== tab) };
  const order = new Map(available.map((t) => [t.id, t.order]));
  const o = order.get(tab) ?? Infinity;
  const at = now.findIndex((id) => (order.get(id) ?? Infinity) > o);
  return { ...s, workspace: 'mine', mine: at < 0 ? [...now, tab] : [...now.slice(0, at), tab, ...now.slice(at)] };
}

/** A shown tab moved one place left (-1) or right (1): the shown tabs become the user's own workspace. */
export function withTabMoved(s: InterfaceSettings, tab: string, by: -1 | 1, available: readonly TabRef[]): InterfaceSettings {
  const now = visibleTabIds(s, available);
  const i = now.indexOf(tab), j = i + by;
  if (i < 0 || j < 0 || j >= now.length) return s;
  const mine = [...now];
  [mine[i], mine[j]] = [mine[j], mine[i]];
  return { ...s, workspace: 'mine', mine };
}

/** Where Lapin runs, for the first choice: the web build on a window under 800 px tall starts compact. */
export interface InterfaceEnv { web: boolean; height: number }

/** The settings from what was kept (anything unreadable left out), else the default for where Lapin runs. */
export function initialSettings(stored: unknown, env: InterfaceEnv): InterfaceSettings {
  const o = stored as { parts?: Record<string, unknown>; chosen?: unknown; hiddenTabs?: unknown; workspace?: unknown; mine?: unknown; rev?: unknown } | null;
  const hiddenTabs = o && typeof o === 'object' && Array.isArray(o.hiddenTabs) ? o.hiddenTabs.filter((t): t is string => typeof t === 'string' && !ALWAYS_SHOWN_TABS.includes(t)) : [];
  const mine = o && typeof o === 'object' && Array.isArray(o.mine) ? o.mine.filter((t): t is string => typeof t === 'string') : null;
  // Tabs hidden before workspaces were: the user's own workspace, every tab but those.
  const workspace: WorkspaceId = o && typeof o === 'object' && WORKSPACE_IDS.includes(o.workspace as WorkspaceId) ? o.workspace as WorkspaceId : hiddenTabs.length > 0 ? 'mine' : 'all';
  const tabs = { workspace, mine, hiddenTabs };
  if (o && typeof o === 'object' && o.chosen === true && o.parts && typeof o.parts === 'object') {
    const parts = { ...PRESETS.comfortable };
    for (const k of Object.keys(CHOICES) as (keyof InterfaceParts)[]) {
      const v = o.parts[k];
      if ((CHOICES[k] as readonly unknown[]).includes(v)) (parts as Record<string, unknown>)[k] = v;
    }
    if (o.rev !== INTERFACE_REV && parts.quickTools === 'floating') parts.quickTools = 'docked';
    return { preset: presetOf(parts), parts, chosen: true, ...tabs };
  }
  const preset: InterfacePreset = env.web && env.height < 800 ? 'compact' : 'comfortable';
  return { preset, parts: { ...PRESETS[preset] }, chosen: false, ...tabs };
}

const KEY = 'lapin.interface';

function readStored(): unknown {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : null; } catch { return null; }
}

/** The window's height and whether this is the web build, read once at start. */
function env(): InterfaceEnv {
  // The web build runs the engine in a worker (engine/session.ts reads the same flag).
  const web = import.meta.env?.VITE_ENGINE === 'worker';
  return { web, height: typeof window !== 'undefined' ? window.innerHeight : 900 };
}

export const useInterface = create<InterfaceSettings>(() => initialSettings(readStored(), env()));

/** Settings chosen by the user: kept, and handed to whoever applies them (the panels). */
const listeners: ((s: InterfaceSettings, before: InterfaceSettings) => void)[] = [];
export const onInterfaceChange = (f: (s: InterfaceSettings, before: InterfaceSettings) => void) => { listeners.push(f); };

export function setInterface(s: InterfaceSettings): void {
  const before = useInterface.getState();
  useInterface.setState(s, true);
  try { localStorage.setItem(KEY, JSON.stringify({ ...s, rev: INTERFACE_REV })); } catch { /* kept for this session */ }
  for (const f of listeners) f(s, before);
}

export const choosePreset = (p: InterfacePreset) => setInterface(withPreset(p, useInterface.getState()));
export const chooseWorkspace = (w: WorkspaceId) => setInterface(withWorkspace(useInterface.getState(), w));
export const showTab = (tab: string, shown: boolean, available: readonly TabRef[]) => setInterface(withTab(useInterface.getState(), tab, shown, available));
export const moveTab = (tab: string, by: -1 | 1, available: readonly TabRef[]) => setInterface(withTabMoved(useInterface.getState(), tab, by, available));
export const choosePart = <K extends keyof InterfaceParts>(key: K, value: InterfaceParts[K]) => setInterface(withPart(useInterface.getState(), key, value));
