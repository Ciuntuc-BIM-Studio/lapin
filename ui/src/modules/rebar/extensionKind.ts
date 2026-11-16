/** Lapin Extensions (docs/plan-extensions.md): bar shapes (.lapinbarshapes) kept on this computer; Add to this project brings them into the open one. */

import { useUi } from '../../app/store';
import { readBarShapes } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';
import { importLibraryText, tallySentence } from '../../platform/ImportPreview';

/** Through the one import preview (audit D13), which says what it did. */
async function bring(text: string, file = 'shapes.lapinbarshapes'): Promise<string | null> {
  const r = await importLibraryText(text, file);
  return r ? tallySentence(r) : null;
}

registerLibraryKind({ ext: 'lapinbarshapes', label: 'Bar shapes', module: 'rebar', names: (t) => readBarShapes(new TextEncoder().encode(t)).codes, bring });

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};
const installer = fileKindInstaller({ kind: 'barShapes', ext: 'lapinbarshapes', read: (b) => { const r = readBarShapes(b); return { text: r.text, names: r.codes }; }, files, bring });
addContentInstaller({ ...installer, open: { label: 'Bar shapes…', run: () => useUi.setState({ dialog: 'barShapes' }) } });
