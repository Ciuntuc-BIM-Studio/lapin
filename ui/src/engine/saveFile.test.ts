/** An export the interface made, saved where the user says (report #66): the desktop's save dialog, else a download. */

import { afterEach, describe, expect, it, vi } from 'vitest';

const call = vi.fn((_m: string, _p: unknown, _o?: unknown) => Promise.resolve({}));
const save = vi.fn((_o: unknown): Promise<string | null> => Promise.resolve('/Users/me/Desktop/Type assemblies.csv'));
vi.mock('./session', () => ({ engine: { call: (m: string, p: unknown, o?: unknown) => call(m, p, o) }, engineInBrowser: false, activeStorey: () => null, useSession: { getState: () => ({}) } }));
vi.mock('@tauri-apps/plugin-dialog', () => ({ save: (o: unknown) => save(o) }));
vi.mock('../app/store', () => ({ notify: vi.fn(), select: vi.fn(), emptyPane: vi.fn(), useUi: { getState: () => ({}), setState: vi.fn() } }));
vi.mock('./ifcImport', () => ({ editableText: vi.fn(), surveyIfc: vi.fn() }));

import { safeFileName, saveFile } from './files';

const g = globalThis as unknown as Record<string, unknown>;
afterEach(() => { delete g.window; delete g.document; call.mockClear(); save.mockClear(); });

describe('saveFile', () => {
  it('asks where on the desktop, with the default name and its extension, and has the engine write the bytes there', async () => {
    g.window = { __TAURI_INTERNALS__: {} };
    const bytes = new TextEncoder().encode('Kind,Type\n');
    expect(await saveFile(bytes, 'Type assemblies.csv', 'text/csv')).toBe('Type assemblies.csv');
    expect(save).toHaveBeenCalledWith({ defaultPath: 'Type assemblies.csv', filters: [{ name: 'CSV', extensions: ['csv'] }] });
    expect(call.mock.calls[0][0]).toBe('file.write');
    expect(call.mock.calls[0][1]).toEqual({ path: '/Users/me/Desktop/Type assemblies.csv', data: btoa('Kind,Type\n') });
  });

  it('writes nothing when the dialog is closed', async () => {
    g.window = { __TAURI_INTERNALS__: {} };
    save.mockResolvedValueOnce(null);
    expect(await saveFile(new Uint8Array([1, 2]), 'Schedule.xlsx')).toBeNull();
    expect(call).not.toHaveBeenCalled();
  });

  it('downloads in a browser', async () => {
    g.window = {};
    const click = vi.fn();
    const a: Record<string, unknown> = { click };
    g.document = { createElement: () => a };
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    expect(await saveFile(new Uint8Array([1]), 'Schedule.csv', 'text/csv')).toBe('Schedule.csv');
    expect(a.download).toBe('Schedule.csv');
    expect(click).toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });

  it('makes a name the file systems take', () => {
    expect(safeFileName('Doors: level 1/2')).toBe('Doors_ level 1_2');
    expect(safeFileName(' ', 'Schedule')).toBe('Schedule');
  });
});
