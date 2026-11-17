/**
 * Lapin Extensions (docs/plan-extensions.md): the content packs core's libraries take — profile libraries (.lapinprofiles,
 * kept on this computer), hatch patterns (.pat, My patterns) and title blocks (this computer's title blocks). Each is read
 * and checked first; one of the same name already there, otherwise, goes through the import preview's choices (audit D13):
 * keep both („Name (2)”), replace or skip.
 */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { readPatterns, readProfileLibrary, readTitleBlock } from '../../platform/extensions/packs';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { chooseOnComputer } from '../../platform/ImportPreview';
import { freeName } from '../../platform/libraryFile';
import { typesInstaller } from '../../platform/extensions/typesKind';
import { typePackRows } from './typeLibrary';
import { forgetPatterns, keepPatterns, myPatterns } from './myPatterns';
import { computerLibraries, forgetOnComputer, keepOnComputer } from './profileLibraries/store';
import type { LibraryJson } from './profileLibraries/model';
import { forgetBlock, keepBlock, type TbFile } from './titleBlocks/titleBlockModel';

const kept = 'Not installed: yours was kept.';

/** Type packs (.lapintypes) installed: kept on this computer (the page's IndexedDB, the desktop's too), their types brought into the open project. */

addContentInstaller(typesInstaller({
  rows: typePackRows,
  bring: async (text) => {
    if (!useSession.getState().connected) return null;
    const r = await execute('types.importFrom', { text }) as { created: number; reused: number } | null;
    if (r) notify(`Types added to the project: ${r.created} new, ${r.reused} already in it.`);
    return r;
  },
}));

addContentInstaller({
  kind: 'profiles',
  install: async (file) => {
    const lib = readProfileLibrary(file.bytes);
    const there = await computerLibraries();
    const choice = await chooseOnComputer(lib.name, 'profiles', lib.name, lib as unknown as LibraryJson,
      (n) => there.find((l) => l.name === n)?.library, (a, b) => a.material === b.material && JSON.stringify(a.profiles) === JSON.stringify(b.profiles));
    if (choice === null || choice === 'skip') throw new Error(kept);
    const name = choice === 'both' ? freeName((n) => there.some((l) => l.name === n), lib.name) : lib.name;
    await keepOnComputer({ ...(lib as unknown as LibraryJson), name });
    return { library: name };
  },
  uninstall: async (r) => { if (typeof r.library === 'string') await forgetOnComputer(r.library); },
  open: { label: 'Profile libraries…', run: () => useUi.setState({ dialog: 'profileLibraries' }) },
});

addContentInstaller({
  kind: 'hatches',
  install: async (file) => {
    const { text } = readPatterns(file.bytes);
    const choice = await chooseOnComputer(file.name, 'pattern', file.name, text, (n) => myPatterns().find((p) => p.name === n)?.text, (a, b) => a.trim() === b.trim());
    if (choice === null || choice === 'skip') throw new Error(kept);
    const name = choice === 'both' ? freeName((n) => myPatterns().some((p) => p.name === n), file.name) : file.name;
    keepPatterns(name, text);
    return { patterns: name };
  },
  uninstall: async (r) => { if (typeof r.patterns === 'string') forgetPatterns(r.patterns); },
  open: { label: 'Materials…', run: () => useUi.setState({ dialog: 'materials' }) },
});

addContentInstaller({
  kind: 'titleBlock',
  install: async (file) => {
    const block = keepBlock(readTitleBlock(file.bytes) as unknown as TbFile);
    if (!block) throw new Error('This computer\'s title blocks are full.');
    return { block: block.id };
  },
  uninstall: async (r) => { if (typeof r.block === 'string') forgetBlock(r.block); },
  open: { label: 'Title blocks…', run: () => useUi.setState({ dialog: 'titleBlocks' }) },
});
