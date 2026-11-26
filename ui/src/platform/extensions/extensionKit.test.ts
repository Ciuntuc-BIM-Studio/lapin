import { describe, expect, it, vi } from 'vitest';
import { resolve } from 'node:path';
import { readFileSync } from 'node:fs';
import { buildPlugin } from '../../../extension-kit/vite.mjs';
import { SDK_EXPORTS, SHARED } from '../../../extension-kit/shared.mjs';
import { importFromText } from './pluginFixtures';

vi.mock('virtual:lapin-modules', () => ({
  default: [{ id: 'core', name: 'Core', summary: '', tabs: [{ id: 'home', label: 'Home', order: 0, groups: [] }] }],
  product: { id: 't', name: 'T', brand: { name: 'T', short: 'Lapin' }, modules: ['core'] },
}));

/**
 * The extension kit (ui/extension-kit) against the app: its example plug-in built as a package carries it (one ES module,
 * the shared libraries read from the app), loaded by the app's loader with the app's own libraries and SDK, and its tab,
 * view and phrases found where the app looks for them.
 */
describe('the extension kit', () => {
  it('shares what the app publishes, and the SDK it lists is the app\'s', { timeout: 60_000 }, async () => {
    const { SHARED_LIBS } = await import('./hostLibs');
    const { sdk } = await import('../sdk');
    expect(Object.keys(SHARED_LIBS).sort()).toEqual([...SHARED].sort());
    expect(Object.keys(sdk).sort()).toEqual([...SDK_EXPORTS].sort());
  });

  it('builds the example into one module that imports nothing, which the app loads into its tabs, views and phrases', { timeout: 60_000 }, async () => {
    const root = resolve(__dirname, '../../../extension-kit/example');
    const built = await buildPlugin({ root, entry: 'plugin.tsx', write: false });
    expect(built.js).not.toMatch(/(^|[;\s])import\s*[\w{*"']/);
    expect(built.js).not.toMatch(/\bimport\(/);
    expect(built.css).toMatch(/\.hello-ext-view/);

    const { publishHost } = await import('./hostLibs');
    const { sdk } = await import('../sdk');
    publishHost();
    expect((globalThis as unknown as { __lapin: { sdk: unknown } }).__lapin.sdk).toBe(sdk);

    vi.resetModules();
    const plugins = await import('./plugins');
    const i18n = await import('../../i18n');
    const styles: string[] = [];
    const r = await plugins.loadPlugins([{
      id: 'ro.example.hello-ext', version: '1.0.0', sdk: '1.x', js: built.js, css: built.css,
      phrases: { ro: readFileSync(resolve(root, 'i18n/ro.json'), 'utf8') },
    }], { importModule: importFromText, taken: ['core'], addStyle: (_id, css) => styles.push(css), addPhrases: i18n.addPhrases });
    expect(r.left).toEqual([]);
    plugins.setPlugins(r);

    const modules = await import('../modules');
    expect(modules.modules.map((m) => m.id)).toEqual(['core', 'hello-ext']);
    expect(modules.ribbonTabs().map((t) => t.id)).toEqual(['home', 'hello']);
    expect(modules.modules[1].views?.['hello-ext.view']?.label).toBe('Hello');
    expect(styles.length).toBe(1);

    await i18n.loadLanguage('ro');
    expect(i18n.translate('Hello from an extension', 'ro')).toBe('Salut de la o extensie');
    expect(i18n.translate('3 hellos', 'ro')).toBe('3 saluturi');
  });
});
