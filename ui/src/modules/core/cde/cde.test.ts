import { describe, expect, it } from 'vitest';
import { containerFor, formatSize, groupFindings, isRevision, issuedSummary, issuePhase, nextRevision, type Finding, type IssuePhase, type IssueResult } from './cde';

describe('revisions as ISO 19650-2 has them', () => {
  it('go P01, P02… and C01, C02…, a contractual issue starting at C01', () => {
    expect(nextRevision(null)).toBe('P01');
    expect(nextRevision(undefined, true)).toBe('C01');
    expect(nextRevision('P01')).toBe('P02');
    expect(nextRevision('P09')).toBe('P10');
    expect(nextRevision('P099')).toBe('P100');
    expect(nextRevision('P03', true)).toBe('C01');
    expect(nextRevision('C01')).toBe('C02');
    expect(nextRevision('C02', false)).toBe('C02');
    expect(nextRevision('odd')).toBe('P01');
  });

  it('are recognised', () => {
    expect(isRevision('P01')).toBe(true);
    expect(isRevision('C12')).toBe(true);
    expect(isRevision('P1')).toBe(false);
    expect(isRevision('R01')).toBe(false);
  });

  it('suggest the container from the status', () => {
    expect(containerFor('S0')).toBe('WIP');
    expect(containerFor('S2')).toBe('Shared');
    expect(containerFor('S7')).toBe('Shared');
    expect(containerFor('A1')).toBe('Published');
    expect(containerFor('B3')).toBe('Published');
    expect(containerFor('CR')).toBe('Published');
  });
});

describe('the findings list', () => {
  const rules = [{ id: 'titleBlock', label: 'Title block', blocking: true }, { id: 'roomNumber', label: 'Room numbers', blocking: false }, { id: 'naming', label: 'File name', blocking: true }];
  const findings: Finding[] = [
    { rule: 'roomNumber', severity: 'warn', message: 'Room „Kitchen” has no number.', entityId: 'r1' },
    { rule: 'titleBlock', severity: 'warn', message: 'Checked by is empty.', sheetId: 's1' },
    { rule: 'titleBlock', severity: 'block', message: 'Drawn by is empty.', sheetId: 's1' },
    { rule: 'company', severity: 'block', message: 'The company says no.' },
  ];

  it('groups by rule, the blocking groups first, a rule the engine did not name kept by its id', () => {
    const groups = groupFindings(findings, rules);
    expect(groups.map((g) => g.id)).toEqual(['titleBlock', 'company', 'roomNumber']);
    expect(groups[0].label).toBe('Title block');
    expect(groups[0].blocking).toBe(true);
    expect(groups[0].findings.map((f) => f.severity)).toEqual(['warn', 'block']);
    expect(groups[1].label).toBe('company');
    expect(groups[2].blocking).toBe(false);
  });

  it('is empty for no findings', () => {
    expect(groupFindings([], rules)).toEqual([]);
  });
});

describe('the Issue dialog after an issue', () => {
  const result: IssueResult = {
    id: 'i1', number: 1, name: 'Issue 1', at: '2026-10-31T09:00:00Z', revision: 'P01', status: 'S2', container: 'Shared', folder: null, note: null, version: 1,
    files: [
      { name: 'HOUSE-CBS-ZZ-00-DR-A-0101_S2_P01.pdf', kind: 'pdf', sheetId: 's1', sha256: 'a'.repeat(64), size: 2048, stored: true },
      { name: 'HOUSE-CBS-ZZ-ZZ-M3-A-0001_S2_P01.ifc', kind: 'ifc', sha256: 'b'.repeat(64), size: 9_000_000 },
      { name: 'HOUSE-CBS-ZZ-ZZ-IE-A-0001_S2_P01.json', kind: 'json', sha256: 'c'.repeat(64), size: 400, stored: true },
    ],
    path: null, warnings: [], saved: true,
  };

  it('shows the issue made, as issued, until a new one is started', () => {
    let phase: IssuePhase = { kind: 'form' };
    phase = issuePhase(phase, { type: 'start' });
    expect(phase.kind).toBe('issuing');
    phase = issuePhase(phase, { type: 'issued', result });
    expect(phase).toEqual({ kind: 'done', result });
    // The project changing after the issue (its record, the next number) does not move what is shown: only a new issue would.
    expect(issuePhase(phase, { type: 'start' })).toBe(phase);
    expect(issuePhase(phase, { type: 'failed' })).toBe(phase);
    expect(issuePhase(phase, { type: 'new' })).toEqual({ kind: 'form' });
    // A refused issue brings the form back.
    expect(issuePhase({ kind: 'issuing' }, { type: 'failed' })).toEqual({ kind: 'form' });
  });

  it('names the issue by its own number, revision and files, the kept ones told apart', () => {
    const s = issuedSummary(result);
    expect(s.title).toBe('Issue 1 · P01 S2 · Shared');
    expect(s.files.map((f) => f.name)).toEqual(result.files.map((f) => f.name));
    expect(s.files.map((f) => f.kept)).toEqual([true, false, true]);
    expect(s.kept).toBe(2);
    expect(s.where).toBe('');
    expect(issuedSummary({ ...result, path: '/cde/Shared' }).where).toBe('/cde/Shared');
  });

  it('gives sizes as people read them', () => {
    expect(formatSize(512)).toBe('512 B');
    expect(formatSize(2048)).toBe('2 kB');
    expect(formatSize(9_000_000)).toBe('8.6 MB');
  });
});
