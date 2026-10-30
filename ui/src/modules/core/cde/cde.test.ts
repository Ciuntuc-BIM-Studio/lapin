import { describe, expect, it } from 'vitest';
import { containerFor, groupFindings, isRevision, nextRevision, type Finding } from './cde';

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
