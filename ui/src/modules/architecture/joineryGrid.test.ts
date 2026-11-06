import { describe, expect, it } from 'vitest';
import { merge, neighbour, normalised, resized, shifted, split, tidy, type GridSpec } from './joineryGrid';

const frame = { mullion: 0.05, transom: 0.05 };

describe('the pane editor', () => {
  it('sets a column in mm, the last taking up the rest, and drags a grid line', () => {
    expect(resized([500, 500, 500], 0, 700)).toEqual([700, 500, 300]);
    expect(resized([500, 500, 500], 2, 600)).toEqual([500, 400, 600]);
    expect(resized([500, 500], 0, 980)).toBeNull();
    expect(shifted([500, 500], 0, 120)).toEqual([620, 380]);
    expect(shifted([500, 500], 0, 480)).toBeNull();
  });

  it('splits a pane where no line is: a new column, the panes across it spanning both', () => {
    // Two rows, one column 1000 mm: the bottom pane split 300 mm from its left.
    const s: GridSpec = { columns: [1000], rows: [600, 900], frame, panels: [{ col: 0, row: 0, operation: 'side-left' }, { col: 0, row: 1, operation: 'fixed' }] };
    const r = split(s, [1000], { col: 0, row: 0, operation: 'side-left' }, 'col', 300)!;
    expect(r.sizes).toEqual([275, 675]);
    const ps = normalised(r.spec);
    expect(ps).toHaveLength(3);
    expect(ps.find((p) => p.row === 1)).toMatchObject({ col: 0, colSpan: 2 });
    expect(ps.filter((p) => p.row === 0).map((p) => [p.col, p.colSpan, p.operation])).toEqual([[0, 1, 'side-left'], [1, 1, 'side-left']]);
    // Under 50 mm: refused.
    expect(split(s, [1000], { col: 0, row: 0, operation: 'side-left' }, 'col', 40)).toBeNull();
  });

  it('splits a spanning pane on a grid line it covers without adding one', () => {
    const s: GridSpec = { columns: [500, 500], rows: [1000], frame, panels: [{ col: 0, row: 0, operation: 'fixed', colSpan: 2 }] };
    const r = split(s, [500, 500], { col: 0, row: 0, operation: 'fixed' }, 'col', 525)!;
    expect(r.sizes).toEqual([500, 500]);
    expect(normalised(r.spec).map((p) => [p.col, p.colSpan])).toEqual([[0, 1], [1, 1]]);
  });

  it('merges two panes sharing a whole edge and takes out the line no pane needs', () => {
    // A transom over two sashes: the two top cells merged, the bottom ones kept apart.
    const s: GridSpec = { columns: [500, 500], rows: [1000, 400], frame, panels: [
      { col: 0, row: 0, operation: 'side-left' }, { col: 1, row: 0, operation: 'side-right' }, { col: 0, row: 1, operation: 'fixed' }, { col: 1, row: 1, operation: 'fixed' }] };
    const sizes = { cols: [500, 500], rows: [1000, 400] };
    expect(neighbour(s, { col: 0, row: 1, operation: 'fixed' }, 'right')).toMatchObject({ col: 1, row: 1 });
    const r = merge(s, sizes, { col: 0, row: 1, operation: 'fixed' }, 'right')!;
    expect(r.sizes.cols).toEqual([500, 500]);
    expect(normalised(r.spec).find((p) => p.row === 1)).toMatchObject({ col: 0, colSpan: 2 });
    // Then the bottom ones too: no pane needs the mullion any more, so the columns become one.
    const r2 = merge(r.spec, r.sizes, { col: 0, row: 0, operation: 'side-left' }, 'right')!;
    expect(r2.sizes.cols).toEqual([1050]);
    expect(normalised(r2.spec).map((p) => [p.col, p.row, p.colSpan ?? 1])).toEqual([[0, 1, 1], [0, 0, 1]]);
    // Not a whole edge: no merge.
    const odd: GridSpec = { ...s, panels: [{ col: 0, row: 0, operation: 'fixed', rowSpan: 2 }, { col: 1, row: 0, operation: 'fixed' }, { col: 1, row: 1, operation: 'fixed' }] };
    expect(merge(odd, sizes, { col: 0, row: 0, operation: 'fixed' }, 'right')).toBeNull();
  });

  it('keeps every line a pane still needs', () => {
    const s: GridSpec = { columns: [400, 400], rows: [1000], frame, panels: [{ col: 0, row: 0, operation: 'fixed' }, { col: 1, row: 0, operation: 'fixed' }] };
    expect(tidy(s, { cols: [400, 400], rows: [1000] }).sizes.cols).toEqual([400, 400]);
  });

  it('splits a door leaf across into the leaf and a fixed glazed fanlight over it', () => {
    const s: GridSpec = { columns: [900], rows: [2050], frame, panels: [{ col: 0, row: 0, operation: 'swing-left' }] };
    const r = split(s, [2050], { col: 0, row: 0, operation: 'swing-left' }, 'row', 1700)!;
    const ps = normalised(r.spec);
    expect(ps.find((p) => p.row === 0)?.operation).toBe('swing-left');
    expect(ps.find((p) => p.row === 1)).toMatchObject({ operation: 'fixed', infill: 'glass' });
    // Split along, a leaf stays a leaf on both sides (a pair of leaves).
    const pair = split(s, [900], { col: 0, row: 0, operation: 'swing-left' }, 'col', 450)!;
    expect(normalised(pair.spec).map((p) => p.operation)).toEqual(['swing-left', 'swing-left']);
  });
});
