import { describe, expect, it } from 'vitest';
import { address, colName, colsOp, fieldInsert, grow, inSel, insertAt, linkOps, linkable, sizeAt, sizeGrips, isTableName, opacityOf, parseAddress, pasteBlock, rangeOf, refInsert, rowsOp, tableRef, withOpacity, type TableInfo } from './tableModel';

describe('drafting table addresses', () => {
  it('names columns and cells as a spreadsheet does', () => {
    expect(colName(0)).toBe('A');
    expect(colName(25)).toBe('Z');
    expect(colName(26)).toBe('AA');
    expect(colName(51)).toBe('AZ');
    expect(address(2, 1)).toBe('B3');
    expect(parseAddress('aa1')).toEqual({ r: 0, c: 26 });
    expect(parseAddress('A0')).toBeNull();
    expect(parseAddress('sheet.number')).toBeNull();
  });

  it('writes a selection as its range, either way round', () => {
    expect(rangeOf({ r0: 3, c0: 2, r1: 0, c1: 0 })).toBe('A1:C4');
    expect(rangeOf({ r0: 1, c0: 1, r1: 1, c1: 1 })).toBe('B2');
    expect(inSel({ r0: 2, c0: 2, r1: 0, c1: 0 }, 1, 1)).toBe(true);
    expect(inSel(null, 0, 0)).toBe(false);
  });
});

describe('drafting table selection', () => {
  const cell = (t = '') => ({ t, shown: t });
  // A1:B1 merged (its master A1), the rest single.
  const info: TableInfo = {
    rows: 2, cols: 3, colW: [25, 25, 25], rowH: [7, 7], errors: [],
    cells: [[{ ...cell('Title'), cs: 2 }, { ...cell(), covered: 'A1' }, cell()], [cell(), cell(), cell()]],
  };

  it('grows to take a merged cell in whole', () => {
    expect(grow({ r0: 0, c0: 1, r1: 0, c1: 1 }, info)).toEqual({ r0: 0, c0: 0, r1: 0, c1: 1 });
    expect(grow({ r0: 1, c0: 1, r1: 0, c1: 2 }, info)).toEqual({ r0: 0, c0: 0, r1: 1, c1: 2 });
  });

  it('puts rows and columns in beside the selection, or takes its own out', () => {
    const s = { r0: 1, c0: 2, r1: 0, c1: 1 };
    expect(rowsOp('above', s)).toEqual({ op: 'insertRows', at: 0, count: 1 });
    expect(rowsOp('below', s)).toEqual({ op: 'insertRows', at: 2, count: 1 });
    expect(rowsOp('delete', s)).toEqual({ op: 'deleteRows', at: 0, count: 2 });
    expect(colsOp('left', s)).toEqual({ op: 'insertCols', at: 1, count: 1 });
    expect(colsOp('right', s)).toEqual({ op: 'insertCols', at: 3, count: 1 });
    expect(colsOp('delete', s)).toEqual({ op: 'deleteCols', at: 1, count: 2 });
  });
});

describe('drafting table colours and paste', () => {
  it('keeps an opacity in the colour, as the engine reads it', () => {
    expect(withOpacity('#FF0000', 1)).toBe('#ff0000');
    expect(withOpacity('#ff000080', 0.25)).toBe('#ff000040');
    expect(opacityOf('#00ff0080')).toBeCloseTo(128 / 255);
    expect(opacityOf('#00ff00')).toBe(1);
    expect(opacityOf(null)).toBe(1);
  });

  it('reads a spreadsheet\'s cells pasted as rows of texts', () => {
    expect(pasteBlock('a\tb\r\nc\td\n')).toEqual([['a', 'b'], ['c', 'd']]);
    expect(pasteBlock('one')).toEqual([['one']]);
  });
});

describe('drafting table references to other tables', () => {
  it('names a table by one word a formula can read', () => {
    expect(isTableName('Areas')).toBe(true);
    expect(isTableName('room_areas2')).toBe(true);
    expect(isTableName('Table 1')).toBe(false);
    expect(isTableName('2nd')).toBe(false);
    expect(isTableName('')).toBe(false);
  });

  it('writes a cell or a range of another table as a formula reads it', () => {
    expect(tableRef('Areas', { r0: 3, c0: 1, r1: 3, c1: 1 })).toBe('table.Areas.B4');
    expect(tableRef('Areas', { r0: 8, c0: 1, r1: 1, c1: 1 })).toBe('table.Areas.B2:B9');
  });

  it('puts the reference in a formula as it is, elsewhere as a token or a total', () => {
    expect(refInsert('{= 2 * ', 7, 'table.A.B4')).toBe('table.A.B4');
    expect(refInsert('Total: ', 7, 'table.A.B4')).toBe('{table.A.B4}');
    expect(refInsert('{= 1} and ', 10, 'table.A.B2:B9')).toBe('{= sum(table.A.B2:B9)}');
    expect(insertAt('ab', 1, 1, 'X')).toEqual({ text: 'aXb', caret: 2 });
    expect(insertAt('abcd', 1, 3, 'X')).toEqual({ text: 'aXd', caret: 2 });
  });
});

describe('drafting table links to the model', () => {
  const types: Record<string, string> = { w1: 'IfcWall', w2: 'IfcWall', t: 'IfcAnnotation', s: 'IfcBuildingStorey', wt: 'IfcWallType', d: 'IfcDoor' };
  it('links model elements only, never the table itself', () => {
    expect(linkable(['t', 'w1', 's', 'wt', 'd', 'nope'], (id) => types[id], 't')).toEqual(['w1', 'd']);
  });

  it('links cells, a row each from the first selected row, or the table, and unlinks', () => {
    const s = { r0: 2, c0: 1, r1: 1, c1: 2 };
    expect(linkOps('cells', s, ['w1', 'w2'])).toEqual([{ op: 'link', range: 'B2:C3', el: 'w1' }]);
    expect(linkOps('rows', s, ['w1', 'w2'])).toEqual([{ op: 'linkRows', at: 'A2', els: ['w1', 'w2'] }]);
    expect(linkOps('table', s, ['w2'])).toEqual([{ op: 'link', on: 'table', el: 'w2' }]);
    expect(linkOps('unlink', s, [])).toEqual([{ op: 'link', range: 'B2:C3', el: null }, { op: 'link', on: 'rows', range: 'B2:C3', el: null }]);
    expect(linkOps('cells', s, [])).toEqual([]);
  });

  it('puts a field in as a token, or its name alone inside a formula', () => {
    expect(fieldInsert('', 0, 'FireRating')).toBe('{el.FireRating}');
    expect(fieldInsert('{= 2 * ', 7, 'NetArea')).toBe('el.NetArea');
  });
});

describe('drafting table grips on the drawing', () => {
  it('puts a grip at each column\'s end along the top and each row\'s foot along the left, turned with the table', () => {
    const g = sizeGrips({ x: 1, y: 5 }, 0, 0.1, [25, 10], [7, 8]);
    expect(g.map((x) => x.key)).toEqual(['col:0', 'col:1', 'row:0', 'row:1']);
    expect(g[1].at.x).toBeCloseTo(4.5);
    expect(g[1].at.y).toBeCloseTo(5);
    expect(g[3].at.y).toBeCloseTo(3.5);
    const turned = sizeGrips({ x: 0, y: 0 }, 90, 0.1, [10], [5]);
    expect(turned[0].at.x).toBeCloseTo(0);
    expect(turned[0].at.y).toBeCloseTo(1);
    // A long table's first part: its rows keep their own numbers.
    expect(sizeGrips({ x: 0, y: 0 }, 0, 1, [1], [1, 1], [0, 3]).map((x) => x.key)).toEqual(['col:0', 'row:0', 'row:3']);
  });

  it('reads a dropped grip as the width or height from where its column or row starts, to a tenth', () => {
    const [col] = sizeGrips({ x: 1, y: 5 }, 0, 0.1, [25], [7]);
    expect(sizeAt(col, { x: 1 + 3.04, y: 9 }, { x: 1, y: 5 }, 0, 0.1, 0)).toBe(30.4);
    const row = sizeGrips({ x: 1, y: 5 }, 0, 0.1, [25], [7, 7])[2];
    expect(sizeAt(row, { x: 0, y: 5 - 1.9 }, { x: 1, y: 5 }, 0, 0.1, 7)).toBe(12);
    expect(sizeAt(row, { x: 0, y: 5 }, { x: 1, y: 5 }, 0, 0.1, 7)).toBe(1);
  });
});
