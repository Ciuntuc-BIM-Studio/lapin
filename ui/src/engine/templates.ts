/**
 * Project templates (issues #9 and #20, decision 470): File → Save as template… keeps the open project's set-up (its types,
 * materials, units and drafting standards, storeys, views, sheets and title blocks; no element) on this computer, and a new
 * project starts from one (File → New from template…, the start screen, or File → New when Preferences make it the default).
 * Lapin's own start ("Lapin default") stays first and is what New makes when no template is chosen.
 *
 * Where they live: on the desktop, .lapin files in the user's folder, kept by the engine (`templates.*`); in the browser,
 * the page's IndexedDB, as the bytes the engine makes (`template.make`) and starts from (`project.newFromTemplate`). Either
 * way a template goes out as a .lapin file and comes back from one, and any project's file serves as a template.
 */

import { create } from 'zustand';
import { engine, engineInBrowser } from './session';
import { LONG_TIMEOUT_MS } from './client';

export interface TemplateInfo { id: string; name: string; at: string; size: number }

/** A template's name as its id (and its file's name on the desktop): what the systems accept, as the engine makes it. */
export function templateId(name: string): string {
  let s = [...name.trim()].map((c) => (/[\\/:*?"<>|]/.test(c) || c.charCodeAt(0) < 32 ? '_' : c)).join('').trim().replace(/^\.+/, '');
  if (s.length > 80) s = s.slice(0, 80).trim();
  return s;
}

/** The templates by name, as a person reads a list. */
export const sortTemplates = (list: TemplateInfo[]) => [...list].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

/** The template File → New starts from: the one Preferences chose, while it is still there; null: Lapin's default. */
export const defaultTemplateOf = (chosen: string | null, list: TemplateInfo[]) => (chosen ? list.find((t) => t.id === chosen) ?? null : null);

/** One template's row in the browser's store. */
export interface TemplateRow extends TemplateInfo { data: string }

/** Where the browser keeps its rows (IndexedDB; a map in the tests or where IndexedDB is not there). */
export interface TemplateRows {
  all(): Promise<TemplateRow[]>;
  put(row: TemplateRow): Promise<void>;
  remove(id: string): Promise<void>;
}

const bytesOf = (base64: string) => Math.floor((base64.length * 3) / 4);

/** The browser's library, over its rows: the same answers the desktop's engine gives. */
export class BrowserTemplates {
  constructor(private rows: TemplateRows) {}

  async list(): Promise<TemplateInfo[]> {
    return sortTemplates((await this.rows.all()).map(({ id, name, at, size }) => ({ id, name, at, size })));
  }

  async data(id: string): Promise<string> {
    const row = (await this.rows.all()).find((r) => r.id === id);
    if (!row) throw new Error(`There is no template „${id}”.`);
    return row.data;
  }

  /** A template kept (its id), or 'exists' when one has the name and replacing it was not asked. */
  async keep(name: string, data: string, overwrite: boolean): Promise<string | 'exists'> {
    const id = templateId(name);
    if (!id) throw new Error('Give the template a name.');
    if (!overwrite && (await this.rows.all()).some((r) => r.id === id)) return 'exists';
    await this.rows.put({ id, name: id, at: new Date().toISOString(), size: bytesOf(data), data });
    return id;
  }

  async rename(id: string, name: string): Promise<string> {
    const to = templateId(name);
    if (!to) throw new Error('Give the template a name.');
    const all = await this.rows.all();
    const row = all.find((r) => r.id === id);
    if (!row) throw new Error(`There is no template „${id}”.`);
    if (to === id) return id;
    if (all.some((r) => r.id === to)) throw new Error(`A template is already called „${to}”.`);
    await this.rows.put({ ...row, id: to, name: to });
    await this.rows.remove(id);
    return to;
  }

  remove(id: string): Promise<void> { return this.rows.remove(id); }
}

/** The rows in the page's IndexedDB (`lapin-templates`), in memory where there is none. */
function indexedRows(): TemplateRows {
  const memory = new Map<string, TemplateRow>();
  const DB = 'lapin-templates', STORE = 'templates';
  const open = (): Promise<IDBDatabase | null> => new Promise((resolve) => {
    if (typeof indexedDB === 'undefined') { resolve(null); return; }
    try {
      const r = indexedDB.open(DB, 1);
      r.onupgradeneeded = () => { r.result.createObjectStore(STORE, { keyPath: 'id' }); };
      r.onsuccess = () => resolve(r.result);
      r.onerror = () => resolve(null);
    } catch { resolve(null); }
  });
  async function tx<T>(mode: IDBTransactionMode, run: (s: IDBObjectStore) => IDBRequest<T>): Promise<T | undefined> {
    const db = await open();
    if (!db) return undefined;
    return new Promise((resolve, reject) => {
      try {
        const req = run(db.transaction(STORE, mode).objectStore(STORE));
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(new Error('The browser did not keep the template (its storage may be full).'));
      } catch (e) { reject(e instanceof Error ? e : new Error(String(e))); }
    });
  }
  return {
    all: async () => (await tx<TemplateRow[]>('readonly', (s) => s.getAll() as IDBRequest<TemplateRow[]>)) ?? [...memory.values()],
    put: async (row) => { if ((await tx('readwrite', (s) => s.put(row))) === undefined) memory.set(row.id, row); },
    remove: async (id) => { if ((await tx('readwrite', (s) => s.delete(id))) === undefined) memory.delete(id); },
  };
}

let browser: BrowserTemplates | null = null;
const inBrowser = () => (browser ??= new BrowserTemplates(indexedRows()));

/** The list the dialogs and the start screen show, read again after each change. */
export const useTemplates = create<{ list: TemplateInfo[]; read: boolean }>(() => ({ list: [], read: false }));

export async function refreshTemplates(): Promise<TemplateInfo[]> {
  const list = engineInBrowser ? await inBrowser().list() : sortTemplates(await engine.call<TemplateInfo[]>('templates.list').catch(() => [] as TemplateInfo[]));
  useTemplates.setState({ list, read: true });
  return list;
}

/** The open project kept as a template: its id, or 'exists' (nothing written) when one has the name and replacing it was not asked. */
export async function saveAsTemplate(name: string, overwrite = false): Promise<string | 'exists'> {
  let r: string | 'exists';
  if (engineInBrowser) {
    const made = await engine.call<{ data: string }>('template.make', undefined, { timeoutMs: LONG_TIMEOUT_MS });
    r = await inBrowser().keep(name, made.data, overwrite);
  } else {
    const o = await engine.call<{ id?: string; exists?: boolean }>('templates.save', { name, overwrite }, { timeoutMs: LONG_TIMEOUT_MS });
    r = o.exists ? 'exists' : o.id!;
  }
  await refreshTemplates();
  return r;
}

/** A new project from a template of the library. */
export async function startFromTemplate(id: string): Promise<void> {
  const params = engineInBrowser ? { data: await inBrowser().data(id) } : { id };
  await engine.call('project.newFromTemplate', params, { timeoutMs: LONG_TIMEOUT_MS });
}

/** A new project from a .lapin file (a template exported, or any project): its set-up, no model. */
export async function startFromTemplateFile(file: { path?: string; data?: string }): Promise<void> {
  await engine.call('project.newFromTemplate', file, { timeoutMs: LONG_TIMEOUT_MS });
}

export async function deleteTemplate(id: string): Promise<void> {
  if (engineInBrowser) await inBrowser().remove(id);
  else await engine.call('templates.delete', { id });
  await refreshTemplates();
}

export async function renameTemplate(id: string, name: string): Promise<string> {
  const to = engineInBrowser ? await inBrowser().rename(id, name) : (await engine.call<{ id: string }>('templates.rename', { id, name })).id;
  await refreshTemplates();
  return to;
}

/** A file taken in as a template (by its path on the desktop, its bytes in the browser): its id, or 'exists'. */
export async function importTemplate(file: { path?: string; data?: string }, name: string, overwrite = false): Promise<string | 'exists'> {
  let r: string | 'exists';
  if (engineInBrowser) {
    // The page keeps what the engine makes of the file: its set-up only, whatever the file held; the open project is left alone.
    const made = await engine.call<{ data: string }>('template.make', { data: file.data }, { timeoutMs: LONG_TIMEOUT_MS });
    r = await inBrowser().keep(name, made.data, overwrite);
  } else {
    const o = await engine.call<{ id?: string; exists?: boolean }>('templates.import', { ...file, name, overwrite }, { timeoutMs: LONG_TIMEOUT_MS });
    r = o.exists ? 'exists' : o.id!;
  }
  await refreshTemplates();
  return r;
}

/** A template's bytes (base64), to export in the browser. */
export const templateData = (id: string) => inBrowser().data(id);
