/**
 * Families as a library kind (docs/plan-user-extensibility.md §4): a .lapinfamily (`{format: "lapin-family", version: 1, name,
 * def}`) read without the engine, brought into the project by `studio.importFamily`, and the extension kind `families`. Only
 * the library plumbing (the project's families come from `studio.families` and `studio.exportFamily`): the families' editor and
 * placing are in ./families.
 */

import { exportFamily } from './families/familyApi';
import { readFamily } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';
import { importLibraryText, tallySentence } from '../../platform/ImportPreview';

export type { FamilyDto } from './families/familyModel';

/** A project family as a .lapinfamily file's text (the firm folders' and this computer's saving). */
export const familyFileText = async (id: string): Promise<{ text: string; file: string } | null> => exportFamily(id);

/** Through the one import preview (audit D13): a part of its name there kept both, replaced or skipped. */
async function bring(text: string, file = 'family.lapinfamily'): Promise<string | null> {
  const r = await importLibraryText(text, file);
  return r ? tallySentence(r) : null;
}

registerLibraryKind({ ext: 'lapinfamily', label: 'Families', module: 'studio', names: (t) => [readFamily(new TextEncoder().encode(t)).name], bring });

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};
addContentInstaller(fileKindInstaller({ kind: 'families', ext: 'lapinfamily', read: (b) => { const r = readFamily(b); return { text: r.text, names: [r.name] }; }, files, bring }));
