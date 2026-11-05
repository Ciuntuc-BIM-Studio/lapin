/**
 * File → Optimize IFC… (docs/ifc-optimize.md): IFC files cleaned and lightened into copies, a pass from file to file in
 * the engine (`ifc.optimize`), no project needed. The copy keeps every GlobalId, name, property and quantity set as the
 * file wrote them; only its geometry changes. Its name is the original's with an ending (`.optimized` by default,
 * remembered with the options on this computer); the original is never written. On the desktop by path (the copy
 * beside it, or in a folder chosen), in a browser by the file's bytes (the copy downloaded).
 */

import { create } from 'zustand';
import { useUi } from '../app/store';

/** One file to optimise: by path (the desktop) or its bytes as base64 (a browser). */
export interface OptimizeSource { name: string; path?: string; data?: string }

/** How precise a simplified body stays, like View → Display quality: the most it may stray, in mm. */
export type OptimizePreset = 'fast' | 'balanced' | 'precise' | 'custom';
export const PRESET_MM: Record<Exclude<OptimizePreset, 'custom'>, number> = { fast: 5, balanced: 1, precise: 0.1 };

/** The choices a user made last, kept on this computer. */
export interface OptimizeChoice {
  preset: OptimizePreset; toleranceMm: string;
  simplify: boolean; clean: boolean; share: boolean; orphans: boolean;
  round: boolean; brepsToFaceSets: boolean; zip: boolean;
  suffix: string;
}

export const DEFAULT_SUFFIX = '.optimized';

export const DEFAULT_CHOICE: OptimizeChoice = {
  preset: 'balanced', toleranceMm: '1', simplify: true, clean: true, share: true, orphans: true, round: false, brepsToFaceSets: false, zip: false, suffix: DEFAULT_SUFFIX,
};

const KEY = 'lapin.optimizeIfc';

export function rememberedChoice(): OptimizeChoice {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? { ...DEFAULT_CHOICE, ...(JSON.parse(raw) as Partial<OptimizeChoice>) } : DEFAULT_CHOICE;
  } catch { return DEFAULT_CHOICE; }
}

export function rememberChoice(c: OptimizeChoice): void {
  try { localStorage.setItem(KEY, JSON.stringify(c)); } catch { /* kept for this time only */ }
}

/** The tolerance in mm the choice stands for: a preset's, or the number typed (null when it is not one). */
export function toleranceOf(c: OptimizeChoice): number | null {
  if (c.preset !== 'custom') return PRESET_MM[c.preset];
  const v = Number(c.toleranceMm.replace(',', '.'));
  return Number.isFinite(v) && v > 0 && v <= 100 ? v : null;
}

/** The options as the engine takes them (`ifc.optimize`'s `options`). */
export function optionsOf(c: OptimizeChoice): Record<string, unknown> {
  return {
    simplify: c.simplify, toleranceMm: toleranceOf(c) ?? 1, clean: c.clean, share: c.share, orphans: c.orphans,
    roundMm: c.round ? 0.01 : 0, brepsToFaceSets: c.brepsToFaceSets, zip: c.zip,
  };
}

/** An ending as the engine keeps it: no folder, no extension of its own; null when nothing is left. */
export function cleanSuffix(s: string): string | null {
  const t = s.trim().replace(/[\\/:*?"<>|]/g, '').replace(/\.ifc$/i, '');
  return t.length ? t : null;
}

/** The copy's name, as the engine makes it: the original's with the ending before its extension. */
export function copyName(name: string, suffix: string, zip: boolean): string {
  const stem = name.replace(/\.ifczip$/i, '').replace(/\.ifc$/i, '');
  return `${stem}${cleanSuffix(suffix) ?? DEFAULT_SUFFIX}${zip ? '.ifczip' : '.ifc'}`;
}

/** A file's name from its path. */
export const fileName = (path: string) => path.split(/[\\/]/).pop() ?? path;

/** The folder a path is in. */
export const folderOf = (path: string) => path.replace(/[\\/][^\\/]*$/, '');

/** What one file's pass did, as the engine reports it. */
export interface OptimizeReport {
  schema: string; bytesBefore: number; bytesAfter: number; entitiesBefore: number; entitiesAfter: number;
  trianglesBefore: number; trianglesAfter: number; bodies: number; simplified: number; cleaned: number;
  shared: number; sharedBodies: number; mapsMerged: number; orphans: number; converted: number; kept: number;
  deviationMm: number; seconds: number; notes: string[];
  source?: string; target?: string; name?: string; data?: string;
}

/** A size as people read it: bytes, kB, MB, GB. */
export function sizeText(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['kB', 'MB', 'GB'];
  let v = bytes / 1024;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
  return `${v >= 100 ? v.toFixed(0) : v.toFixed(1)} ${units[i]}`;
}

/** Before → after, with the change as a share: "2.8 MB → 2.2 MB (−20 %)". */
export function change(before: number, after: number, text: (n: number) => string = (n) => n.toLocaleString('en-US')): string {
  const pct = before > 0 ? Math.round(((after - before) / before) * 100) : 0;
  return `${text(before)} → ${text(after)} (${pct > 0 ? '+' : pct < 0 ? '−' : '±'}${Math.abs(pct)} %)`;
}

/** One file in the dialog: waiting, running (its stage and how far), done (its report) or failed (why). */
export interface OptimizeItem {
  source: OptimizeSource;
  state: 'waiting' | 'running' | 'done' | 'failed' | 'cancelled';
  stage?: string; done?: number; total?: number;
  report?: OptimizeReport; error?: string;
}

interface OptimizeState { items: OptimizeItem[]; folder: string | null; running: boolean }

export const useOptimize = create<OptimizeState>(() => ({ items: [], folder: null, running: false }));

/** The dialog opened with these files (none: the user adds them there). */
export function openOptimize(sources: OptimizeSource[] = []): void {
  useOptimize.setState({ items: sources.map((source) => ({ source, state: 'waiting' })), running: false });
  useUi.setState({ dialog: 'optimizeIfc' });
}

/** Files added to the list, each once (by path, or by name for a browser's). */
export function addSources(sources: OptimizeSource[]): void {
  const { items } = useOptimize.getState();
  const key = (s: OptimizeSource) => s.path ?? `bytes:${s.name}`;
  const have = new Set(items.map((i) => key(i.source)));
  useOptimize.setState({ items: [...items, ...sources.filter((s) => !have.has(key(s))).map((source) => ({ source, state: 'waiting' as const }))] });
}
