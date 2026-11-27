/**
 * The interface plug-ins of extension modules, loaded at boot (docs/plan-extension-modules.md §3.1). Fetched by main.tsx
 * on the desktop only (and pnpm dev), never in the browser edition, whose engine loads no extensions (design §2.3).
 *
 * One call to the engine, `extensions.modules`, on a connection of its own: the extensions it loaded, with the texts of
 * their verified files. The engine is the only verifier (signature, FILES.sha256); nothing is read from disk here. With
 * none installed (an older engine has no such call: none), nothing else is fetched and boot goes on as before; else the
 * host's shared libraries come (hostLibs.ts) and the plug-ins are loaded.
 */

import { EngineClient, engineUrl } from '../../engine/client';
import { pluginsWanted, setPlugins, type PluginFiles } from './plugins';

/** How long the first call waits for the engine (it may still be starting): past it, the app starts without plug-ins. */
export const ENGINE_WAIT_MS = 15_000;

/** What `extensions.modules` answered, as a list (the list itself, or `{modules}`); anything else: none. */
export function pluginList(r: unknown): PluginFiles[] {
  const list = Array.isArray(r) ? r : (r as { modules?: unknown } | null)?.modules;
  return Array.isArray(list) ? list.filter((x): x is PluginFiles => !!x && typeof x === 'object') : [];
}

/** The extensions the engine loaded. */
async function askEngine(): Promise<PluginFiles[]> {
  const client = new EngineClient(engineUrl);
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    const deadline = new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new Error('the engine did not answer')), ENGINE_WAIT_MS); });
    await Promise.race([client.start(), deadline]);
    return pluginList(await Promise.race([client.call<unknown>('extensions.modules', {}, { timeoutMs: ENGINE_WAIT_MS }), deadline]));
  } finally {
    clearTimeout(timer);
    client.stop();
  }
}

/** Loads the plug-ins of the extensions the engine loaded, before the module list is made. Never throws. */
export async function loadExtensionPlugins(): Promise<void> {
  const none = () => setPlugins({ modules: [], loaded: [], left: [] });
  if (!pluginsWanted({ inBrowser: import.meta.env.VITE_ENGINE === 'worker', guest: false })) { none(); return; }
  let list: PluginFiles[] = [];
  try { list = await askEngine(); } catch (e) { console.warn('Extensions: their interfaces were not asked for:', e); }
  if (!list.length) { none(); return; }
  try {
    const { installPlugins } = await import('./hostLibs');
    await installPlugins(list);
  } catch (e) { console.warn('Extensions: their interfaces were not loaded:', e); none(); }
}
