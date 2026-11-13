/** Lapin Extensions (docs/plan-extensions.md): bar shapes (.lapinbarshapes) kept on this computer; Add to this project brings them into the open one. */

import { notify, useUi } from '../../app/store';
import { readBarShapes } from '../../platform/extensions/packs';
import { fileKindInstaller } from '../../platform/extensions/fileKind';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { computerFileId, computerFileRows, forgetComputerFile, keepComputerFile, registerLibraryKind } from '../../platform/libraries';
import { useSession } from '../../engine/session';
import { importShapes } from './barShapes';

async function bring(text: string): Promise<string | null> {
  if (!useSession.getState().connected) { notify('Open a project first.'); return null; }
  const r = await importShapes(text, true);
  const said = r ? `${r.added} shapes added, ${r.updated} updated.` : null;
  if (said) notify(said);
  return said;
}

registerLibraryKind({ ext: 'lapinbarshapes', label: 'Bar shapes', module: 'rebar', names: (t) => readBarShapes(new TextEncoder().encode(t)).codes, bring });

const files = {
  keep: keepComputerFile,
  forget: (ext: string, name: string) => forgetComputerFile(computerFileId(ext, name)),
  get: async (ext: string, name: string) => (await computerFileRows.all()).find((f) => f.id === computerFileId(ext, name))?.text ?? null,
};
const installer = fileKindInstaller({ kind: 'barShapes', ext: 'lapinbarshapes', read: (b) => { const r = readBarShapes(b); return { text: r.text, names: r.codes }; }, files, bring });
addContentInstaller({ ...installer, open: { label: 'Bar shapes…', run: () => useUi.setState({ dialog: 'barShapes' }) } });
