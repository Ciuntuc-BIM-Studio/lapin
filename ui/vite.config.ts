import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { resolveProduct } from '../scripts/product.mjs';

/**
 * Products are compositions of modules (products/<id>.json, chosen with
 * LAPIN_PRODUCT; default: lapin), completed with what their modules require
 * (scripts/product.mjs). The build bundles only the product's UI modules: a
 * module the product does not have is not hidden, its code is not in the
 * bundle at all. The same manifest tells the engine which modules to load;
 * dev and product builds pass it resolved, in LAPIN_PRODUCT_FILE.
 */
function productModules(): Plugin {
  const id = 'virtual:lapin-modules';
  const resolved = '\0' + id;
  const file = process.env.LAPIN_PRODUCT_FILE;
  const manifest = (file
    ? JSON.parse(readFileSync(file, 'utf8'))
    : resolveProduct(resolve(__dirname, '..'), process.env.LAPIN_PRODUCT ?? 'lapin')) as { id: string; name: string; modules: string[]; uiPaths?: Record<string, string> };
  // A module outside this repository (a private one) comes by its folder's path; it imports Lapin through the @lapin alias.
  const pathOf = (m: string) => manifest.uiPaths?.[m] ?? resolve(__dirname, 'src/modules', m);
  const present = manifest.modules.filter((m) => existsSync(resolve(pathOf(m), 'index.tsx')));
  return {
    name: 'lapin-product-modules',
    resolveId: (s) => (s === id ? resolved : undefined),
    load: (s) => {
      if (s !== resolved) return undefined;
      const imports = present.map((m, i) => `import m${i} from '${manifest.uiPaths?.[m] ? resolve(pathOf(m), 'index.tsx') : `/src/modules/${m}/index.tsx`}';`).join('\n');
      return `${imports}\nexport const product = ${JSON.stringify({ id: manifest.id, name: manifest.name })};\nexport default [${present.map((_, i) => `m${i}`).join(', ')}];\n`;
    },
  };
}

export default defineConfig({
  plugins: [react(), productModules()],
  resolve: { alias: { '@lapin': resolve(__dirname, 'src') } },
  // The web build is served from wherever its folder is put (LAPIN_UI_BASE=./); the desktop app and dev from the root.
  base: process.env.LAPIN_UI_BASE ?? '/',
  // The engine's worker (src/engine/worker) is an ES module: it imports the engine's runtime from engine/.
  worker: { format: 'es' },
  clearScreen: false,
  // The sibling repositories too, for the modules that live there.
  server: { port: 5180, strictPort: true, fs: { allow: [resolve(__dirname, '../..')] } },
  build: { outDir: process.env.LAPIN_UI_OUT ?? 'dist', emptyOutDir: true, chunkSizeWarningLimit: 1500 },
});
