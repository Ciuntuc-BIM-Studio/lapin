/**
 * The user's settings kept where they survive a restart on every system (issue #20, decision 470). The interface keeps its
 * preferences in the page's localStorage (`lapin.*`: Preferences, panels, shortcuts, the language, the libraries of title
 * blocks, documents and prices…). In the desktop app that storage belongs to the system's webview, and on Linux
 * (WebKitGTK) it writes to disk late and is not flushed when the app quits, so what was changed shortly before closing
 * could be gone at the next start. The desktop therefore keeps a copy of every `lapin.*` key in the user's folder
 * (`settings.json` in the app's data folder, beside the recovery journals and the project templates), written by the shell
 * (`user_settings_write`) a moment after each change and when the window closes, and put back into the page's storage
 * before anything reads it (`restoreUserSettings`, awaited by main.tsx before the app's modules load). In a browser the
 * page's own storage is the only one: nothing to do.
 */

/** The keys kept: the interface's own. */
export const PREFIX = 'lapin.';

/** The file's values against the page's: what to put into the page (the file wins: it is what was last written for sure) and what the file lacks (kept the first time, from a build before the file). */
export function restorePlan(file: Record<string, string>, local: Record<string, string>): { put: Record<string, string>; missing: Record<string, string> } {
  const put: Record<string, string> = {};
  const missing: Record<string, string> = {};
  for (const [k, v] of Object.entries(file)) if (k.startsWith(PREFIX) && local[k] !== v) put[k] = v;
  for (const [k, v] of Object.entries(local)) if (k.startsWith(PREFIX) && !(k in file)) missing[k] = v;
  return { put, missing };
}

/** The changes waiting to be written: the last value of each key set, the keys removed. */
export class PendingWrites {
  set: Record<string, string> = {};
  remove = new Set<string>();

  put(key: string, value: string): void { this.set[key] = value; this.remove.delete(key); }
  drop(key: string): void { delete this.set[key]; this.remove.add(key); }
  get empty(): boolean { return Object.keys(this.set).length === 0 && this.remove.size === 0; }

  /** What to write, the queue emptied. */
  take(): { set: Record<string, string>; remove: string[] } {
    const out = { set: this.set, remove: [...this.remove] };
    this.set = {};
    this.remove = new Set();
    return out;
  }
}

type Invoke = <T>(cmd: string, args?: Record<string, unknown>) => Promise<T>;

const isDesktop = () => Boolean((window as unknown as { __TAURI_INTERNALS__?: unknown }).__TAURI_INTERNALS__);

let pending: PendingWrites | null = null;
let timer: ReturnType<typeof setTimeout> | null = null;
let invoker: Invoke | null = null;

/** The waiting changes written now (the window closing, or a moment after a change). */
export async function flushUserSettings(): Promise<void> {
  if (timer) { clearTimeout(timer); timer = null; }
  if (!pending || pending.empty || !invoker) return;
  const { set, remove } = pending.take();
  try { await invoker('user_settings_write', { set, remove }); } catch (e) { console.warn('[settings] not written', e); }
}

const later = () => { if (!timer) timer = setTimeout(() => void flushUserSettings(), 400); };

/** The page's `lapin.*` keys. */
function localKeys(): Record<string, string> {
  const o: Record<string, string> = {};
  for (let i = 0; i < localStorage.length; i++) {
    const k = localStorage.key(i);
    if (k?.startsWith(PREFIX)) o[k] = localStorage.getItem(k) ?? '';
  }
  return o;
}

/**
 * The desktop's settings back into the page's storage, then every change to a `lapin.*` key followed into the user's
 * folder. Awaited before the app's modules load (they read the storage as they load). Never fails the start: without the
 * file the page's storage is what there is.
 */
export async function restoreUserSettings(): Promise<void> {
  if (!isDesktop()) return;
  try {
    const { invoke } = await import('@tauri-apps/api/core');
    invoker = invoke as Invoke;
    const file = await invoke<Record<string, string>>('user_settings_read');
    const { put, missing } = restorePlan(file ?? {}, localKeys());
    for (const [k, v] of Object.entries(put)) localStorage.setItem(k, v);
    pending = new PendingWrites();
    for (const [k, v] of Object.entries(missing)) pending.put(k, v);
    const storage = Object.getPrototypeOf(localStorage) as Storage;
    const setItem = storage.setItem;
    const removeItem = storage.removeItem;
    storage.setItem = function (this: Storage, key: string, value: string) {
      setItem.call(this, key, value);
      if (this === localStorage && key.startsWith(PREFIX)) { pending?.put(key, String(value)); later(); }
    };
    storage.removeItem = function (this: Storage, key: string) {
      removeItem.call(this, key);
      if (this === localStorage && key.startsWith(PREFIX)) { pending?.drop(key); later(); }
    };
    if (!pending.empty) later();
    window.addEventListener('pagehide', () => void flushUserSettings());
  } catch (e) {
    console.warn('[settings] the desktop copy could not be read', e);
  }
}
