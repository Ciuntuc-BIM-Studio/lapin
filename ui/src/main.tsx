/**
 * The interface's entry. On the desktop the user's settings kept in their folder go back into the page's storage first
 * (app/userSettings.ts, decision 470): the app's modules read that storage as they load, so they load after it (boot.tsx).
 * Then, on the desktop only, the interface plug-ins of the extensions the engine loaded (platform/extensions/host.ts,
 * docs/plan-extension-modules.md §3.1): before boot, because the module list is made once, when boot imports it. The
 * browser edition loads none, and its build leaves the loader out (the condition is a constant there).
 */
import { restoreUserSettings } from './app/userSettings';

const plugins = (): Promise<void> => (import.meta.env.VITE_ENGINE === 'worker'
  ? Promise.resolve()
  : import('./platform/extensions/host').then((h) => h.loadExtensionPlugins()));

void restoreUserSettings()
  .catch(() => undefined)
  .then(plugins)
  .catch((e: unknown) => { console.warn('Extensions: their interfaces were not loaded:', e); })
  .finally(() => import('./boot'));
