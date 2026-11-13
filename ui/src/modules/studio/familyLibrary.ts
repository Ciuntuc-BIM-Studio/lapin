/**
 * Families as a library kind (docs/plan-user-extensibility.md §4): a .lapinfamily (`{format: "lapin-family", version: 1, name,
 * def}`) read without the engine, brought into the project by `studio.importFamily`, and the extension kind `families`. Only
 * the library plumbing: the families' editor and placing are Studio's own.
 */

import { notify } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { readFamily } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';

export interface FamilyDto { id: string; name: string; def: unknown; uses: number }

async function bring(text: string): Promise<string | null> {
  if (!useSession.getState().connected) { notify('Open a project first.'); return null; }
  const r = await execute('studio.importFamily', { text }) as { id: string } | null;
  const said = r ? `Family added to the project: ${readFamily(new TextEncoder().encode(text)).name}.` : null;
  if (said) notify(said);
  return said;
}

registerLibraryKind({ ext: 'lapinfamily', label: 'Families', module: 'studio', names: (t) => [readFamily(new TextEncoder().encode(t)).name], bring });

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};
addContentInstaller(fileKindInstaller({ kind: 'families', ext: 'lapinfamily', read: (b) => { const r = readFamily(b); return { text: r.text, names: [r.name] }; }, files, bring }));
