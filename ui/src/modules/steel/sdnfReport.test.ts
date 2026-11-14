import { describe, expect, it } from 'vitest';
import { durationText, groupItems, plainNotes, reportFileName, sdnfReportText, sdnfSummary, type SdnfImportReport } from './sdnfReport';

const report: SdnfImportReport = {
  name: 'hall.sdnf', version: 'SDNF Version 3.0', source: 'Tekla',
  made: 1200, updated: 3, remade: 1, plates: 40, holes: 160, grids: 1, skipped: 3,
  byClass: { IfcBeam: 700, IfcColumn: 500 },
  unknownProfiles: ['W12X50'],
  notes: [
    'The member loads (packet 30) were left out: Lapin keeps no loads.',
    'Left out, members shorter than a millimetre: 2 — member 7 (line 80), member 9 (line 100).',
    'Changed, holes whose curved edges came in straight between their vertices: 1 — hole 4 (line 900).',
  ],
  packets: [
    { packet: '10', line: 11, announced: 1206, read: 1206, imported: 1204 },
    { packet: '20', line: 12100, announced: 41, read: 40, imported: 40 },
    { packet: '30', line: 13000, announced: 0, read: 0, imported: 0 },
    { packet: '70', line: 13010, announced: 0, read: 0, imported: 0 },
  ],
  skips: [
    { reason: 'members shorter than a millimetre', count: 2, items: ['member 7 (line 80)', 'member 9 (line 100)'] },
    { reason: 'members in the file twice (the second left out)', count: 250, items: ['member 1 (line 20)', 'member 2 (line 30)'] },
  ],
  warnings: [{ reason: 'holes whose curved edges came in straight between their vertices', count: 1, items: ['hole 4 (line 900)'] }],
  lines: 13020,
  ms: 2400,
};

describe('the SDNF import protocol (report #148)', () => {
  it('says the time as a person reads it', () => {
    expect(durationText(380)).toBe('380 ms');
    expect(durationText(2400)).toBe('2.4 s');
    expect(durationText(125_000)).toBe('2 min 5 s');
  });

  it('heads with one phrase the languages translate', () => {
    expect(sdnfSummary(report)).toBe('Imported hall.sdnf: 1200 new, 4 updated, 40 plates, 160 holes, 1 grids, 3 left out.');
  });

  it('lists a group\'s first items and how many more it has', () => {
    expect(groupItems(report.skips![0])).toBe('member 7 (line 80), member 9 (line 100)');
    expect(groupItems(report.skips![1], 1)).toBe('member 1 (line 20) and 249 more');
  });

  it('keeps the notes the groups do not already say', () => {
    expect(plainNotes(report)).toEqual(['The member loads (packet 30) were left out: Lapin keeps no loads.']);
  });

  it('writes every packet, group, item and note as text, with the time', () => {
    const text = sdnfReportText(report, 9_500, new Date('2026-11-14T10:20:30Z'));
    const lines = text.split('\n');
    expect(lines[0]).toBe('SDNF import protocol — hall.sdnf');
    expect(lines[1]).toBe('Date: 2026-11-14 10:20:30 UTC');
    expect(text).toContain('File: SDNF Version 3.0, written by Tekla, 13020 lines');
    expect(text).toContain('Time: 9.5 s (reading and the model 2.4 s, the rest building the bodies)');
    expect(text).toContain('Elements: 1200 new, 3 updated, 1 made again, 3 left out.');
    expect(text).toContain('By class: IfcBeam 700, IfcColumn 500.');
    expect(text).toContain('  Packet 10 (line 11), linear members: 1206 read, 1204 imported');
    expect(text).toContain('  Packet 20 (line 12100), plates: 40 read of 41 announced, 40 imported');
    expect(text).toContain('  Packet 30 (line 13000), member loads (not imported)');
    expect(text).toContain('  Packet 70 (line 13010), not defined by SDNF (not imported)');
    expect(text).toContain('Left out:\n  members shorter than a millimetre: 2\n    member 7 (line 80)\n    member 9 (line 100)\n');
    expect(text).toContain('    member 2 (line 30)\n    … and 248 more');
    expect(text).toContain('Changed:\n  holes whose curved edges came in straight between their vertices: 1\n    hole 4 (line 900)');
    expect(text).toContain('Profiles in no library (drawn as placeholders): W12X50');
    expect(text).toContain('Notes:\n  The member loads (packet 30) were left out: Lapin keeps no loads.\n');
    expect(text).not.toContain('Left out, members');
    expect(text.endsWith('\n')).toBe(true);
  });

  it('names its file after the SDNF', () => {
    expect(reportFileName('hall.sdnf')).toBe('hall-import.txt');
    expect(reportFileName('frame.v2.dat')).toBe('frame.v2-import.txt');
  });
});
