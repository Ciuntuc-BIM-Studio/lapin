import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Products are compositions of modules (products/<id>.json, chosen with
 * LAPIN_PRODUCT; default: lapin). The build bundles only the product's UI
 * modules: a module the product does not have is not hidden, its code is not
 * in the bundle at all. The same manifest tells the engine which of its
 * modules to load.
 */
function productModules(): Plugin {
  const id = 'virtual:lapin-modules';
  const resolved = '\0' + id;
  const file = resolve(__dirname, '../products', `${process.env.LAPIN_PRODUCT ?? 'lapin'}.json`);
  const manifest = JSON.parse(readFileSync(file, 'utf8')) as { id: string; name: string; modules: string[] };
  const present = manifest.modules.filter((m) => existsSync(resolve(__dirname, 'src/modules', m, 'index.tsx')));
  return {
    name: 'lapin-product-modules',
    resolveId: (s) => (s === id ? resolved : undefined),
    load: (s) => {
      if (s !== resolved) return undefined;
      const imports = present.map((m, i) => `import m${i} from '/src/modules/${m}/index.tsx';`).join('\n');
      return `${imports}\nexport const product = ${JSON.stringify({ id: manifest.id, name: manifest.name })};\nexport default [${present.map((_, i) => `m${i}`).join(', ')}];\n`;
    },
  };
}

// The engine's wire protocol and client come from the BubbleBIM Pro engine
// itself (vendor/bubblebim-pro), so Lapin always speaks the engine's language.
export default defineConfig({
  plugins: [react(), productModules()],
  resolve: { alias: { '@bbim/platform': resolve(__dirname, '../vendor/bubblebim-pro/ui/src/platform') } },
  clearScreen: false,
  server: { port: 5180, strictPort: true, fs: { allow: [resolve(__dirname, '..')] } },
  build: { outDir: process.env.LAPIN_UI_OUT ?? 'dist', emptyOutDir: true, chunkSizeWarningLimit: 1500 },
});
