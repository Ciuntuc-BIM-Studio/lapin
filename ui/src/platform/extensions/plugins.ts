/**
 * The interface halves of extension modules (docs/plan-extension-modules.md §3): loaded once, at boot, before the module
 * list is made (main.tsx awaits them after restoreUserSettings(), then imports boot; platform/modules.ts appends what is
 * here to the product's own modules). This file is the loader's core and the registry; it imports nothing heavy, so
 * modules.ts can read the registry in every edition. The host's shared libraries and the engine call are in host.ts,
 * fetched on the desktop only.
 *
 * Each plug-in comes from the engine (`extensions.modules`), which has verified its files (signature, FILES.sha256):
 * `{id, version, sdk, js, css?, phrases?}`. Its `js` is one ES module whose default export is a LapinModule (or a function
 * given the SDK that returns one). A plug-in that asks for an SDK this app does not have, that throws while it loads or
 * registers, or whose module is not one, is left out with the reason, and boot goes on.
 */

import { create } from 'zustand';
import type { LapinModule } from '../module';
import { sdkProblem } from '../sdkVersion';

/** One extension's interface files, as the engine serves them (design §3.1). */
export interface PluginFiles {
  id: string;
  version: string;
  /** The SDK it was built against, from its manifest ("1.x", or "1.2": that minor or later). */
  sdk: string;
  /** plugin.js, the text. */
  js: string;
  /** plugin.css, the text. */
  css?: string | null;
  /** ui/i18n/<lang>.json by language: the table, or its JSON text. */
  phrases?: Record<string, unknown> | null;
}

/** A phrase table of a plug-in's language (ui/i18n/<lang>.json). */
export interface PhraseTable { phrases?: Record<string, string>; patterns?: [string, string][] }

export interface PluginLoaded { id: string; version: string; module: string }
export interface PluginLeftOut { id: string; version: string; reason: string }

export interface PluginDeps {
  /** Evaluates a plug-in's code as an ES module (a blob: URL in the app). */
  importModule(js: string): Promise<unknown>;
  /** The ids of the modules already there (the product's), which a plug-in may not take. */
  taken: readonly string[];
  /** Puts a plug-in's stylesheet on the page. */
  addStyle?(id: string, css: string): void;
  /** Merges a language's phrases into the translator. */
  addPhrases?(lang: string, table: PhraseTable): void;
  /** What a plug-in's default export is called with when it is a function. */
  sdk?: unknown;
}

const text = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Why `m` cannot be taken as a module; null when it can. */
function moduleProblem(m: unknown): string | null {
  if (!m || typeof m !== 'object') return 'its interface has no module (default export)';
  const x = m as Record<string, unknown>;
  if (typeof x.id !== 'string' || !/^[a-z][a-z0-9.-]*$/.test(x.id)) return 'its module has no valid id';
  if (typeof x.name !== 'string') return 'its module has no name';
  for (const k of ['tabs', 'hotbar', 'propertySections', 'overlays', 'overlays3d', 'planOverlays', 'drawingOverlays', 'statusItems', 'placeSources', 'legacyTools']) {
    if (x[k] !== undefined && !Array.isArray(x[k])) return `its module's ${k} is not a list`;
  }
  for (const k of ['views', 'dialogs', 'grips', 'grips3d', 'references', 'similar', 'setType', 'editType', 'typeFields', 'sharedGroups', 'subSelectionPanels', 'subSelectionPick', 'exports']) {
    if (x[k] !== undefined && (typeof x[k] !== 'object' || x[k] === null || Array.isArray(x[k]))) return `its module's ${k} is not a table`;
  }
  return null;
}

/** A phrase table from what the engine sent (the object, or its JSON text); null when it is neither. */
function tableOf(v: unknown): PhraseTable | null {
  let t = v;
  if (typeof t === 'string') { try { t = JSON.parse(t); } catch { return null; } }
  return t && typeof t === 'object' && !Array.isArray(t) ? t as PhraseTable : null;
}

/** Loads each plug-in in turn: what loaded, and what was left out and why. Never throws. */
export async function loadPlugins(list: readonly PluginFiles[], deps: PluginDeps): Promise<{ modules: LapinModule[]; loaded: PluginLoaded[]; left: PluginLeftOut[] }> {
  const modules: LapinModule[] = [];
  const loaded: PluginLoaded[] = [];
  const left: PluginLeftOut[] = [];
  const taken = new Set(deps.taken);
  const seen = new Set<string>();
  for (const p of list) {
    const id = typeof p?.id === 'string' ? p.id : '?';
    const version = typeof p?.version === 'string' ? p.version : '?';
    const leave = (reason: string) => left.push({ id, version, reason });
    if (id === '?' || seen.has(id)) { leave(id === '?' ? 'it has no id' : 'it is listed twice'); continue; }
    seen.add(id);
    const sdk = sdkProblem(p.sdk);
    if (sdk) { leave(sdk); continue; }
    if (typeof p.js !== 'string' || !p.js.trim()) { leave('it has no interface code'); continue; }
    let m: unknown;
    try {
      const ns = await deps.importModule(p.js) as { default?: unknown } | null;
      m = ns?.default;
    } catch (e) { leave(`its interface did not load: ${text(e)}`); continue; }
    if (typeof m === 'function') {
      try { m = await (m as (sdk: unknown) => unknown)(deps.sdk); } catch (e) { leave(`its interface failed to register: ${text(e)}`); continue; }
    }
    const bad = moduleProblem(m);
    if (bad) { leave(bad); continue; }
    const mod = m as LapinModule;
    // The module's id is the last part of the extension's (ro.ciuntucbimstudio.planning-plus → planning-plus), as the
    // catalogue's validator requires (manifest.ts, moduleIdOf): its CSS classes and its commands are named after it.
    const want = id.slice(id.lastIndexOf('.') + 1);
    if (mod.id !== want) { leave(`its module id "${mod.id}" is not "${want}", the last part of the extension's id`); continue; }
    if (taken.has(mod.id)) { leave(`its module id "${mod.id}" is taken`); continue; }
    taken.add(mod.id);
    // Only now, with the module taken: a plug-in left out leaves nothing behind (no phrases, no styles).
    if (p.phrases && typeof p.phrases === 'object') {
      for (const [lang, raw] of Object.entries(p.phrases)) {
        const table = tableOf(raw);
        if (table) { try { deps.addPhrases?.(lang, table); } catch { /* a bad table loses its phrases only */ } }
      }
    }
    if (typeof p.css === 'string' && p.css.trim()) { try { deps.addStyle?.(id, p.css); } catch { /* the plug-in stays, unstyled */ } }
    modules.push(mod);
    loaded.push({ id, version, module: mod.id });
  }
  return { modules, loaded, left };
}

/** Whether this window loads plug-ins: never in the browser edition (its engine is trimmed, design §2.3), nor as a shared session's guest. */
export const pluginsWanted = (env: { inBrowser: boolean; guest: boolean }): boolean => !env.inBrowser && !env.guest;

// ------------------------------------------------------------ the registry

interface PluginReport { loaded: PluginLoaded[]; left: PluginLeftOut[]; done: boolean }

/** What boot loaded and left out (Extensions → Installed says why one was left out). */
export const usePluginReport = create<PluginReport>(() => ({ loaded: [], left: [], done: false }));

let registered: LapinModule[] = [];

/** The extensions' modules, for modules.ts to append to the product's (read once, when it is evaluated). */
export const extensionModules = (): readonly LapinModule[] => registered;

/** Keeps what boot loaded (called once, before modules.ts is evaluated). */
export function setPlugins(result: { modules: LapinModule[]; loaded: PluginLoaded[]; left: PluginLeftOut[] }): void {
  registered = result.modules;
  usePluginReport.setState({ loaded: result.loaded, left: result.left, done: true });
}
