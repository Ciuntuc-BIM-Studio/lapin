import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../../engine/protocol';
import { daysBetween, expiryCounts, expiryOf, isoDay } from './expiry';
import {
  answerCandidates, badgeOf, fileKind, filterRefs, flattenFolders, folderIds, folderLanguage, hasNoReferenceSettings, nameIn, NO_FILTER, phaseChoices, requiresOf, sortRefs, splitList,
  type RefDoc, type RefFolder,
} from './references';

const NOW = '2026-11-25';
const doc = (id: string, over: Partial<RefDoc> = {}): RefDoc => ({ id, title: id, status: 'received', expired: false, files: [], ...over });
const folder = (id: string, children: RefFolder[] = [], derived?: string): RefFolder => ({ id, name: id, text: id, count: 0, total: 0, children, ...(derived ? { derived } : {}) });
const tree = [folder('input', [folder('input-cu'), folder('input-cf')]), folder('permits', [folder('permits-fire')]), folder('issued', [], 'sheets')];

describe('validity', () => {
  it('counts days between ISO days and reads the local day', () => {
    expect(daysBetween('2026-11-25', '2026-12-25')).toBe(30);
    expect(daysBetween('2026-11-25', '2026-11-24')).toBe(-1);
    expect(daysBetween('2026-12-31', '2027-01-01')).toBe(1);
    expect(isoDay(new Date(2026, 10, 5))).toBe('2026-11-05');
  });

  it('is expiring within 30 days, expired after, and neither for what is not received', () => {
    expect(expiryOf('received', '2026-12-25', NOW)).toEqual({ kind: 'expiring', days: 30 });
    expect(expiryOf('received', '2026-12-26', NOW)).toBeNull();
    expect(expiryOf('received', '2026-11-25', NOW)).toEqual({ kind: 'expiring', days: 0 });
    expect(expiryOf('received', '2026-11-24', NOW)).toEqual({ kind: 'expired', days: 1 });
    expect(expiryOf('expired', '2026-01-01', NOW)?.kind).toBe('expired');
    expect(expiryOf('withdrawn', '2026-01-01', NOW)).toBeNull();
    expect(expiryOf('superseded', '2026-11-30', NOW)).toBeNull();
    expect(expiryOf('requested', '2026-11-30', NOW)).toBeNull();
    expect(expiryOf('received', undefined, NOW)).toBeNull();
  });

  it('counts the expiring and the expired among the nodes, hiding nothing else', () => {
    const n = (id: string, type: string, props: Record<string, unknown>) => [id, { id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null } as NodeDto] as const;
    const nodes = new Map([
      n('a', 'LapinReference', { status: 'received', validUntil: '2026-12-01' }),
      n('b', 'LapinReference', { status: 'received', validUntil: '2026-11-01' }),
      n('c', 'LapinReference', { status: 'withdrawn', validUntil: '2026-11-01' }),
      n('d', 'LapinReference', { status: 'received', validUntil: '2028-01-01' }),
      n('e', 'IfcWall', { validUntil: '2026-11-01' }),
    ]);
    expect(expiryCounts(nodes, NOW)).toEqual({ expiring: 1, expired: 1 });
    expect(expiryCounts(new Map(), NOW)).toEqual({ expiring: 0, expired: 0 });
  });
});

describe('badges', () => {
  it('says the status, with the days when the validity is running out', () => {
    expect(badgeOf({ status: 'received', validUntil: '2027-06-01' }, NOW)).toEqual({ label: 'Received', tone: 'ok', expiry: null });
    expect(badgeOf({ status: 'received', validUntil: '2026-12-10' }, NOW)).toEqual({ label: 'Received', tone: 'warn', expiry: { kind: 'expiring', days: 15 } });
    expect(badgeOf({ status: 'expired', validUntil: '2026-11-20' }, NOW)).toEqual({ label: 'Expired', tone: 'bad', expiry: { kind: 'expired', days: 5 } });
    expect(badgeOf({ status: 'requested' }, NOW).tone).toBe('warn');
    expect(badgeOf({ status: 'withdrawn' }, NOW).tone).toBe('muted');
    expect(badgeOf({ status: 'superseded', validUntil: '2026-01-01' }, NOW).expiry).toBeNull();
  });
});

describe('the list', () => {
  const docs = [
    doc('cu1', { title: 'Certificat 123', type: 'cu', typeName: 'Urbanism certificate', folder: 'input-cu', number: '123', issuer: 'Primăria', validUntil: '2026-12-10', phase: ['SF'] }),
    doc('fire', { title: 'Aviz ISU', type: 'aviz', folder: 'permits-fire', issuer: 'ISU', validUntil: '2026-10-01', status: 'expired' }),
    doc('cf', { title: 'Extras CF', type: 'cf', folder: 'input-cf', phase: ['DTAC', 'PT'] }),
    doc('old', { title: 'Old', type: 'aviz', folder: 'permits-fire', status: 'withdrawn' }),
    doc('wait', { title: 'Water', type: 'aviz', status: 'requested' }),
  ];
  const ids = (l: RefDoc[]) => l.map((d) => d.id);

  it('takes a folder with its subfolders', () => {
    expect([...folderIds(tree, 'input')].sort()).toEqual(['input', 'input-cf', 'input-cu']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, folder: 'input' }, tree, NOW))).toEqual(['cu1', 'cf']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, folder: 'permits-fire' }, tree, NOW))).toEqual(['fire', 'old']);
    expect(filterRefs(docs, NO_FILTER, tree, NOW)).toHaveLength(5);
  });

  it('filters by type, phase, status and text', () => {
    expect(ids(filterRefs(docs, { ...NO_FILTER, type: 'aviz' }, tree, NOW))).toEqual(['fire', 'old', 'wait']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, phase: 'pt' }, tree, NOW))).toEqual(['cf']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, status: 'expired' }, tree, NOW))).toEqual(['fire']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, q: 'isu' }, tree, NOW))).toEqual(['fire']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, q: '123' }, tree, NOW))).toEqual(['cu1']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, q: 'urbanism' }, tree, NOW))).toEqual(['cu1']);
  });

  it('lists the received documents whose validity ends within N days', () => {
    expect(ids(filterRefs(docs, { ...NO_FILTER, within: 30 }, tree, NOW))).toEqual(['cu1']);
    expect(ids(filterRefs(docs, { ...NO_FILTER, within: 5 }, tree, NOW))).toEqual([]);
  });

  it('sorts by a column with the empty ones last', () => {
    expect(ids(sortRefs(docs, 'issuer'))).toEqual(['fire', 'cu1', 'cf', 'old', 'wait']);
    expect(ids(sortRefs(docs, 'issuer', true))).toEqual(['cu1', 'fire', 'cf', 'old', 'wait']);
    expect(ids(sortRefs(docs, 'title'))[0]).toBe('fire');
  });

  it('flattens the folders for a picker, the derived ones out unless asked', () => {
    expect(flattenFolders(tree).map((f) => `${f.depth}${f.id}`)).toEqual(['0input', '1input-cu', '1input-cf', '0permits', '1permits-fire']);
    expect(flattenFolders(tree, true).at(-1)).toMatchObject({ id: 'issued', derived: 'sheets' });
  });
});

describe('the checklist and the files', () => {
  it('offers the live documents, the item\'s own folder first', () => {
    const docs = [doc('a', { title: 'B', folder: 'x' }), doc('b', { title: 'A', folder: 'water' }), doc('c', { status: 'withdrawn' }), doc('d', { status: 'superseded' }), doc('self')];
    expect(answerCandidates(docs, 'self', 'water').map((d) => d.id)).toEqual(['b', 'a']);
  });

  it('hands the engine a key, a label and the linked document', () => {
    expect(requiresOf([{ key: 'k', label: { en: 'K' }, text: 'K', state: 'missing' }, { key: 'w', label: 'W', text: 'W', state: 'received', document: 'd1' }])).toEqual([{ key: 'k', label: { en: 'K' } }, { key: 'w', label: 'W', document: 'd1' }]);
  });

  it('tells what the preview does with a file', () => {
    expect(fileKind('CU.PDF')).toBe('pdf');
    expect(fileKind('x', 'application/pdf')).toBe('pdf');
    expect(fileKind('plan.png')).toBe('image');
    expect(fileKind('scan', 'image/jpeg')).toBe('image');
    expect(fileKind('model.ifc')).toBe('ifc');
    expect(fileKind('Structura.IFCZIP')).toBe('ifc');
    expect(fileKind('notes.txt')).toBe('text');
    expect(fileKind('drawing.dwg')).toBe('other');
  });
});

describe('names, phases and the first open', () => {
  it('reads a name in a language, else English, else Romanian', () => {
    expect(nameIn({ ro: 'Aviz', en: 'Permit' }, 'ro')).toBe('Aviz');
    expect(nameIn({ ro: 'Aviz', en: 'Permit' }, 'de')).toBe('Permit');
    expect(nameIn({ ro: 'Aviz' }, 'de')).toBe('Aviz');
    expect(nameIn('Plain', 'ro')).toBe('Plain');
    expect(nameIn(undefined, 'ro')).toBe('');
  });

  it('names the folders in Romanian for a Romanian interface, English otherwise', () => {
    expect(folderLanguage('ro')).toBe('ro');
    expect(folderLanguage('de')).toBe('en');
    expect(folderLanguage('en')).toBe('en');
  });

  it('knows a project that has no reference settings yet', () => {
    const project = (props: Record<string, unknown>) => new Map<string, NodeDto>([['p', { id: 'p', type: 'IfcProject', name: 'P', parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null } as NodeDto]]);
    expect(hasNoReferenceSettings(project({}))).toBe(true);
    expect(hasNoReferenceSettings(project({ references: { language: 'en' } }))).toBe(false);
    expect(hasNoReferenceSettings(new Map())).toBe(false);
  });

  it('splits and merges phases', () => {
    expect(splitList('SF, DTAC;PT ,, ')).toEqual(['SF', 'DTAC', 'PT']);
    expect(phaseChoices([doc('a', { phase: ['sf', 'AC'] })], ['Concept'])).toEqual(['Concept', 'sf', 'AC', 'DTAC', 'PT', 'DE']);
  });
});
