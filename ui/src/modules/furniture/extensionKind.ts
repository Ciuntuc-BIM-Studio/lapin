/** Lapin Extensions (docs/plan-extensions.md): a furniture pack (.lapinlib) goes into My furniture; Uninstall takes away what it added. */

import { useUi } from '../../app/store';
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
