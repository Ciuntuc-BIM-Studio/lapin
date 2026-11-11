import { describe, expect, it } from 'vitest';
import { barWidths, fitBars, leafPairs, merge, neighbour, normalised, resized, shifted, split, tidy, withBar, type GridSpec } from './joineryGrid';

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

describe('per-bar widths (decision 450)', () => {
  const P = (col: number, operation: string, extra = {}) => ({ col, row: 0, operation, ...extra });

  it('fits the list to the lines and drops it when every bar is the frame\'s', () => {
    expect(fitBars([0, null, 0.1], 3)).toEqual([0, null]);
    expect(fitBars([0], 4)).toEqual([0, null, null]);
    expect(fitBars([null, null], 3)).toBeUndefined();
    expect(fitBars(undefined, 3)).toBeUndefined();
    expect(withBar(undefined, 3, 1, 0)).toEqual([null, 0]);
    expect(withBar([null, 0], 3, 1, null)).toBeUndefined();
    expect(withBar([0], 2, 5, 0.1)).toEqual([0]);
    expect(barWidths([0, null], 3, 0.05)).toEqual([0, 50]);
  });

  it('keeps each bar with its line when a column is added: the new line takes the frame\'s', () => {
    const s: GridSpec = { columns: [500, 500], rows: [1000], frame, mullions: [0.1], panels: [P(0, 'swing-left'), P(1, 'swing-right')] };
    // Cut the first column: the old line (index 0) is now index 1.
    const r = split(s, [500, 500], P(0, 'swing-left'), 'col', 200)!;
    expect(r.spec.mullions).toEqual([null, 0.1]);
    // Cut the second: the old line stays first.
    const r2 = split(s, [500, 500], P(1, 'swing-right'), 'col', 300)!;
    expect(r2.spec.mullions).toEqual([0.1, null]);
    // A split on a line already inside the pane adds none.
    const t: GridSpec = { columns: [500, 500], rows: [1000], frame, mullions: [0.1], panels: [P(0, 'fixed', { colSpan: 2 })] };
    expect(split(t, [500, 500], P(0, 'fixed'), 'col', 500 + 50)!.spec.mullions).toEqual([0.1]);
  });

  it('takes a bar out with its line, joining the two sizes over the bar\'s own width', () => {
    const s: GridSpec = { columns: [300, 300, 300], rows: [1000], frame, mullions: [0, 0.1], panels: [P(0, 'fixed', { colSpan: 2 }), P(2, 'fixed')] };
    const r = tidy(s, { cols: [300, 300, 300], rows: [1000] });
    // Line 0 (no bar) goes: 300 + 0 + 300; line 1 stays with its 0.1.
    expect(r.sizes.cols).toEqual([600, 300]);
    expect(r.spec.mullions).toEqual([0.1]);
    const t: GridSpec = { ...s, mullions: [0.1, 0] };
    expect(tidy(t, { cols: [300, 300, 300], rows: [1000] }).sizes.cols).toEqual([700, 300]);
  });

  it('keeps the transoms in step the same way', () => {
    const s: GridSpec = { columns: [1000], rows: [500, 500], frame, transoms: [0.08], panels: [P(0, 'fixed')] };
    const r = split(s, [1000], P(0, 'fixed'), 'row', 200)!;
    expect(r.spec.transoms).toEqual([null, 0.08]);
  });

  it('finds where two leaves can meet', () => {
    const s: GridSpec = { columns: [1, 1], rows: [1], frame, panels: [P(0, 'swing-left'), P(1, 'swing-right')] };
    expect(leafPairs(s)).toEqual([0]);
    expect(leafPairs({ ...s, panels: [P(0, 'swing-right'), P(1, 'swing-left')] })).toEqual([]);
    expect(leafPairs({ ...s, columns: [1, 2, 1], panels: [P(0, 'fixed'), P(1, 'swing-left'), P(2, 'swing-right')] })).toEqual([1]);
  });
});
