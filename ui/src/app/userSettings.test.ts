/**
 * The user's settings on the desktop (issue #20, decision 470): every `lapin.*` key the interface keeps is copied into the
 * user's folder by the shell and put back into the page's storage at the next start, so a webview that lost its storage
 * (WebKitGTK on Linux) loses nothing.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const file = new Map<string, string>();
const invoke = vi.fn(async (cmd: string, args?: { set: Record<string, string>; remove: string[] }) => {
  if (cmd === 'user_settings_read') return Object.fromEntries(file);
  if (cmd === 'user_settings_write') {
    for (const k of args!.remove) file.delete(k);
    for (const [k, v] of Object.entries(args!.set)) file.set(k, v);
  }
  return null;
});
vi.mock('@tauri-apps/api/core', () => ({ invoke: (cmd: string, args?: never) => invoke(cmd, args) }));

import { PendingWrites, flushUserSettings, restorePlan, restoreUserSettings } from './userSettings';

/** A page's storage as the browser gives it: its methods on the prototype, as the shell patches them. */
class FakeStorage {
  map = new Map<string, string>();
  get length() { return this.map.size; }
  key(i: number) { return [...this.map.keys()][i] ?? null; }
  getItem(k: string) { return this.map.get(k) ?? null; }
  setItem(k: string, v: string) { this.map.set(k, String(v)); }
  removeItem(k: string) { this.map.delete(k); }
}

const g = globalThis as unknown as Record<string, unknown>;

describe('what goes back into the page at the start', () => {
  it('is what the file holds, the file winning, and what the page has that the file lacks is kept the first time', () => {
    const { put, missing } = restorePlan(
      { 'lapin.prefs': '{"theme":"graphite"}', 'lapin.lang': 'ro', 'other': 'x' },
      { 'lapin.prefs': '{"theme":"paper"}', 'lapin.panels': '{}', 'lapin.lang': 'ro', 'vite': '1' },
    );
    expect(put).toEqual({ 'lapin.prefs': '{"theme":"graphite"}' });
    expect(missing).toEqual({ 'lapin.panels': '{}' });
  });

  it('changes wait together, the last of a key winning, a removal cancelling a set', () => {
    const w = new PendingWrites();
    expect(w.empty).toBe(true);
    w.put('lapin.a', '1'); w.put('lapin.a', '2'); w.put('lapin.b', '1'); w.drop('lapin.b');
    expect(w.take()).toEqual({ set: { 'lapin.a': '2' }, remove: ['lapin.b'] });
    expect(w.empty).toBe(true);
  });
});

describe('the desktop', () => {
  beforeEach(() => {
    file.clear();
    invoke.mockClear();
    g.window = { __TAURI_INTERNALS__: {}, addEventListener: vi.fn() };
    g.localStorage = new FakeStorage();
  });
  afterEach(() => { delete g.window; delete g.localStorage; });

  it('puts the settings kept in the user’s folder back after the webview lost them, then follows every change there', async () => {
    // What a Linux webview lost: the page's storage is empty, the file still has the preferences.
    file.set('lapin.prefs', '{"defaultTemplate":"Office","theme":"graphite"}');
    file.set('lapin.lang', 'ro');
    await restoreUserSettings();
    const ls = g.localStorage as FakeStorage;
    expect(ls.getItem('lapin.prefs')).toBe('{"defaultTemplate":"Office","theme":"graphite"}');
    expect(ls.getItem('lapin.lang')).toBe('ro');
    // A change is written into the file (at once when the window closes); keys not the interface's stay in the page.
    ls.setItem('lapin.prefs', '{"theme":"paper"}');
    ls.removeItem('lapin.lang');
    ls.setItem('something.else', '1');
    await flushUserSettings();
    expect(Object.fromEntries(file)).toEqual({ 'lapin.prefs': '{"theme":"paper"}' });
    expect(invoke).toHaveBeenCalledWith('user_settings_write', { set: { 'lapin.prefs': '{"theme":"paper"}' }, remove: ['lapin.lang'] });
  });
});

describe('a browser', () => {
  it('keeps its own storage only: nothing is asked of a shell', async () => {
    g.window = {};
    invoke.mockClear();
    await restoreUserSettings();
    expect(invoke).not.toHaveBeenCalled();
    delete g.window;
  });
});
