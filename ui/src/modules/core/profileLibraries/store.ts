/**
 * Where profile libraries live (docs/plan-profile-libraries.md): Lapin's own (the engine's `profiles.builtin`), this
 * computer's (the app's IndexedDB, roomier than localStorage: an AISC table is a few megabytes), a firm's folders on the
 * desktop (local, network or synced; read by the engine host, written where the user allowed it), and the project's copies.
 * `useProfileLibraries` gathers them for the manager and the pickers.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { engine, query } from '../../../engine/session';
import type { LibraryEntry, LibraryJson } from './model';

const DB = 'lapin-profiles';
const STORE = 'libraries';

/** This computer's libraries kept when IndexedDB is not there (a private window, the tests): for the session only. */
const memory = new Map<string, { name: string; added: string; library: LibraryJson }>();

function open(): Promise<IDBDatabase | null> {
  if (typeof indexedDB === 'undefined') return Promise.resolve(null);
  return new Promise((resolve) => {
    try {
      const r = indexedDB.open(DB, 1);
      r.onupgradeneeded = () => { r.result.createObjectStore(STORE, { keyPath: 'name' }); };
      r.onsuccess = () => resolve(r.result);
      r.onerror = () => resolve(null);
    } catch { resolve(null); }
  });
}

async function tx<T>(mode: IDBTransactionMode, run: (s: IDBObjectStore) => IDBRequest<T>): Promise<T | null> {
  const db = await open();
  if (!db) return null;
  return new Promise((resolve) => {
    try {
      const req = run(db.transaction(STORE, mode).objectStore(STORE));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => resolve(null);
    } catch { resolve(null); }
  });
}

/** This computer's libraries, by name. */
export async function computerLibraries(): Promise<{ name: string; added: string; library: LibraryJson }[]> {
  const all = await tx<{ name: string; added: string; library: LibraryJson }[]>('readonly', (s) => s.getAll() as IDBRequest<{ name: string; added: string; library: LibraryJson }[]>);
  return (all ?? [...memory.values()]).sort((a, b) => a.name.localeCompare(b.name));
}

/** A library kept on this computer (one of that name replaced). */
export async function keepOnComputer(library: LibraryJson): Promise<void> {
  const row = { name: library.name, added: new Date().toISOString().slice(0, 10), library };
  if ((await tx('readwrite', (s) => s.put(row))) == null) memory.set(row.name, row);
  useLibraries.getState().bump();
}

export async function forgetOnComputer(name: string): Promise<void> {
  if ((await tx('readwrite', (s) => s.delete(name))) == null) memory.delete(name);
  useLibraries.getState().bump();
}

// ─── A firm's folders (desktop) ─────────────────────────────────────────────

export interface SharedFolder { path: string; writable: boolean }
const FOLDERS = 'lapin.profileFolders';

export function sharedFolders(): SharedFolder[] {
  try { return (JSON.parse(localStorage.getItem(FOLDERS) ?? '[]') as SharedFolder[]).filter((f) => f && typeof f.path === 'string'); } catch { return []; }
}

export function setSharedFolders(folders: SharedFolder[]): void {
  try { localStorage.setItem(FOLDERS, JSON.stringify(folders)); } catch { /* kept for this session */ }
  useLibraries.getState().bump();
}

interface FolderRead { folder: string; libraries: { file: string; modified?: string; library: LibraryJson }[]; problems: string[]; exists: boolean }

/** A folder's libraries as the engine host reads them; none (and why) when it cannot. */
export async function readFolder(path: string): Promise<FolderRead> {
  try { return await engine.call<FolderRead>('profiles.readFolder', { path }); } catch (e) {
    return { folder: path, libraries: [], problems: [e instanceof Error ? e.message : String(e)], exists: false };
  }
}

/** A library written into a writable folder as a .lapinprofiles file (its name from the library's). */
export async function writeToFolder(path: string, library: LibraryJson): Promise<string> {
  const r = await engine.call<{ file: string }>('profiles.writeFolder', { path, library });
  useLibraries.getState().bump();
  return r.file;
}

// ─── Gathered ───────────────────────────────────────────────────────────────

/** A counter the views watch: bumped when a library is kept, forgotten or a folder changes. */
export const useLibraries = create<{ version: number; bump: () => void }>((set) => ({ version: 0, bump: () => set((s) => ({ version: s.version + 1 })) }));

let builtin: Promise<LibraryJson | null> | null = null;
/** Lapin's own library, asked of the engine once. */
export const builtinLibrary = (): Promise<LibraryJson | null> => (builtin ??= query<LibraryJson>('profiles.builtin').then((l) => { if (!l) builtin = null; return l; }));

/** Every library but the project's: Lapin's, this computer's, the folders'. */
export async function allLibraries(withFolders = true): Promise<{ entries: LibraryEntry[]; problems: string[] }> {
  const entries: LibraryEntry[] = [];
  const problems: string[] = [];
  const b = await builtinLibrary();
  if (b) entries.push({ key: 'builtin', origin: 'builtin', name: b.name, library: b });
  for (const c of await computerLibraries()) entries.push({ key: `computer:${c.name}`, origin: 'computer', name: c.name, library: c.library, added: c.added });
  if (withFolders) {
    for (const f of sharedFolders()) {
      const r = await readFolder(f.path);
      problems.push(...r.problems);
      for (const l of r.libraries) entries.push({ key: `folder:${l.file}`, origin: 'folder', name: l.library.name, library: l.library, folder: f.path, file: l.file, writable: f.writable, added: l.modified });
    }
  }
  return { entries, problems };
}

/** The libraries, gathered again when one changes. */
export function useProfileLibraries(withFolders = true): { entries: LibraryEntry[]; problems: string[]; loading: boolean } {
  const version = useLibraries((s) => s.version);
  const [state, setState] = useState<{ entries: LibraryEntry[]; problems: string[]; loading: boolean }>({ entries: [], problems: [], loading: true });
  useEffect(() => {
    let live = true;
    void allLibraries(withFolders).then((r) => { if (live) setState({ ...r, loading: false }); });
    return () => { live = false; };
  }, [version, withFolders]);
  return state;
}
