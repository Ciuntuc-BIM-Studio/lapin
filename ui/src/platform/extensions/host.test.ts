import { afterEach, describe, expect, it, vi } from 'vitest';

const made = vi.hoisted(() => ({ clients: 0, answer: null as unknown, fail: null as string | null }));
vi.mock('../../engine/client', () => ({
  engineUrl: async () => 'ws://test',
  EngineClient: class {
    constructor() { made.clients++; }
    async start() { /* connected */ }
    async call(method: string) { if (made.fail) throw new Error(made.fail); return method === 'extensions.modules' ? made.answer : null; }
    stop() { /* closed */ }
  },
}));
vi.mock('./hostLibs', () => { throw new Error('the shared libraries must not be fetched here'); });

/** The boot step (host.ts): asks the engine once, fetches the shared libraries only when an extension has a plug-in, never in the browser edition. */
describe('loadExtensionPlugins', () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); made.clients = 0; made.answer = null; made.fail = null; });

  it('does nothing in the browser edition: no engine call, no plug-ins', async () => {
    vi.stubEnv('VITE_ENGINE', 'worker');
    const { loadExtensionPlugins } = await import('./host');
    const { extensionModules, usePluginReport } = await import('./plugins');
    made.answer = [{ id: 'x', version: '1', sdk: '1.x', js: 'export default {}' }];
    await loadExtensionPlugins();
    expect(made.clients).toBe(0);
    expect(extensionModules()).toEqual([]);
    expect(usePluginReport.getState()).toMatchObject({ done: true, loaded: [], left: [] });
  });

  it('with none installed, or an engine that has no such call, boots as before', async () => {
    const { loadExtensionPlugins } = await import('./host');
    const { extensionModules, usePluginReport } = await import('./plugins');
    made.answer = { modules: [] };
    await loadExtensionPlugins();
    expect(made.clients).toBe(1);
    made.fail = 'Unknown method extensions.modules';
    await loadExtensionPlugins();
    expect(extensionModules()).toEqual([]);
    expect(usePluginReport.getState().done).toBe(true);
  });

  it('a failure to fetch the shared libraries leaves the plug-ins out and does not throw', async () => {
    const { loadExtensionPlugins } = await import('./host');
    const { extensionModules } = await import('./plugins');
    made.answer = [{ id: 'x', version: '1', sdk: '1.x', js: 'export default {}' }];
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await expect(loadExtensionPlugins()).resolves.toBeUndefined();
    expect(extensionModules()).toEqual([]);
    warn.mockRestore();
  });
});
