/**
 * Lapin Extensions (docs/plan-extensions.md): the content packs core's libraries take — profile libraries (.lapinprofiles,
 * kept on this computer), hatch patterns (.pat, My patterns) and title blocks (this computer's title blocks). Each is read
 * and checked first; one of the same name already there is replaced only when the user says so.
 */

import { useUi } from '../../app/store';
import { readPatterns, readProfileLibrary, readTitleBlock } from '../../platform/extensions/packs';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { forgetPatterns, keepPatterns, myPatterns } from './myPatterns';
import { computerLibraries, forgetOnComputer, keepOnComputer } from './profileLibraries/store';
import type { LibraryJson } from './profileLibraries/model';
import { forgetBlock, keepBlock, type TbFile } from './titleBlocks/titleBlockModel';

const kept = 'Not installed: yours was kept.';

addContentInstaller({
  kind: 'profiles',
  install: async (file, _m, ctx) => {
    const lib = readProfileLibrary(file.bytes);
    if ((await computerLibraries()).some((l) => l.name === lib.name) && !(await ctx.confirm(`A profile library is already called „${lib.name}” on this computer. Replace it?`))) throw new Error(kept);
    await keepOnComputer(lib as unknown as LibraryJson);
    return { library: lib.name };
  },
  uninstall: async (r) => { if (typeof r.library === 'string') await forgetOnComputer(r.library); },
  open: { label: 'Profile libraries…', run: () => useUi.setState({ dialog: 'profileLibraries' }) },
});

addContentInstaller({
  kind: 'hatches',
  install: async (file, _m, ctx) => {
    const { text } = readPatterns(file.bytes);
    if (myPatterns().some((p) => p.name === file.name) && !(await ctx.confirm(`My patterns already has „${file.name}”. Replace it?`))) throw new Error(kept);
    keepPatterns(file.name, text);
    return { patterns: file.name };
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
