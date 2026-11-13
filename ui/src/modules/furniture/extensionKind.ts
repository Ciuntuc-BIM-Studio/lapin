/** Lapin Extensions (docs/plan-extensions.md): a furniture pack (.lapinlib) goes into My furniture; Uninstall takes away what it added. */

import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { registerLibraryKind } from '../../platform/libraries';
import { toBase64 } from '../../engine/files';
import { readFurnitureLibrary } from '../../platform/extensions/packs';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { deleteEntry, importLibrary } from './library';

addContentInstaller({
  kind: 'furniture',
  install: async (file) => {
    readFurnitureLibrary(file.bytes);
    // An item the library already has, the same, is skipped: it stays when the extension is uninstalled.
    const r = await importLibrary({ data: toBase64(file.bytes) });
    return { ids: r.added, skipped: r.skipped };
  },
  uninstall: async (r) => {
    for (const id of Array.isArray(r.ids) ? (r.ids as string[]) : []) await deleteEntry(id).catch(() => undefined);
  },
  open: { label: 'My furniture…', run: () => useUi.setState({ dialog: 'furnitureLibrary' }) },
});

/** A .lapinlib in a firm's folder (or the Catalog): its items added to My furniture, those it already has skipped. */
registerLibraryKind({
  ext: 'lapinlib', label: 'Furniture', module: 'furniture',
  names: (t) => readFurnitureLibrary(new TextEncoder().encode(t)).names,
  bring: async (text) => {
    if (!useSession.getState().connected) { notify('Open a project first.'); return null; }
    const r = await importLibrary({ data: toBase64(new TextEncoder().encode(text)) });
    const said = `Furniture added to My furniture: ${r.added.length} new, ${r.skipped} already there.`;
    notify(said);
    return said;
  },
});
