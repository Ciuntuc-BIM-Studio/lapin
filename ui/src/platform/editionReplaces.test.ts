/**
 * The lapin edition replaces nothing (docs/overrides.md): its UI modules, as the build bundles them
 * (`virtual:lapin-modules` from products/lapin.json completed by scripts/product.mjs), have no `replaces`.
 */

import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { resolveProduct } from '../../../scripts/product.mjs';

/** The build's product is another edition's (LAPIN_PRODUCT, LAPIN_PRODUCT_FILE, as vite.config.ts reads them): not this test's. */
const other = Boolean(process.env.LAPIN_PRODUCT_FILE) || (process.env.LAPIN_PRODUCT ?? 'lapin') !== 'lapin';

describe('the lapin edition', () => {
  it.skipIf(other)('has modules that replace nothing', async () => {
    const product = resolveProduct(resolve(__dirname, '../../..'), 'lapin');
    // The app's own order: the product's modules first, as App.tsx reads them.
    const { modules, replacements } = await import('./modules');
    const { fileActions, baseFileActions } = await import('../shell/fileActions');
    expect(modules.map((m) => m.id)).toEqual(product.ui);
    expect(modules.filter((m) => m.replaces).map((m) => m.id)).toEqual([]);
    expect(replacements).toEqual([]);
    expect([...fileActions().values()].map((a) => a.label)).toEqual(baseFileActions().map((a) => a.label));
  }, 120_000);
});
