import { beforeEach, describe, expect, it, vi } from 'vitest';

const stored = new Map<string, string>();
vi.stubGlobal('localStorage', {
  getItem: (k: string) => stored.get(k) ?? null,
  setItem: (k: string, v: string) => void stored.set(k, v),
  removeItem: (k: string) => void stored.delete(k),
  clear: () => stored.clear(),
});

import {
  bounds, dragHandle, fillText, forgetBlock, freeName, insertToken, isTitleBlockFile, itemAt, keepBlock, keyPoints, libraryBlocks, moveItem,
  normalise, renameBlock, snap, textRuns, tokensOf, type TbItem,
} from './titleBlockModel';

const values = new Map<string, string | null>([['project.name', 'Kesämökki'], ['project.client', null], ['sheet.number', 'A-101']]);

describe('live text in the designer', () => {
  it('fills the tokens it knows, shows an empty field as its token and an unknown one as <key>', () => {
    expect(tokensOf('Project: {project.name} – {{sheet.number}}')).toEqual(['project.name', 'sheet.number']);
    expect(textRuns('Project: {project.name}', values)).toEqual([{ text: 'Project: ', kind: 'plain' }, { text: 'Kesämökki', kind: 'value', key: 'project.name' }]);
    expect(textRuns('{project.client}', values)).toEqual([{ text: '{project.client}', kind: 'empty', key: 'project.client' }]);
    expect(textRuns('{project.nope}!', values)).toEqual([{ text: '<project.nope>', kind: 'unknown', key: 'project.nope' }, { text: '!', kind: 'plain' }]);
    // As the sheet prints it: an empty field prints nothing.
    expect(fillText('Client: {project.client}| {sheet.number}', values)).toBe('Client: | A-101');
  });

  it('puts a token at the end of a text, a space before it', () => {
    expect(insertToken('', 'sheet.number')).toBe('{sheet.number}');
    expect(insertToken('Sheet', 'sheet.number')).toBe('Sheet {sheet.number}');
    expect(insertToken('Sheet ', 'sheet.number')).toBe('Sheet {sheet.number}');
  });
});

describe('the designer\'s geometry', () => {
  const items: TbItem[] = [
    { type: 'rect', x: 0, y: 0, w: 180, h: 50, stroke: 0.5 },
    { type: 'line', a: [0, 30], b: [180, 30] },
    { type: 'text', at: [3, 35], text: '{project.name}', h: 5 },
  ];

  it('snaps to the items\' corners, ends and middles first, then to the grid', () => {
    expect(snap([179.4, 29.6], items, { grid: 1, tol: 1 })).toEqual({ p: [180, 30], on: 'point' });
    expect(snap([90.3, 30.4], items, { grid: 1, tol: 1 })).toEqual({ p: [90, 30], on: 'point' });   // the line's middle
    expect(snap([42.26, 12.74], items, { grid: 0.5, tol: 1 })).toEqual({ p: [42.5, 12.5], on: 'grid' });
    expect(snap([42.26, 12.74], items, { grid: 0, tol: 1, objects: false })).toEqual({ p: [42.26, 12.74], on: 'none' });
    // Ortho from a start point: the larger of the two moves kept.
    expect(snap([60.2, 33.1], items, { grid: 1, tol: 0.5, from: [10, 31], ortho: true })).toEqual({ p: [60, 31], on: 'grid' });
    // An item's own points are not snapped to while it is dragged.
    expect(snap([179.6, 29.7], items, { grid: 5, tol: 1, except: 1 }).p).toEqual([180, 30]);
    expect(snap([90.4, 30.3], items, { grid: 5, tol: 1, except: 1 }).p).toEqual([90, 30]);
  });

  it('moves, reshapes and hits items', () => {
    expect(moveItem(items[1], 2.5, -1)).toEqual({ type: 'line', a: [2.5, 29], b: [182.5, 29] });
    expect(moveItem(items[0], 1, 1)).toMatchObject({ x: 1, y: 1, w: 180, h: 50 });
    // A rectangle's corner dragged: the opposite corner stays.
    expect(dragHandle(items[0], 2, [100, 40])).toMatchObject({ x: 0, y: 0, w: 100, h: 40 });
    expect(dragHandle(items[0], 0, [190, 60])).toMatchObject({ x: 180, y: 50, w: 10, h: 10 });
    expect(keyPoints(items[1])).toContainEqual([90, 30]);
    expect(bounds(items[2])[0]).toBe(3);
    expect(itemAt(items, [90, 30.2], 0.5)).toBe(1);
    expect(itemAt(items, [5, 37], 0.5)).toBe(2);
    expect(itemAt(items, [100, 10], 0.5)).toBe(-1);   // inside an unfilled box: nothing
    expect(itemAt(items, [100, 0.2], 0.5)).toBe(0);   // on its edge
  });

  it('reads a template the engine sent, filling what it leaves out', () => {
    expect(normalise({ w: 100, items: [] })).toEqual({ w: 100, h: 50, anchor: 'bottomRight', frame: { show: true, left: 10, top: 10, right: 10, bottom: 10 }, revisions: 'above', items: [] });
  });
});

describe('this computer\'s title blocks', () => {
  beforeEach(() => localStorage.clear());

  it('are kept, renamed and forgotten, and only title block files are taken', () => {
    const file = { name: 'Finnish', template: normalise({ items: [{ type: 'text', at: [1, 1], text: '{project.name}' }] }) };
    expect(isTitleBlockFile(file)).toBe(true);
    expect(isTitleBlockFile({ name: 'x', blocks: [] })).toBe(false);
    const kept = keepBlock(file)!;
    expect(libraryBlocks().map((b) => b.name)).toEqual(['Finnish']);
    expect(renameBlock(kept.id, 'Suomi')).toBe(true);
    expect(libraryBlocks()[0].name).toBe('Suomi');
    forgetBlock(kept.id);
    expect(libraryBlocks()).toEqual([]);
    expect(freeName('Cartuș', ['cartuș', 'Cartuș 2'])).toBe('Cartuș 3');
  });
});
