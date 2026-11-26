import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadPlugins, pluginsWanted, type PluginFiles, type PhraseTable } from './plugins';
import { sdkProblem, SDK_VERSION_TEXT } from '../sdkVersion';
import { pluginList } from './host';
import { importFromText } from './pluginFixtures';

/**
 * The interface plug-in loader (docs/plan-extension-modules.md §3.1): hand-written ES modules, as the extension kit
 * builds them (they read what they share from `globalThis.__lapin`), each written to a file of its own and imported, as
 * the app imports them from a blob: URL (the test runner's module loader takes no blob: or data: URL). Nothing here
 * comes from outside the test.
 */
const importModule = importFromText;

const g = globalThis as unknown as { __lapin?: unknown };
afterEach(() => { delete g.__lapin; });

const HELLO = `
const sdk = globalThis.__lapin.sdk;
function HelloView() { return null; }
export default {
  id: 'hello-ext', name: 'Hello', summary: 'SDK ' + sdk.version,
  tabs: [{ id: 'hello', label: 'Hello', order: 900, groups: [{ label: 'Greetings', tools: [{ id: 'hello-ext.say', label: 'Say hello', icon: null, run: () => sdk.notify('Hello') }] }] }],
  views: { 'hello-ext.view': { label: 'Hello', component: HelloView } },
};`;

const plugin = (over: Partial<PluginFiles> = {}): PluginFiles => ({
  id: 'ro.example.hello-ext', version: '1.0.0', sdk: '1.x', js: HELLO,
  css: '.hello-ext-view { color: red; }',
  phrases: { ro: { phrases: { 'Say hello': 'Salută' } }, de: JSON.stringify({ phrases: { 'Say hello': 'Hallo sagen' } }) },
  ...over,
});

function deps(taken: string[] = ['core', 'architecture']) {
  const styles: [string, string][] = [];
  const phrases: [string, PhraseTable][] = [];
  const notices: string[] = [];
  g.__lapin = { version: 1, libs: {}, sdk: { version: SDK_VERSION_TEXT, notify: (s: string) => notices.push(s) } };
  return { styles, phrases, notices, d: { importModule, taken, addStyle: (id: string, css: string) => styles.push([id, css]), addPhrases: (l: string, t: PhraseTable) => phrases.push([l, t]), sdk: (g.__lapin as { sdk: unknown }).sdk } };
}

describe('loadPlugins', () => {
  it('a plug-in registers its tab, its view, its phrases and its CSS', async () => {
    const { d, styles, phrases, notices } = deps();
    const r = await loadPlugins([plugin()], d);
    expect(r.left).toEqual([]);
    expect(r.loaded).toEqual([{ id: 'ro.example.hello-ext', version: '1.0.0', module: 'hello-ext' }]);
    const m = r.modules[0];
    expect(m.summary).toBe(`SDK ${SDK_VERSION_TEXT}`);
    expect(m.tabs?.[0].id).toBe('hello');
    expect(Object.keys(m.views ?? {})).toEqual(['hello-ext.view']);
    expect(styles).toEqual([['ro.example.hello-ext', '.hello-ext-view { color: red; }']]);
    expect(phrases.map(([l, t]) => [l, t.phrases?.['Say hello']])).toEqual([['ro', 'Salută'], ['de', 'Hallo sagen']]);
    m.tabs![0].groups[0].tools[0].run!();
    expect(notices).toEqual(['Hello']);
  });

  it('a default export that is a function is given the SDK', async () => {
    const { d } = deps();
    const js = 'export default (sdk) => ({ id: "fn-ext", name: "Fn " + sdk.version, summary: "" });';
    const r = await loadPlugins([plugin({ id: 'ro.example.fn-ext', js })], d);
    expect(r.modules.map((m) => m.name)).toEqual([`Fn ${SDK_VERSION_TEXT}`]);
  });

  it('a plug-in that throws while it loads or registers is left out, and the others still load', async () => {
    const { d, styles, phrases } = deps();
    const r = await loadPlugins([
      plugin({ id: 'a.throws', js: 'throw new Error("boom at load");' }),
      plugin({ id: 'b.register', js: 'export default () => { throw new Error("boom at register"); };' }),
      plugin({ id: 'c.syntax', js: 'export default {' }),
      plugin({ id: 'd.hello-ext' }),
    ], d);
    expect(r.modules.map((m) => m.id)).toEqual(['hello-ext']);
    expect(r.left.map((l) => l.id)).toEqual(['a.throws', 'b.register', 'c.syntax']);
    expect(r.left[0].reason).toMatch(/did not load: boom at load/);
    expect(r.left[1].reason).toMatch(/failed to register: boom at register/);
    // What was left out left nothing behind.
    expect(styles.map(([id]) => id)).toEqual(['d.hello-ext']);
    expect(new Set(phrases.map(([l]) => l)).size).toBe(2);
  });

  it('a plug-in needing another SDK is refused, without its code being run', async () => {
    const { d } = deps();
    const ran = vi.fn(importModule);
    const r = await loadPlugins([
      plugin({ id: 'newer.minor', sdk: '1.99' }),
      plugin({ id: 'newer.major', sdk: '2.x' }),
      plugin({ id: 'none', sdk: '' }),
    ], { ...d, importModule: ran });
    expect(r.modules).toEqual([]);
    expect(r.left.map((l) => l.id)).toEqual(['newer.minor', 'newer.major', 'none']);
    expect(r.left[0].reason).toBe(`it needs SDK 1.99, and this Lapin has SDK ${SDK_VERSION_TEXT}`);
    expect(ran).not.toHaveBeenCalled();
  });

  it('refuses what is not a module, a module id already taken, and an extension listed twice', async () => {
    const { d } = deps(['core', 'hello-ext']);
    const r = await loadPlugins([
      plugin({ id: 'no.default', js: 'export const x = 1;' }),
      plugin({ id: 'x.bad-tabs', js: 'export default { id: "bad-tabs", name: "B", summary: "", tabs: {} };' }),
      plugin({ id: 'takes.hello-ext' }),
      plugin({ id: 'other.name' }),
      plugin({ id: 'x.twice', js: 'export default { id: "twice", name: "T", summary: "a" };' }),
      plugin({ id: 'x.twice', js: 'export default { id: "twice", name: "T", summary: "b" };' }),
    ], d);
    expect(r.modules.map((m) => [m.id, m.summary])).toEqual([['twice', 'a']]);
    expect(r.left.map((l) => [l.id, l.reason])).toEqual([
      ['no.default', 'its interface has no module (default export)'],
      ['x.bad-tabs', "its module's tabs is not a list"],
      ['takes.hello-ext', 'its module id "hello-ext" is taken'],
      ['other.name', 'its module id "hello-ext" is not "name", the last part of the extension\'s id'],
      ['x.twice', 'it is listed twice'],
    ]);
  });
});

describe('the SDK version', () => {
  it('takes the same Major and a Minor up to its own', () => {
    expect(sdkProblem('1.x', { major: 1, minor: 2 })).toBeNull();
    expect(sdkProblem('1', { major: 1, minor: 2 })).toBeNull();
    expect(sdkProblem('^1.2', { major: 1, minor: 2 })).toBeNull();
    expect(sdkProblem('1.3', { major: 1, minor: 2 })).toMatch(/needs SDK 1.3/);
    expect(sdkProblem('0.9', { major: 1, minor: 2 })).toMatch(/needs SDK 0.9/);
    expect(sdkProblem('latest')).toMatch(/cannot be read/);
    expect(sdkProblem(undefined)).toMatch(/does not say/);
  });
});

describe('where plug-ins load', () => {
  it('never in the browser edition nor as a guest', () => {
    expect(pluginsWanted({ inBrowser: false, guest: false })).toBe(true);
    expect(pluginsWanted({ inBrowser: true, guest: false })).toBe(false);
    expect(pluginsWanted({ inBrowser: false, guest: true })).toBe(false);
  });

  it('reads the engine\'s answer as a list or {modules}', () => {
    expect(pluginList([plugin()]).length).toBe(1);
    expect(pluginList({ modules: [plugin(), null] }).length).toBe(1);
    expect(pluginList(null)).toEqual([]);
    expect(pluginList({ error: 'x' })).toEqual([]);
  });
});
