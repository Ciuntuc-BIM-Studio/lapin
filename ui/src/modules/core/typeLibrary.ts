/**
 * Type packs on this computer and in the firm's folders (docs/plan-extensions.md §9.1, docs/plan-user-extensibility.md §5.2):
 * the .lapintypes the engine writes for chosen types (`types.export`), kept on this computer (the page's IndexedDB, where the
 * types extensions are too) or written into a firm folder, so a type configured in one project is one click away in another.
 * A row's id is `user:<name>` for what the user saved and the extension's id for what an extension brought.
 */

import { notify } from '../../app/store';
import { indexedRows } from '../../engine/templates';
import { ask } from './LibraryChoices';
import { readTypePack } from '../../platform/extensions/packs';
import { useLibraryFiles } from '../../platform/libraries';
import { firmFoldersAvailable, firmFolders, writableFolders, writeFirmFile } from './firmFolders';
import type { TypePackRow } from '../../platform/extensions/typesKind';

/** The type packs kept on this computer (what a types extension installed and what the user saved). */
export const typePackRows = indexedRows<TypePackRow>('lapin-type-packs', 'packs', 'type pack');

export const USER_PACK = 'user:';
export const isUserPack = (row: { id: string }) => row.id.startsWith(USER_PACK);

/** The engine's export of types (by id), as text and the file's name; null when it refused (the sentence is shown). */
export const exportTypes = (ids: string[], name?: string) => ask<{ text: string; file: string }>('types.export', { ids, ...(name ? { name } : {}) });

/** Where a configured type can be saved. */
export type LibraryTarget = { kind: 'computer' } | { kind: 'folder'; path: string };

/** The places the user can save to: this computer, and each writable firm folder (desktop). */
export function libraryTargets(): { id: string; label: string; target: LibraryTarget }[] {
  const out: { id: string; label: string; target: LibraryTarget }[] = [{ id: 'computer', label: 'This computer', target: { kind: 'computer' } }];
  if (firmFoldersAvailable()) for (const f of writableFolders(firmFolders())) out.push({ id: `folder:${f.path}`, label: f.path.split(/[\\/]/).filter(Boolean).pop() ?? f.path, target: { kind: 'folder', path: f.path } });
  return out;
}

/**
 * Types saved as a .lapintypes to a library: the engine writes the pack, the pack is checked as any other, then kept in the
 * IndexedDB of this computer or written into a firm folder. A pack of the same name is replaced. The sentence said; false when it failed.
 */
export async function saveTypesToLibrary(ids: string[], name: string, target: LibraryTarget): Promise<boolean> {
  if (ids.length === 0) { notify('Choose a type to save.'); return false; }
  const r = await exportTypes(ids, name);
  if (!r) return false;
  try {
    const pack = readTypePack(new TextEncoder().encode(r.text));
    if (target.kind === 'computer') {
      await typePackRows.put({ id: `${USER_PACK}${name}`, name, file: r.file, text: r.text, types: pack.types.map((t) => t.name), at: new Date().toISOString() });
      useLibraryFiles.getState().bump();
      notify(`Saved to this computer's type packs: ${name}.`);
    } else notify(`Saved to ${await writeFirmFile(target.path, name, 'lapintypes', r.text)}.`);
    return true;
  } catch (e) { notify(e instanceof Error ? e.message : String(e)); return false; }
}
