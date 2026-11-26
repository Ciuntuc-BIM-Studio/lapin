import { describe, expect, it } from 'vitest';
import { defaultSuitability, formProblems, isSubmittable, sha256Hex, submissionBody, suitabilities, type SubmitForm } from './submitFiles';

const form = (over: Partial<SubmitForm> = {}): SubmitForm => ({
  files: [{ name: 'PRJ-ORG-ZZ-00-DR-S-0101_S2_P01.pdf', size: 42 }], revision: 'P01', purpose: 'shared', suitability: 'S2', issue: '', note: '', ...over,
});

describe('Submit files… (R7)', () => {
  it('takes PDFs, DXFs and IFCs, zipped or not, whatever their case', () => {
    expect(['a.pdf', 'b.DXF', 'c.ifc', 'd.IfcZip'].every(isSubmittable)).toBe(true);
    expect(['e.dwg', 'f.docx', 'g', 'h.ifc.bak'].some(isSubmittable)).toBe(false);
  });

  it('offers the statuses each container takes, S2 and A1 first chosen', () => {
    expect(suitabilities('shared')).toEqual(['S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7']);
    expect(suitabilities('published')).toHaveLength(19);
    expect(suitabilities('published')).toContain('CR');
    expect(suitabilities('published')).not.toContain('S2');
    expect([defaultSuitability('shared'), defaultSuitability('published')]).toEqual(['S2', 'A1']);
  });

  it('says what stops the form', () => {
    expect(formProblems(form())).toEqual([]);
    expect(formProblems(form({ files: [] }))).toEqual([{ file: null, text: 'Choose at least one file.' }]);
    expect(formProblems(form({ files: [{ name: 'a.docx', size: 1 }] }))).toEqual([{ file: 'a.docx', text: 'Only PDF, DXF and IFC files are submitted here.' }]);
    expect(formProblems(form({ files: [{ name: 'A.pdf', size: 1 }, { name: 'a.PDF', size: 2 }] }))).toEqual([{ file: 'a.PDF', text: 'Two files have this name.' }]);
    expect(formProblems(form({ revision: 'X1' }))).toHaveLength(1);
    expect(formProblems(form({ revision: 'c02' }))).toEqual([]);
    expect(formProblems(form({ purpose: 'published', suitability: 'S2' }))).toEqual([{ file: null, text: 'Choose a status the container takes.' }]);
    expect(formProblems(form({ issue: '0' }))).toHaveLength(1);
    expect(formProblems(form({ issue: '12' }))).toEqual([]);
  });

  it('makes the body without a commit, the names kept as they are on disk', () => {
    const b = submissionBody(form({ revision: ' p02 ', issue: ' 4 ', note: '  From the structural engineer ', purpose: 'published', suitability: 'A1' }),
      [{ name: 'Structura bloc.ifc', sha256: 'ab'.repeat(32), size: 9 }]);
    expect(b).toEqual({ issue: 4, revision: 'P02', purpose: 'published', suitability: 'A1', note: 'From the structural engineer', files: [{ name: 'Structura bloc.ifc', sha256: 'ab'.repeat(32), size: 9 }] });
    expect(b).not.toHaveProperty('commit');
    expect(submissionBody(form(), [])).toMatchObject({ issue: null });
    expect(submissionBody(form(), [])).not.toHaveProperty('note');
  });

  it('names a file by its SHA-256', async () => {
    expect(await sha256Hex(new Uint8Array())).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
  });
});
