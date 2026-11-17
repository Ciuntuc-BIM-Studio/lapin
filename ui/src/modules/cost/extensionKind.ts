/** Lapin Extensions (docs/plan-extensions.md): a price list (.lapinprices) goes into this computer's price lists, marked indicative. */

import { useUi } from '../../app/store';
import { readPriceList } from '../../platform/extensions/packs';
import { addContentInstaller } from '../../platform/extensions/runtime';
import { chooseOnComputer } from '../../platform/ImportPreview';
import { freeName } from '../../platform/libraryFile';
import { forgetFromLibrary, keepInLibrary, library } from './estimate';

addContentInstaller({
  kind: 'priceList',
  install: async (file) => {
    const list = readPriceList(file.bytes);
    // One of the same name, otherwise: the import preview's choices (audit D13), as every library file.
    const choice = await chooseOnComputer(list.name, 'priceList', list.name, { currency: list.currency, rows: list.rows },
      (n): { currency: string; rows: unknown } | undefined => library().find((l) => l.name === n), (a, b) => a.currency === b.currency && JSON.stringify(a.rows) === JSON.stringify(b.rows));
    if (choice === null || choice === 'skip') throw new Error('Not installed: yours was kept.');
    const name = choice === 'both' ? freeName((n) => library().some((l) => l.name === n), list.name) : list.name;
    if (!keepInLibrary({ name, currency: list.currency, rows: list.rows, added: new Date().toISOString().slice(0, 10) })) throw new Error('This computer\'s library of price lists is full.');
    return { list: name };
  },
  uninstall: async (r) => { if (typeof r.list === 'string') forgetFromLibrary(r.list); },
  open: { label: 'Price lists…', run: () => useUi.setState({ dialog: 'cost.prices' }) },
});
