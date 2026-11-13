/**
 * The libraries a user keeps and shares (docs/plan-user-extensibility.md §4): every kind of library file (.lapinsymbols,
 * .lapintypes, .lin…) is one `LibraryKind`, registered by the module that owns its library with how to read what is in the
 * file and how to bring it into the open project through that library's own importer. The firm folders (desktop: local,
 * network or synced folders, read-only or writable), the Catalog and the extension installers all work through this one
 * registry; "this computer" keeps the kinds that had no store of their own as files in the page's IndexedDB.
 */

import { create } from 'zustand';
import { indexedRows } from '../engine/templates';

export interface LibraryKind {
  /** The file's extension, as the engine host names the kind: `lapinsymbols`, `lin`, `pat`. */
  ext: string;
  /** What a library of this kind is called, plural: Symbols, Rule packs. */
  label: string;
  /** The module whose library it belongs to (none: the shell's own); a kind is offered only when the edition has it. */
  module?: string;
  /** The names a file holds; throws a sentence when the text is not this kind's file. */
  names: (text: string) => string[];
  /** The file brought into the open project, through the library's own importer; a sentence of what happened, null when it refused (its message is shown). */
  bring?: (text: string, file: string) => Promise<string | null>;
}

const kinds = new Map<string, LibraryKind>();

/** A kind registered (the last for an extension stands). */
export function registerLibraryKind(k: LibraryKind): void { kinds.set(k.ext, k); }
export const libraryKind = (ext: string): LibraryKind | undefined => kinds.get(ext.replace(/^\./, '').toLowerCase());
export const libraryKinds = (): LibraryKind[] => [...kinds.values()];

/** A file's name from a library item's: letters and digits kept, the characters a file system refuses turned into `_`. */
export const libraryFileName = (name: string, ext: string): string => `${name.replace(/[\\/:*?"<>|]+/g, '_').trim() || 'library'}.${ext}`;

// ------------------------------------------------------------ this computer's files

/** A library file kept on this computer (the page's IndexedDB; the desktop's too), for the kinds whose library had no store of its own. */
export interface ComputerFile { id: string; ext: string; name: string; text: string; at: string; /** The extension that brought it. */ extension?: string }

export const computerFileRows = indexedRows<ComputerFile>('lapin-libraries', 'files', 'library file');
export const computerFileId = (ext: string, name: string) => `${ext}:${name}`;

/** A counter the views watch: bumped when a file is kept or forgotten on this computer, or a firm folder changes. */
export const useLibraryFiles = create<{ version: number; bump: () => void }>((set) => ({ version: 0, bump: () => set((s) => ({ version: s.version + 1 })) }));

export async function computerFiles(ext?: string): Promise<ComputerFile[]> {
  const all = await computerFileRows.all();
  return all.filter((f) => !ext || f.ext === ext).sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

export async function keepComputerFile(ext: string, name: string, text: string, extension?: string): Promise<ComputerFile> {
  const row: ComputerFile = { id: computerFileId(ext, name), ext, name, text, at: new Date().toISOString().slice(0, 10), ...(extension ? { extension } : {}) };
  await computerFileRows.put(row);
  useLibraryFiles.getState().bump();
  return row;
}

export async function forgetComputerFile(id: string): Promise<void> {
  await computerFileRows.remove(id);
  useLibraryFiles.getState().bump();
}
