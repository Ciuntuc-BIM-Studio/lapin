import { describe, expect, it } from 'vitest';
import { assigneesOf, filterRemarks, noFilter, type Remark } from './remarks';

const remark = (p: Partial<Remark>): Remark => ({
  id: 'x', guid: 'g', title: 'A remark', creationDate: '2026-10-30T10:00:00Z', creationAuthor: 'ionut', labels: [], comments: [], viewpoints: [], open: true, ...p,
});

const list: Remark[] = [
  remark({ id: '1', title: 'Door too narrow', status: 'Open', priority: 'High', assignedTo: 'andrei', labels: ['doors'] }),
  remark({ id: '2', title: 'Beam through duct', status: 'Resolved', priority: 'Normal', assignedTo: 'ionut', comments: [{ guid: 'c', date: '', author: 'x', text: 'moved the duct' }] }),
  remark({ id: '3', title: 'Handrail', status: 'Closed', priority: 'Low', open: false, description: 'add one on the stairs' }),
];

describe('the remarks list', () => {
  it('shows everything without a filter', () => {
    expect(filterRemarks(list, noFilter).map((r) => r.id)).toEqual(['1', '2', '3']);
  });

  it('filters by status, priority and assignee, case blind', () => {
    expect(filterRemarks(list, { ...noFilter, status: 'open' }).map((r) => r.id)).toEqual(['1']);
    expect(filterRemarks(list, { ...noFilter, priority: 'Normal' }).map((r) => r.id)).toEqual(['2']);
    expect(filterRemarks(list, { ...noFilter, assignee: 'ANDREI' }).map((r) => r.id)).toEqual(['1']);
    expect(filterRemarks(list, { ...noFilter, status: 'Open', priority: 'Low' })).toEqual([]);
  });

  it('keeps the open ones only when asked: a closed remark goes, a resolved one stays', () => {
    expect(filterRemarks(list, { ...noFilter, open: true }).map((r) => r.id)).toEqual(['1', '2']);
  });

  it('searches the title, the description, the labels and the comments', () => {
    expect(filterRemarks(list, { ...noFilter, text: 'duct' }).map((r) => r.id)).toEqual(['2']);
    expect(filterRemarks(list, { ...noFilter, text: 'stairs' }).map((r) => r.id)).toEqual(['3']);
    expect(filterRemarks(list, { ...noFilter, text: 'DOORS' }).map((r) => r.id)).toEqual(['1']);
    expect(filterRemarks(list, { ...noFilter, text: '  ' }).map((r) => r.id)).toEqual(['1', '2', '3']);
  });

  it('names the assignees once each, sorted', () => {
    expect(assigneesOf(list)).toEqual(['andrei', 'ionut']);
  });
});
