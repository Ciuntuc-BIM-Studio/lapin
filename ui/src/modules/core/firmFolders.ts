/**
 * A firm's folders for every library (docs/plan-user-extensibility.md §4.2; desktop): the list the profile libraries already
 * kept (`lapin.profileFolders`, each local, network or synced, read-only or writable) now serves every kind of library file.
 * The engine host reads a folder's files of the kinds asked (`library.readFolder`) and writes one (`library.writeFolder`);
 * each library's dialog reads what it finds through that kind's own importer. The browser edition has no folders.
 */

import { useEffect, useState } from 'react';
import { engine, engineInBrowser } from '../../engine/session';
import { libraryFileName, useLibraryFiles } from '../../platform/libraries';
import { setSharedFolders, sharedFolders, useLibraries, type SharedFolder } from './profileLibraries/store';

export interface FirmFile { file: string; name: string; kind: string; modified: string; text: string; folder: string; writable: boolean }
export interface FolderRead { folder: string; exists: boolean; files: { file: string; name: string; kind: string; modified: string; text: string }[]; problems: string[] }

/** Whether this edition can reach folders (the desktop's engine does the reading). */
export const firmFoldersAvailable = (): boolean => !engineInBrowser;

export { setSharedFolders as setFirmFolders, sharedFolders as firmFolders, type SharedFolder };

/** The folders Lapin may write library files to. */
export const writableFolders = (folders: SharedFolder[]): SharedFolder[] => folders.filter((f) => f.writable);

/** A folder's files of the kinds, as the engine host reads them; none (and why) when it cannot. */
export async function readFirmFolder(path: string, kinds: string[]): Promise<FolderRead> {
  try { return await engine.call<FolderRead>('library.readFolder', { path, ...(kinds.length ? { kinds } : {}) }); } catch (e) {
    return { folder: path, exists: false, files: [], problems: [e instanceof Error ? e.message : String(e)] };
  }
}

/** The files of all the folders gathered, each knowing its folder and whether it is writable; newest first within a name. */
export function gatherFirmFiles(folders: SharedFolder[], reads: FolderRead[]): { files: FirmFile[]; problems: string[] } {
  const files: FirmFile[] = [], problems: string[] = [];
  reads.forEach((r, i) => {
    problems.push(...r.problems);
    for (const f of r.files) files.push({ ...f, folder: folders[i].path, writable: folders[i].writable });
  });
  files.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }) || b.modified.localeCompare(a.modified));
  return { files, problems };
}

/** Every file of the kinds in the firm's folders. */
export async function readFirmFiles(kinds: string[]): Promise<{ files: FirmFile[]; problems: string[] }> {
  if (!firmFoldersAvailable()) return { files: [], problems: [] };
  const folders = sharedFolders();
  return gatherFirmFiles(folders, await Promise.all(folders.map((f) => readFirmFolder(f.path, kinds))));
}

/** A library file written into a writable folder (one of that name replaced); its path. */
export async function writeFirmFile(folder: string, name: string, ext: string, text: string): Promise<string> {
  const r = await engine.call<{ file: string }>('library.writeFolder', { path: folder, name: libraryFileName(name, ext).replace(/\.[^.]+$/, ''), kind: ext, text });
  useLibraryFiles.getState().bump();
  return r.file;
}

/** The firm's files of the kinds, read again whenever a folder or a library changes. */
export function useFirmFiles(kinds: string[]): { files: FirmFile[]; problems: string[]; loading: boolean; folders: SharedFolder[] } {
  const a = useLibraries((s) => s.version);
  const b = useLibraryFiles((s) => s.version);
  const [state, set] = useState<{ files: FirmFile[]; problems: string[]; loading: boolean }>({ files: [], problems: [], loading: true });
  const key = kinds.join(',');
  useEffect(() => {
    let live = true;
    void readFirmFiles(key.split(',').filter(Boolean)).then((r) => { if (live) set({ ...r, loading: false }); });
    return () => { live = false; };
  }, [a, b, key]);
  return { ...state, folders: sharedFolders() };
}

/** The page File → Preferences… opens on next (set by a library's „Add a firm folder” link). */
export const prefsStart = { page: null as string | null };
