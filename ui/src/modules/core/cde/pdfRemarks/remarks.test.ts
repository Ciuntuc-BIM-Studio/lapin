import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../../../engine/protocol';
import { countOpen, filterRemarks, groupBySheet, initials, remarkOf, remarkShape, remarksOf, type Remark } from './remarks';

const node = (id: string, type: string, name: string, props: Record<string, unknown>): NodeDto =>
  ({ id, type, name, parentId: null, px: 0, py: 0, props, editable: true }) as unknown as NodeDto;

const remark = (id: string, over: Partial<Remark> = {}): Remark => ({
  id, name: id, sheetId: 's1', kind: 'rect', rect: [100, 50, 200, 120], text: 'Move this wall', subject: null, author: 'Ana Pop', date: '2026-10-30T10:15:00+02:00', color: '#ff0000',
  status: 'open', source: { file: 'A.pdf', revision: 'P01', issueId: null, page: 1, annot: 'sq-1', read: '' }, replies: [], ...over,
});

describe('the overlay geometry', () => {
  it('draws a square as its box, a circle as an ellipse, a note as a marker alone', () => {
    const box = remarkShape(remark('r'));
    expect(box.box).toEqual([100, 50, 200, 120]);
    expect(box.lines).toEqual([]);
    expect(box.ellipse).toBe(false);
    expect(box.marker).toEqual([100, 50]);
    expect(remarkShape(remark('e', { kind: 'ellipse' })).ellipse).toBe(true);
    const note = remarkShape(remark('n', { kind: 'note', rect: [30, 34, 36, 40] }));
    expect(note.box).toBeNull();
    expect(note.marker).toEqual([30, 34]);
  });

  it('draws ink and lines open, highlights and clouds closed, the marker at the first point when there is no box', () => {
    const ink = remarkShape(remark('i', { kind: 'ink', rect: undefined, paths: [[[10, 150], [35, 175], [60, 200]]] }));
    expect(ink.closed).toBe(false);
    expect(ink.box).toBeNull();
    expect(ink.lines).toHaveLength(1);
    expect(ink.marker).toEqual([10, 150]);
    const hl = remarkShape(remark('h', { kind: 'highlight', rect: [250, 60, 300, 70], paths: [[[250, 60], [300, 60], [300, 70], [250, 70]]] }));
    expect(hl.closed).toBe(true);
    expect(hl.box).toBeNull();
    expect(remarkShape(remark('c', { kind: 'cloud', paths: [[[0, 0], [1, 0], [0, 1]]] })).closed).toBe(true);
    // A line with no points left falls back to its box.
    expect(remarkShape(remark('l', { kind: 'line', paths: [] })).box).toEqual([100, 50, 200, 120]);
  });
});

describe('the list', () => {
  const nodes = new Map<string, NodeDto>([
    ['s1', node('s1', 'LapinSheet', 'Ground floor plan', { number: 'A-101' })],
    ['s2', node('s2', 'LapinSheet', 'First floor plan', { number: 'A-102' })],
    ['r1', node('r1', 'LapinSheetRemark', 'Move this wall', { sheetId: 's1', kind: 'rect', rect: [1, 2, 3, 4], text: 'Move this wall', author: 'Ana', date: '2026-10-30T10:00:00Z', status: 'open', source: { file: 'a.pdf', annot: 'x', page: 1 }, replies: [{ annot: 'y', author: 'Ionut', date: null, text: 'Done' }] })],
    ['r2', node('r2', 'LapinSheetRemark', 'Check', { sheetId: 's1', kind: 'note', text: 'Check', status: 'resolved', source: { file: 'a.pdf', annot: 'z' } })],
    ['r3', node('r3', 'LapinSheetRemark', 'Old', { sheetId: 's2', kind: 'ink', paths: [[[0, 0], [1, 1]]], status: 'closed', source: { file: 'b.pdf', annot: 'w' } })],
    ['r4', node('r4', 'LapinSheetRemark', 'Odd', { sheetId: 's2', kind: 'stamp', status: 'whatever', source: {} })],
    ['w', node('w', 'IfcWall', 'Wall', {})],
  ]);

  it('reads a remark from its node, with a status it knows', () => {
    const r = remarkOf(nodes.get('r1')!);
    expect(r.sheetId).toBe('s1');
    expect(r.rect).toEqual([1, 2, 3, 4]);
    expect(r.replies).toHaveLength(1);
    expect(r.source.annot).toBe('x');
    expect(remarkOf(nodes.get('r4')!).status).toBe('open');
    expect(remarkOf(nodes.get('r4')!).rect).toBeUndefined();
  });

  it('lists the remarks of a sheet, counts the open ones (not resolved, not closed), filters by status and groups by sheet in number order', () => {
    expect(remarksOf(nodes).map((r) => r.id)).toEqual(['r2', 'r4', 'r3', 'r1']);   // undated first by name, then by date
    expect(remarksOf(nodes, 's2').map((r) => r.id)).toEqual(['r4', 'r3']);
    expect(countOpen(nodes, 's1')).toBe(1);   // r1 open; r2 resolved is not counted
    expect(countOpen(nodes, 's2')).toBe(1);
    const all = remarksOf(nodes);
    expect(filterRemarks(all, 'open').map((r) => r.id)).toEqual(['r4', 'r1']);
    expect(filterRemarks(all, 'closed').map((r) => r.id)).toEqual(['r3']);
    expect(filterRemarks(all, 'all')).toHaveLength(4);
    const groups = groupBySheet(all, nodes);
    expect(groups.map((g) => g.label)).toEqual(['A-101 · Ground floor plan', 'A-102 · First floor plan']);
    expect(groups[0].remarks.map((r) => r.id)).toEqual(['r2', 'r1']);
    // A sheet that is gone keeps its remarks under its id.
    expect(groupBySheet([remark('x', { sheetId: 'gone' })], nodes)[0].label).toBe('gone');
  });

  it('makes initials of the author', () => {
    expect(initials('Ana Pop')).toBe('AP');
    expect(initials('ionut')).toBe('IO');
    expect(initials('Ionuț Lucian Ciuntuc')).toBe('IC');
    expect(initials('ana.pop')).toBe('AP');
    expect(initials('')).toBe('?');
    expect(initials(null)).toBe('?');
  });
});
