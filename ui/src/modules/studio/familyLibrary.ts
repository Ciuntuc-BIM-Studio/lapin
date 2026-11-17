/**
 * Parts as a library kind (docs/plan-user-extensibility.md §4, audit S6): a .lapinpart (`{format: "lapinpart", version: 1, name,
 * def}`; an older .lapinfamily is read too) read without the engine, brought into the project through the one import preview,
 * and the extension kind `families`. Only the library plumbing (the project's parts come from `studio.parts` and
 * `studio.exportPart`): the parts' manager and placing are in ./parts and ./families.
 */

import { exportPart } from './families/familyApi';
import { readFamily } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';
import { importLibraryText, tallySentence } from '../../platform/ImportPreview';

export type { FamilyDto } from './families/familyModel';

/** A project part as a .lapinpart file's text (the firm folders' and this computer's saving). */
export const familyFileText = async (id: string): Promise<{ text: string; file: string } | null> => exportPart(id);

/** Through the one import preview (audit D13): a part of its name there kept both, replaced or skipped. */
async function bring(text: string, file = 'part.lapinpart'): Promise<string | null> {
  const r = await importLibraryText(text, file);
  return r ? tallySentence(r) : null;
}

registerLibraryKind({ ext: 'lapinpart', label: 'Parts', module: 'studio', names: (t) => [readFamily(new TextEncoder().encode(t)).name], bring });
registerLibraryKind({ ext: 'lapinfamily', label: 'Parts (old .lapinfamily)', module: 'studio', names: (t) => [readFamily(new TextEncoder().encode(t)).name], bring });

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};
addContentInstaller(fileKindInstaller({ kind: 'families', ext: 'lapinfamily', read: (b) => { const r = readFamily(b); return { text: r.text, names: [r.name] }; }, files, bring }));
