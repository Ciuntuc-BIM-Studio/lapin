/** An export the interface made, saved where the user says (report #66): the desktop's save dialog, else a download. */

import { afterEach, describe, expect, it, vi } from 'vitest';

const call = vi.fn((_m: string, _p: unknown, _o?: unknown) => Promise.resolve({}));
const save = vi.fn((_o: unknown): Promise<string | null> => Promise.resolve('/Users/me/Desktop/Type assemblies.csv'));
vi.mock('./session', () => ({ engine: { call: (m: string, p: unknown, o?: unknown) => call(m, p, o) }, engineInBrowser: false, activeStorey: () => null, useSession: { getState: () => ({}) } }));
vi.mock('@tauri-apps/plugin-dialog', () => ({ save: (o: unknown) => save(o) }));
vi.mock('../app/store', () => ({ notify: vi.fn(), select: vi.fn(), emptyPane: vi.fn(), useUi: { getState: () => ({}), setState: vi.fn() } }));
const askName = vi.fn((_t: string, _f: unknown): Promise<Record<string, string> | null> => Promise.resolve({ name: 'Schedule.csv' }));
vi.mock('../shell/AskBox', () => ({ ask: (t: string, f: unknown) => askName(t, f), textField: (key: string, label: string, value: string) => ({ key, label, value, text: true }) }));
vi.mock('./ifcImport', () => ({ editableText: vi.fn(), surveyIfc: vi.fn() }));

import { chooseTarget, exportFile, safeFileName, saveFile } from './files';

const g = globalThis as unknown as Record<string, unknown>;
afterEach(() => { delete g.window; delete g.document; call.mockClear(); save.mockClear(); askName.mockClear(); });

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

  it('asks for the name in a box where the browser has no save picker, then downloads under it', async () => {
    g.window = {};
    const click = vi.fn();
    const a: Record<string, unknown> = { click };
    g.document = { createElement: () => a };
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    askName.mockResolvedValueOnce({ name: 'Doors' });
    expect(await saveFile(new Uint8Array([1]), 'Schedule.csv', 'text/csv')).toBe('Doors.csv');
    expect(askName.mock.calls[0][1]).toEqual([{ key: 'name', label: 'File name', value: 'Schedule.csv', text: true }]);
    expect(a.download).toBe('Doors.csv');
    expect(click).toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });

  it('downloads nothing when the box is closed', async () => {
    g.window = {};
    askName.mockResolvedValueOnce(null);
    expect(await saveFile(new Uint8Array([1]), 'Schedule.csv', 'text/csv')).toBeNull();
  });

  it('uses the browser save picker where there is one, with the suggested name', async () => {
    const write = vi.fn(); const close = vi.fn();
    const handle = { name: 'My house.ifc', createWritable: () => Promise.resolve({ write, close }) };
    const showSaveFilePicker = vi.fn(() => Promise.resolve(handle));
    g.window = { showSaveFilePicker };
    expect(await saveFile(new Uint8Array([1]), 'project.ifc')).toBe('My house.ifc');
    expect(showSaveFilePicker).toHaveBeenCalledWith(expect.objectContaining({ suggestedName: 'project.ifc' }));
    expect(write).toHaveBeenCalled();
    expect(askName).not.toHaveBeenCalled();
  });

  it('falls back to the box when the picker is refused, and to nothing when it is cancelled', async () => {
    g.window = { showSaveFilePicker: () => Promise.reject(Object.assign(new Error('x'), { name: 'SecurityError' })) };
    askName.mockResolvedValueOnce({ name: 'House' });
    expect(await chooseTarget('House.ifc')).toEqual({ name: 'House.ifc', handle: null });
    g.window = { showSaveFilePicker: () => Promise.reject(Object.assign(new Error('x'), { name: 'AbortError' })) };
    expect(await chooseTarget('House.ifc')).toBeNull();
    expect(askName).toHaveBeenCalledTimes(1);
  });

  it('asks the name of an export before the engine works, and exports under it', async () => {
    g.window = {};
    const a: Record<string, unknown> = { click: vi.fn() };
    g.document = { createElement: () => a };
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    askName.mockResolvedValueOnce({ name: 'My house.ifc' });
    call.mockResolvedValueOnce({ data: btoa('ISO') });
    await exportFile('project.exportIfc', {}, 'My house.ifc', 'ifc');
    expect(a.download).toBe('My house.ifc');
    askName.mockResolvedValueOnce(null);
    call.mockClear();
    await exportFile('project.exportIfc', {}, 'My house.ifc', 'ifc');
    expect(call).not.toHaveBeenCalled();
  });

  it('makes a name the file systems take', () => {
    expect(safeFileName('Doors: level 1/2')).toBe('Doors_ level 1_2');
    expect(safeFileName(' ', 'Schedule')).toBe('Schedule');
  });
});
