/** Lapin Extensions (docs/plan-extensions.md): a price list (.lapinprices) goes into this computer's price lists, marked indicative. */

import { useUi } from '../../app/store';
import { readPriceList } from '../../platform/extensions/packs';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { forgetFromLibrary, keepInLibrary, library } from './estimate';

addContentInstaller({
  kind: 'priceList',
  install: async (file, _m, ctx) => {
    const list = readPriceList(file.bytes);
    if (library().some((l) => l.name === list.name) && !(await ctx.confirm(`A price list is already called „${list.name}” on this computer. Replace it?`))) throw new Error('Not installed: yours was kept.');
    if (!keepInLibrary({ name: list.name, currency: list.currency, rows: list.rows, added: new Date().toISOString().slice(0, 10) })) throw new Error('This computer\'s library of price lists is full.');
    return { list: list.name };
  },
  uninstall: async (r) => { if (typeof r.list === 'string') forgetFromLibrary(r.list); },
  open: { label: 'Price lists…', run: () => useUi.setState({ dialog: 'cost.prices' }) },
});
