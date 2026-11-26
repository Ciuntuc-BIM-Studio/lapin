import { describe, expect, it } from 'vitest';
import {
  countRows, defaultFileName, defaultTitle, instanceLabel, keptRows, newGlobalId, pickInstance, pickTranslator, receiveArgs, referenceBody, reportRows,
  sendProblems, statusText, updatedSummary, DEFAULT_OPTIONS, type ArchicadInstance, type ArchicadReceived,
} from './archicad';

const inst = (o: Partial<ArchicadInstance>): ArchicadInstance => ({ port: 19723, archicad: '29', build: 3000, tapir: '2.0.1', project: 'House', untitled: false, teamwork: false, ok: true, problem: null, ...o });

describe('the Archicad connector', () => {
  it('shows the instance used last when it still works, else the first that works, else the first', () => {
    const a = inst({ port: 19723, ok: false, problem: 'Tapir is missing.' });
    const b = inst({ port: 19724 });
    const c = inst({ port: 19725 });
    expect(pickInstance([a, b, c], 19725)).toBe(c);
    expect(pickInstance([a, b, c], 19723)).toBe(b);
    expect(pickInstance([a, b, c])).toBe(b);
    expect(pickInstance([a])).toBe(a);
    expect(pickInstance([])).toBeNull();
  });

  it('names an instance by its project, Archicad\'s version and its port', () => {
    expect(instanceLabel(inst({}))).toBe('House · Archicad 29 · port 19723');
    expect(instanceLabel(inst({ project: null, untitled: true, archicad: null, port: 19730 }))).toBe('Untitled project · Archicad · port 19730');
  });

  it('asks the engine for what the dialog chose, the copy only when asked', () => {
    expect(receiveArgs(19723, DEFAULT_OPTIONS)).toEqual({ port: 19723, storeys: 'create', recognize: true, origin: 'project' });
    expect(receiveArgs(1, { storeys: 'existing', origin: 'survey', recognize: false, asCopy: true })).toEqual({ port: 1, storeys: 'existing', recognize: false, origin: 'survey', asCopy: true });
    expect(receiveArgs(1, { ...DEFAULT_OPTIONS, georeference: false }).georeference).toBe(false);
  });

  it('lists a report\'s rows, the optional ones only when they are not zero, and why elements stayed references', () => {
    const r: ArchicadReceived = {
      elements: 10, added: 6, updated: 3, unchanged: 1, removed: 0, native: 7, references: 3, storeys: {}, levels: { matched: 2, made: 1, renamed: 0, moved: 0, removed: 0, kept: 0 },
      recognition: { recognized: 7, offered: 8, byClass: { IfcWall: 4 }, kept: { 'curved wall': 1, 'no body': 3 }, reverted: 0 }, skipped: {}, notes: [], groupId: null,
    };
    expect(reportRows(r).map((x) => `${x.label} ${x.value}`)).toEqual(['Native elements 7', 'References 3', 'Added 6', 'Updated 3', 'Unchanged 1', 'Removed 0', 'Storeys made 1']);
    expect(keptRows(r)).toEqual([['no body', 3], ['curved wall', 1]]);
    expect(countRows({ a: 0, b: 2, c: 2 })).toEqual([['b', 2], ['c', 2]]);
  });

  it('defaults the title to the project\'s name and the file to its title', () => {
    expect(defaultTitle(' House ')).toBe('House');
    expect(defaultTitle(null)).toBe('Archicad model');
    expect(defaultFileName('Villa: A/B')).toBe('Villa- A-B.ifc');
    expect(pickTranslator([{ name: 'IFC2x3', preview: false }, { name: 'Preview', preview: true }])).toBe('Preview');
    expect(pickTranslator([{ name: 'IFC2x3', preview: false }])).toBe('IFC2x3');
    expect(pickTranslator([])).toBe('');
  });

  it('says what a send lacks', () => {
    const ok = { server: 'https://x', port: 19723, translator: 'T', title: 'M', folder: 'f', file: 'M.ifc' };
    expect(sendProblems(ok)).toEqual([]);
    expect(sendProblems({ ...ok, server: null })).toEqual(['Connect the project to a server first: File → Collaborate…']);
    expect(sendProblems({ ...ok, port: null, title: ' ' })).toHaveLength(2);
  });

  it('makes IFC GlobalIds and the server\'s reference body', () => {
    const id = newGlobalId();
    expect(id).toMatch(/^[0-9A-Za-z_$]{22}$/);
    expect(id.charAt(0)).toMatch(/[0-3]/);
    expect(newGlobalId()).not.toBe(id);
    expect(newGlobalId(() => new Uint8Array(16))).toBe('0000000000000000000000');
    expect(referenceBody('g', { title: ' M ', folder: '04', discipline: '' }, { name: 'M.ifc', sha256: 'ab', size: 5 })).toEqual({
      id: 'g', title: 'M', type: 'model', folder: '04', status: 'received', files: [{ name: 'M.ifc', sha256: 'ab', size: 5, mime: 'application/x-step' }],
    });
    expect(referenceBody('g', { title: 'M', folder: '04', discipline: ' A ' }, { name: 'M.ifc', sha256: 'ab', size: 5 }).discipline).toBe('A');
  });

  it('says in the status bar that Archicad changed only while linked, reachable and not busy, with the count when known', () => {
    expect(statusText(null)).toBeNull();
    expect(statusText({ linked: false })).toBeNull();
    expect(statusText({ linked: true, changed: false, count: 0 })).toBeNull();
    expect(statusText({ linked: true, changed: true, busy: true })).toBeNull();
    expect(statusText({ linked: true, changed: true, reachable: false })).toBeNull();
    expect(statusText({ linked: true, changed: true, count: 3, reachable: true })).toEqual({ label: 'Archicad model changed', count: 3 });
    expect(statusText({ linked: true, changed: true, lost: true, count: null })).toEqual({ label: 'Archicad model changed', count: null });
  });

  it('sums an update up in a line', () => {
    const base: ArchicadReceived = {
      elements: 10, added: 0, updated: 0, unchanged: 10, removed: 0, native: 7, references: 3, storeys: {}, levels: null, recognition: null, skipped: {}, notes: [], groupId: null,
    };
    expect(updatedSummary({ ...base, nothing: true })).toBe('Nothing changed in Archicad since the last receive.');
    expect(updatedSummary({ ...base, updated: 2, mode: 'incremental' })).toBe('Got the latest from Archicad.');
    expect(updatedSummary({ ...base, added: 1, mode: 'full' })).toBe('Got the latest from Archicad (the whole model read again).');
  });
});
