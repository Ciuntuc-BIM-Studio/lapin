/**
 * How much interface stands around the view (View → Interface…, Ctrl+.):
 * three presets, Comfortable (the ribbon with names, a command line of its
 * own, the HUD over the view), Compact (an icon band, one status line for
 * the prompt, the command line and the HUD, the quick tools docked on the
 * view's edge) and Focus (the tabs and a command line; the ribbon drops
 * down from its tab, the panels hide), and each part chosen on its own,
 * which makes the choice Custom. Kept per user in the browser's storage (the
 * desktop app and the browser apart). The web build starts in Compact on a
 * short window, until the user chooses.
 */

import { create } from 'zustand';

export type InterfacePreset = 'comfortable' | 'compact' | 'focus';

export interface InterfaceParts {
  /** Large tools with their names, icons only, or a band that opens from its tab. */
  ribbon: 'large' | 'icons' | 'dropdown';
  /** The quick tools 1–9: floating at the bottom of the view, docked on its left edge, or off (the number keys still work). */
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
  comfortable: { ribbon: 'large', quickTools: 'floating', commandLine: 'own', hud: 'over', prompt: 'banner', keyPlan: 'shown', panels: 'docked', rows: 'normal' },
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

export interface InterfaceSettings {
  preset: InterfacePreset | 'custom';
  parts: InterfaceParts;
  /** The user chose (a preset or a part): no default by the window's size any more. */
  chosen: boolean;
}

const same = (a: InterfaceParts, b: InterfaceParts) => (Object.keys(CHOICES) as (keyof InterfaceParts)[]).every((k) => a[k] === b[k]);

/** The preset these parts are, or custom. */
export function presetOf(parts: InterfaceParts): InterfacePreset | 'custom' {
  return (Object.keys(PRESETS) as InterfacePreset[]).find((p) => same(PRESETS[p], parts)) ?? 'custom';
}

export const withPreset = (preset: InterfacePreset): InterfaceSettings => ({ preset, parts: { ...PRESETS[preset] }, chosen: true });

export function withPart<K extends keyof InterfaceParts>(s: InterfaceSettings, key: K, value: InterfaceParts[K]): InterfaceSettings {
  const parts = { ...s.parts, [key]: value };
  return { preset: presetOf(parts), parts, chosen: true };
}

/** Where Lapin runs, for the first choice: the web build on a window under 800 px tall starts compact. */
export interface InterfaceEnv { web: boolean; height: number }

/** The settings from what was kept (anything unreadable left out), else the default for where Lapin runs. */
export function initialSettings(stored: unknown, env: InterfaceEnv): InterfaceSettings {
  const o = stored as { parts?: Record<string, unknown>; chosen?: unknown } | null;
  if (o && typeof o === 'object' && o.chosen === true && o.parts && typeof o.parts === 'object') {
    const parts = { ...PRESETS.comfortable };
    for (const k of Object.keys(CHOICES) as (keyof InterfaceParts)[]) {
      const v = o.parts[k];
      if ((CHOICES[k] as readonly unknown[]).includes(v)) (parts as Record<string, unknown>)[k] = v;
    }
    return { preset: presetOf(parts), parts, chosen: true };
  }
  const preset: InterfacePreset = env.web && env.height < 800 ? 'compact' : 'comfortable';
  return { preset, parts: { ...PRESETS[preset] }, chosen: false };
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
  try { localStorage.setItem(KEY, JSON.stringify(s)); } catch { /* kept for this session */ }
  for (const f of listeners) f(s, before);
}

export const choosePreset = (p: InterfacePreset) => setInterface(withPreset(p));
export const choosePart = <K extends keyof InterfaceParts>(key: K, value: InterfaceParts[K]) => setInterface(withPart(useInterface.getState(), key, value));
