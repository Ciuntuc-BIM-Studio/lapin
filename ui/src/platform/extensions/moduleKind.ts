/**
 * Extension modules in the app (docs/plan-extension-modules.md, S2): the kind `module`, signed C# engine modules with an interface
 * plug-in, installed by the desktop's engine (`extensions.inspect`, `extensions.install`, `uninstall`, `enable`, `disable`,
 * `modules`), which checks the package whole (the store's signature, every file's SHA-256) and owns
 * `<user-dir>/extensions/modules.json`. The interface only downloads the package (install.ts, as for any kind), shows its licence
 * and, when it has one, its licence agreement to accept, and lists what the engine found at its start. Every change takes effect
 * when Lapin restarts: Restart now saves (or lets go) the work, stops the engine and starts the app again, as an update does.
 * The browser edition has none of this (its engine is built in and trimmed): Extensions shows them as desktop only.
 */

import { create } from 'zustand';
import { engine, engineInBrowser } from '../../engine/session';
import { toBase64 } from '../../engine/files';
import { phrase } from '../../shell/prompts';
import type { ContentInstaller, Receipt } from './install';
import type { ExtensionKind, Manifest } from './manifest';

/** The kind's id (manifest.ts lists it from S1 on). */
export const MODULE_KIND = 'module' as ExtensionKind;
export const isModuleKind = (kind: string): boolean => kind === MODULE_KIND;

/** The permission line every extension module shows: nothing restricts its code (§4.2). */
export const FULL_ACCESS = 'Runs code with full access: it can read and change your files, use the network and start programs, as you can.';

/** One installed extension module, as the engine lists it (`extensions.modules`, `installed`). */
export interface EngineExtension {
  id: string;
  version?: string;
  name?: string;
  /** What this start did: loaded, disabled (by the user), failed (refused: `problem`), pending (installed since). */
  status: 'loaded' | 'disabled' | 'failed' | 'pending';
  enabled: boolean;
  pendingRemoval: boolean;
  /** The next start would change something for it. */
  restart?: boolean;
  nextVersion?: string;
  problem?: string;
  /** What kind of refusal `problem` is: "revoked" (the catalogue withdrew this version). */
  problemKind?: string;
  /** The revocation list in force withdraws this version (docs/plan-extension-modules.md §4.3): why, and since when. */
  revoked?: { reason: string; date: string };
  license?: string;
  module?: string;
}

export interface ModulesAnswer { contract: string; restart: boolean; installed: EngineExtension[] }

/** What the engine says of the installed extension modules; none in the browser, or from an engine without them. */
export async function engineModules(): Promise<ModulesAnswer> {
  if (engineInBrowser) return { contract: '', restart: false, installed: [] };
  try {
    const r = await engine.call<Partial<ModulesAnswer>>('extensions.modules', {});
    return { contract: r.contract ?? '', restart: !!r.restart, installed: Array.isArray(r.installed) ? r.installed : [] };
  } catch { return { contract: '', restart: false, installed: [] }; }
}

/** The engine's list, kept for Extensions → Installed. */
export const useEngineModules = create<ModulesAnswer & { read: boolean }>(() => ({ contract: '', restart: false, installed: [], read: false }));
export async function refreshEngineModules(): Promise<void> {
  useEngineModules.setState({ ...(await engineModules()), read: true });
}

/**
 * What Installed says of an extension module beside its switch: why it was refused at this start, why its interface was left out
 * (`leftReason`), or what the next start will change; null when it simply runs. `detail` is the engine's or the loader's own
 * sentence, shown as it came.
 */
export function moduleStatus(e: EngineExtension | undefined, leftReason?: string): { text: string; detail?: string; problem: boolean } | null {
  if (!e) return leftReason ? { text: 'Its interface was left out:', detail: leftReason, problem: true } : null;
  if (e.pendingRemoval) return { text: 'Removed when Lapin restarts.', problem: false };
  // Withdrawn by the catalogue: refused at this start, or (the list read since) at the next one.
  if (e.status === 'failed' && (e.problemKind === 'revoked' || e.revoked)) return { text: 'Withdrawn by Lapin Extensions:', detail: e.revoked?.reason ?? e.problem, problem: true };
  if (e.revoked && e.status === 'loaded') return { text: 'Withdrawn by Lapin Extensions; it is turned off when Lapin restarts:', detail: e.revoked.reason, problem: true };
  if (e.status === 'failed') return { text: 'Not loaded:', detail: e.problem, problem: true };
  if (e.status === 'pending') return { text: 'Loads when Lapin restarts.', problem: false };
  if (e.nextVersion) return { text: 'The new version loads when Lapin restarts:', detail: e.nextVersion, problem: false };
  if (e.status === 'disabled') return { text: e.enabled ? 'Turns on when Lapin restarts.' : 'Off.', problem: false };
  if (!e.enabled) return { text: 'Turns off when Lapin restarts.', problem: false };
  if (leftReason) return { text: 'Its interface was left out:', detail: leftReason, problem: true };
  return null;
}

/** Turns an installed extension module on or off from the next start. */
export async function setModuleEnabled(id: string, on: boolean): Promise<void> {
  await engine.call(on ? 'extensions.enable' : 'extensions.disable', { id });
  await refreshEngineModules();
}

/** What a click-through shows: the licence, and the agreement when there is one (null: the user said no). */
export interface Agreement { license: string; licenseText?: string | null; eula?: { version: string; text?: string | null } | null }

/**
 * Whether the user accepts an extension module's terms: the permission, its licence and, when it has one, its licence agreement
 * (I accept / Cancel). Pure: the question is asked through `ask`.
 */
export function acceptTerms(m: Pick<Manifest, 'name'>, a: Agreement, ask: (text: string) => boolean): boolean {
  const licence = `${m.name}: ${phrase(FULL_ACCESS)}\n\n${phrase('Licence:')} ${a.license}${a.licenseText ? `\n\n${a.licenseText.trim()}` : ''}`;
  if (!ask(licence)) return false;
  if (a.eula) return ask(`${phrase('Licence agreement of {name} (version {version}):', { name: m.name, version: a.eula.version })}\n\n${(a.eula.text ?? '').trim()}\n\n${phrase('I accept')}`);
  return true;
}

/** The installer of the kind: the engine checks and unpacks, after the user has accepted the terms. */
export const moduleInstaller: ContentInstaller = {
  kind: MODULE_KIND,
  install: async (file, m) => {
    if (engineInBrowser) throw new Error('Extension modules run in the desktop app only.');
    const args = { manifest: m, data: toBase64(file.bytes) };
    const seen = await engine.call<{ license: string; licenseText?: string | null; eula?: { version: string; text?: string | null } | null; problem?: string }>('extensions.inspect', args, { timeoutMs: 120_000 });
    if (seen.problem) throw new Error(seen.problem);
    if (!acceptTerms(m, seen, (q) => window.confirm(q))) throw new Error('Not installed: the terms were not accepted.');
    const eulaAccepted = seen.eula?.version;
    await engine.call('extensions.install', { ...args, ...(eulaAccepted ? { eulaAccepted } : {}) }, { timeoutMs: 120_000 });
    await refreshEngineModules();
    const receipt: Receipt = { module: m.id, ...(eulaAccepted ? { eula: eulaAccepted, acceptedAt: new Date().toISOString() } : {}) };
    return receipt;
  },
  uninstall: async (r) => {
    if (typeof r.module !== 'string') return;
    await engine.call('extensions.uninstall', { id: r.module }).catch(() => undefined);
    await refreshEngineModules();
  },
};

/**
 * Lapin started again, so what was installed, updated, turned on or off or uninstalled takes effect: the work saved or let go
 * first (`letWorkGo`), then the engine stopped and the app relaunched, as an update does (app/updates.ts). The desktop only;
 * false when the user kept their work open.
 */
export async function restartNow(letWorkGo: () => Promise<boolean>): Promise<boolean> {
  if (engineInBrowser) return false;
  if (!(await letWorkGo())) return false;
  const { invoke } = await import('@tauri-apps/api/core');
  const { relaunch } = await import('@tauri-apps/plugin-process');
  await invoke('stop_engine');
  await relaunch();
  return true;
}
