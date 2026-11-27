/**
 * @lapin/extension-kit: builds the interface half of a Lapin extension module (docs/plan-extension-modules.md §1.1, §3)
 * into what its package carries under ui/:
 *
 *   plugin.js     one ES module, nothing outside it imported: the shared libraries (shared.mjs, SHARED) are read from
 *                 the app (`globalThis.__lapin`), the rest is bundled in, dynamic imports included (no chunks of its own)
 *   plugin.css    the CSS it imports, if any (every class name starts with the extension's id: the validator checks it)
 *   i18n/*.json   copied from the folder given: one table per language of ui/src/i18n,
 *                 `{"phrases": {"English": "…"}, "patterns": [["^(\\d+) tasks$", "{1} sarcini"]]}`
 *
 * Its default export is a LapinModule (ui/src/platform/module.ts), or a function given the SDK that returns one.
 *
 *   import { buildPlugin } from '@lapin/extension-kit';
 *   await buildPlugin({ entry: 'src/plugin.tsx', outDir: 'dist/ui', i18n: 'i18n' });
 *
 * The build is deterministic (no timestamps, no hashes in names), so the catalogue's CI can rebuild it to the same bytes.
 */

import { build } from 'vite';
import { cpSync, existsSync, readdirSync, mkdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { lapinShared } from './shared.mjs';

export { SHARED, SDK_EXPORTS, lapinShared, sharedModuleCode } from './shared.mjs';

/** The Vite configuration of a plug-in's build (for a project that runs Vite itself). */
export function pluginConfig({ entry, outDir = 'dist/ui', root = process.cwd(), minify = true, write = true, importer } = {}) {
  if (!entry) throw new Error('extension-kit: an entry is needed (the file whose default export is the module).');
  return {
    configFile: false, root, logLevel: 'warn', mode: 'production', publicDir: false,
    define: { 'process.env.NODE_ENV': '"production"' },
    esbuild: { jsx: 'automatic', legalComments: 'none' },
    plugins: [lapinShared(importer ? { importer } : {})],
    build: {
      write, outDir: resolve(root, outDir), emptyOutDir: true, minify, target: 'es2022', sourcemap: false, copyPublicDir: false, cssCodeSplit: false,
      lib: { entry: resolve(root, entry), formats: ['es'], fileName: () => 'plugin.js', cssFileName: 'plugin' },
      rollupOptions: { treeshake: { propertyReadSideEffects: false }, output: { inlineDynamicImports: true, assetFileNames: (a) => (/\.css$/.test(a.names?.[0] ?? a.name ?? '') ? 'plugin.css' : '[name][extname]') } },
    },
  };
}

/** Builds a plug-in; with `write: false` gives back `{ js, css }` instead of writing them. */
export async function buildPlugin(options) {
  const out = await build(pluginConfig(options));
  const root = options.root ?? process.cwd();
  if (options.write !== false && options.i18n) {
    const from = resolve(root, options.i18n);
    const to = join(resolve(root, options.outDir ?? 'dist/ui'), 'i18n');
    if (existsSync(from)) { mkdirSync(to, { recursive: true }); for (const f of readdirSync(from)) if (f.endsWith('.json')) cpSync(join(from, f), join(to, f)); }
  }
  if (options.write !== false) return null;
  const files = (Array.isArray(out) ? out : [out]).flatMap((o) => o.output);
  const js = files.find((f) => f.type === 'chunk')?.code ?? '';
  const cssFile = files.find((f) => f.type === 'asset' && String(f.fileName).endsWith('.css'));
  const css = cssFile ? (typeof cssFile.source === 'string' ? cssFile.source : new TextDecoder().decode(cssFile.source)) : null;
  return { js, css };
}
