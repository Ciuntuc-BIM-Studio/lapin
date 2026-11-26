import { describe, expect, it } from 'vitest';
import { headerFacts, idsCounts, importArgs, isModelName, openedNotice } from './referenceModel';
import { fileKind } from './references';
import type { IdsReport } from '../../../engine/serverIds';

describe('reference models in the CDE web (R7)', () => {
  it('knows a model by its name, zipped or not', () => {
    expect(['Structura.ifc', 'INSTALATII.IFCZIP'].every(isModelName)).toBe(true);
    expect(isModelName('plan.pdf')).toBe(false);
    expect(fileKind('Structura.ifczip')).toBe('ifc');
  });

  it('says the header the server read, as far as it has it', () => {
    expect(headerFacts(null)).toEqual([]);
    expect(headerFacts({ schema: 'IFC4', application: 'Tekla Structures 2025', projectName: 'Bloc P+4', readable: true })).toEqual([
      { label: 'Schema', value: 'IFC4' }, { label: 'Written by', value: 'Tekla Structures 2025' }, { label: 'IFC project', value: 'Bloc P+4' },
    ]);
  });

  it('counts the IDS report, none when the server ran none', () => {
    expect(idsCounts(null)).toBeNull();
    const ids = { summary: { specifications: 3, passed: 1, failed: 1, skipped: 1 } } as IdsReport;
    expect(idsCounts(ids)).toEqual({ passed: 1, failed: 1, skipped: 1 });
  });

  it('opens it alone as a project of references, or links it into the latest commit', () => {
    const file = { name: 'Structura.ifc', data: 'SVNP' };
    expect(importArgs(file, false)).toEqual({ name: 'Structura.ifc', data: 'SVNP', mode: 'reference', asProject: true, group: false });
    expect(importArgs(file, true)).toEqual({ name: 'Structura.ifc', data: 'SVNP', mode: 'link' });
    expect(openedNotice(false)).toMatch(/read-only/);
    expect(openedNotice(true)).toMatch(/linked/);
  });
});
