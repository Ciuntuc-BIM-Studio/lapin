/**
 * The pane editor's rules (decision 370): a door's or window's grid as sizes in millimetres, a pane split in two (a column or
 * a row added where the cut falls, the panes across it spanning it), two panes merged into one, grid lines no pane needs any
 * more taken out. A type keeps its grid as relative sizes (columns, rows) and its panes as cells with spans; these work on that.
 */

export interface GridPanel { col: number; row: number; operation: string; colSpan?: number; rowSpan?: number; glazed?: boolean; infill?: string | null }
export interface GridSpec { columns: number[]; rows: number[]; panels: GridPanel[]; frame: { mullion: number; transom: number }; mullions?: (number | null)[]; transoms?: (number | null)[] }

/** One bar width per grid line (decision 466): null is the frame's, 0 none, else metres. A list as long as the lines, or none when every bar is the frame's. */
export type Bars = (number | null)[];

/** The bar list fitted to <paramref name="count"/> cells (count - 1 lines): longer cut, shorter padded with the frame's; undefined when every bar is the frame's. */
export function fitBars(bars: Bars | null | undefined, count: number): Bars | undefined {
  const out = Array.from({ length: Math.max(count - 1, 0) }, (_, i) => bars?.[i] ?? null);
  return out.some((b) => b != null) ? out : undefined;
}

/** Each line's width in mm: its own, or the frame's (metres) where it has none. */
export function barWidths(bars: Bars | null | undefined, count: number, frameBar: number): number[] {
  return Array.from({ length: Math.max(count - 1, 0) }, (_, i) => (bars?.[i] ?? frameBar) * 1000);
}

/** The bar list after a line was added at <paramref name="at"/> (the frame's bar) or removed. */
const withLine = (bars: Bars | undefined, count: number, at: number): Bars | undefined => {
  const b = Array.from({ length: count - 1 }, (_, i) => bars?.[i] ?? null);
  b.splice(at, 0, null);
  return fitBars(b, count + 1);
};
const withoutLine = (bars: Bars | undefined, count: number, at: number): Bars | undefined => {
  const b = Array.from({ length: count - 1 }, (_, i) => bars?.[i] ?? null);
  b.splice(at, 1);
  return fitBars(b, count - 1);
};

/** A bar's width set (metres; null: the frame's, 0: none), the list kept as long as the lines. */
export function withBar(bars: Bars | null | undefined, count: number, index: number, value: number | null): Bars | undefined {
  const b = Array.from({ length: Math.max(count - 1, 0) }, (_, i) => bars?.[i] ?? null);
  if (index < 0 || index >= b.length) return fitBars(bars, count);
  b[index] = value;
  return fitBars(b, count);
}

/**
 * The column boundaries where a door leaf hinged on the left meets one hinged on the right in the same row (what the engine calls a pair
 * once the mullion between them is 0): the left leaf ends at the boundary, the right one starts after it.
 */
export function leafPairs(s: GridSpec): number[] {
  const panels = normalised(s);
  const out = new Set<number>();
  for (const l of panels) {
    if (l.operation !== 'swing-left' && l.operation !== 'double-acting-left') continue;
    const end = l.col + span(l, 'col') - 1;
    const r = panels.find((x) => x.col === end + 1 && x.row === l.row && span(x, 'row') === span(l, 'row'));
    if (r && (r.operation === 'swing-right' || r.operation === 'double-acting-right')) out.add(end);
  }
  return [...out].sort((a, b) => a - b);
}

const DOOR_LEAVES = new Set(['swing-left', 'swing-right', 'double-acting-left', 'double-acting-right', 'door-sliding-left', 'door-sliding-right', 'folding-left', 'folding-right',
  'lift-slide-left', 'lift-slide-right', 'tilt-slide-left', 'tilt-slide-right']);

/** A window's opening sashes (forum topic 34: in a window, or in a door beside its leaves). */
export const WINDOW_SASHES = new Set(['side-left', 'side-right', 'tilt-turn-left', 'tilt-turn-right', 'bottom-hung', 'top-hung', 'pivot-horizontal', 'pivot-vertical', 'sliding-left', 'sliding-right', 'sliding-vertical']);

export const isDoorLeaf = (op: string) => DOOR_LEAVES.has(op);

/**
 * The Opens list in groups (forum topic 34): fixed first, then the door leaves, the window sashes and the garage doors' operations,
 * each as given (the order of the catalogue); empty groups left out.
 */
export function operationGroups(ops: readonly string[], garage: readonly string[] = []): { label: string | null; ops: string[] }[] {
  const g = new Set(garage);
  const groups: { label: string | null; ops: string[] }[] = [
    { label: null, ops: ops.filter((o) => o === 'fixed') },
    { label: 'Door leaves', ops: ops.filter((o) => DOOR_LEAVES.has(o)) },
    { label: 'Window sashes', ops: ops.filter((o) => WINDOW_SASHES.has(o)) },
    { label: 'Garage doors', ops: ops.filter((o) => g.has(o)) },
  ];
  const known = new Set(groups.flatMap((x) => x.ops));
  groups.push({ label: null, ops: ops.filter((o) => !known.has(o)) });
  return groups.filter((x) => x.ops.length > 0);
}

/** What a type is as the engine sees it (forum topic 34): a door with a door leaf, a window with opening sashes and no leaf, else its own kind. */
export function kindOf(s: GridSpec & { kind: 'door' | 'window' }): 'door' | 'window' {
  const ops = normalised(s).map((p) => p.operation);
  if (ops.some((o) => DOOR_LEAVES.has(o))) return 'door';
  return ops.some((o) => WINDOW_SASHES.has(o)) ? 'window' : s.kind;
}

/** The part over a door leaf split across: a fixed glazed light (a fanlight), as a leaf is never in two pieces. */
const upper = (p: GridPanel, axis: 'col' | 'row'): GridPanel => (axis === 'row' && DOOR_LEAVES.has(p.operation) ? { ...p, operation: 'fixed', infill: 'glass', glazed: false } : p);

const span = (p: GridPanel, axis: 'col' | 'row') => Math.max(1, (axis === 'col' ? p.colSpan : p.rowSpan) ?? 1);

/** Every cell covered by one panel (overlaps dropped, empty cells fixed glass), as the engine reads them. */
export function normalised(s: GridSpec): GridPanel[] {
  const nc = s.columns.length, nr = s.rows.length;
  const taken = Array.from({ length: nc }, () => Array<boolean>(nr).fill(false));
  const o: GridPanel[] = [];
  for (const p of s.panels) {
    if (p.col < 0 || p.row < 0 || p.col >= nc || p.row >= nr) continue;
    const cs = Math.min(span(p, 'col'), nc - p.col), rs = Math.min(span(p, 'row'), nr - p.row);
    let free = true;
    for (let c = p.col; c < p.col + cs; c++) for (let r = p.row; r < p.row + rs; r++) if (taken[c][r]) free = false;
    if (!free) continue;
    for (let c = p.col; c < p.col + cs; c++) for (let r = p.row; r < p.row + rs; r++) taken[c][r] = true;
    o.push({ ...p, colSpan: cs, rowSpan: rs });
  }
  for (let c = 0; c < nc; c++) for (let r = 0; r < nr; r++) if (!taken[c][r]) o.push({ col: c, row: r, operation: 'fixed', colSpan: 1, rowSpan: 1 });
  return o;
}

/** Sizes (mm) set one at a time: the one given takes its new size, the last (or, when it is the last, the one before) takes up the difference. Null when that leaves under 50 mm. */
export function resized(sizes: number[], index: number, mm: number): number[] | null {
  if (index < 0 || index >= sizes.length || !(mm > 0)) return null;
  if (sizes.length === 1) return null;
  const other = index === sizes.length - 1 ? index - 1 : sizes.length - 1;
  const next = [...sizes];
  next[other] = sizes[other] + sizes[index] - mm;
  next[index] = mm;
  return next[other] >= 50 ? next.map((v) => Math.round(v * 10) / 10) : null;
}

/** A grid line dragged: the sizes either side of boundary <paramref name="i"/> (between i and i+1) moved by <paramref name="by"/> mm; null under 50 mm. */
export function shifted(sizes: number[], i: number, by: number): number[] | null {
  if (i < 0 || i + 1 >= sizes.length) return null;
  const next = [...sizes];
  next[i] += by; next[i + 1] -= by;
  return next[i] >= 50 && next[i + 1] >= 50 ? next.map((v) => Math.round(v * 10) / 10) : null;
}

/**
 * A pane split in two at <paramref name="at"/> mm from its left (vertical cut, 'col') or bottom (horizontal, 'row') edge.
 * Where the cut falls on a grid line inside the pane, the pane is only divided there; else the column (row) it falls in
 * is cut in two, the bar between them taking its width from the left part, and every other pane across that column spans
 * both. Sizes are the columns' (rows') in mm; the bar is the mullion's (transom's). Null when either part would be under 50 mm.
 */
export function split(s: GridSpec, sizes: number[], pane: GridPanel, axis: 'col' | 'row', at: number): { spec: GridSpec; sizes: number[] } | null {
  const panels = normalised(s);
  const p = panels.find((x) => x.col === pane.col && x.row === pane.row);
  if (!p) return null;
  const bar = (axis === 'col' ? s.frame.mullion : s.frame.transom) * 1000;
  const lines = axis === 'col' ? s.mullions : s.transoms;
  const barAt = (i: number) => (lines?.[i] ?? bar / 1000) * 1000;
  const start = axis === 'col' ? p.col : p.row, n = span(p, axis);
  // The pane's extent along the axis, from its first cell's start: each cell and the bars between them.
  let pos = 0;
  for (let k = start; k < start + n; k++) {
    const a = pos, b = pos + sizes[k];
    if (k > start && Math.abs(at - (a - barAt(k - 1) / 2)) < 1) {
      // On a grid line already inside the pane: two panes, no new line.
      const left = { ...p, [axis === 'col' ? 'colSpan' : 'rowSpan']: k - start };
      const right = upper({ ...p, [axis]: k, [axis === 'col' ? 'colSpan' : 'rowSpan']: start + n - k }, axis);
      return { spec: { ...s, panels: [...panels.filter((x) => x !== p), left, right] }, sizes: [...sizes] };
    }
    if (at > a && at < b) {
      const first = at - a - bar / 2, second = b - at - bar / 2;
      if (first < 50 || second < 50) return null;
      const nextSizes = [...sizes.slice(0, k), first, second, ...sizes.slice(k + 1)];
      const key = axis === 'col' ? 'colSpan' : 'rowSpan';
      const moved = panels.map((x) => {
        if (x === p) return x;
        const xs = x[axis], xn = span(x, axis);
        if (xs > k) return { ...x, [axis]: xs + 1 };
        if (xs <= k && xs + xn - 1 >= k) return { ...x, [key]: xn + 1 };
        return x;
      });
      const left = { ...p, [key]: k - start + 1 };
      const right = upper({ ...p, [axis]: k + 1, [key]: start + n - k }, axis);
      const columns = axis === 'col' ? nextSizes : s.columns;
      const rows = axis === 'row' ? nextSizes : s.rows;
      // The new line takes the frame's bar; the lines after it move up one.
      const bars = axis === 'col' ? { mullions: withLine(s.mullions, s.columns.length, k) } : { transoms: withLine(s.transoms, s.rows.length, k) };
      return { spec: { ...s, ...bars, columns, rows, panels: [...moved.filter((x) => x !== p), left, right] }, sizes: nextSizes };
    }
    pos = b + barAt(k);
  }
  return null;
}

/** The pane beside another on one side ('left', 'right', 'below', 'above') sharing that whole edge with it; null when none does. */
export function neighbour(s: GridSpec, pane: GridPanel, side: 'left' | 'right' | 'below' | 'above'): GridPanel | null {
  const panels = normalised(s);
  const p = panels.find((x) => x.col === pane.col && x.row === pane.row);
  if (!p) return null;
  const cs = span(p, 'col'), rs = span(p, 'row');
  return panels.find((x) => {
    const xc = span(x, 'col'), xr = span(x, 'row');
    if (side === 'left') return x.col + xc === p.col && x.row === p.row && xr === rs;
    if (side === 'right') return x.col === p.col + cs && x.row === p.row && xr === rs;
    if (side === 'below') return x.row + xr === p.row && x.col === p.col && xc === cs;
    return x.row === p.row + rs && x.col === p.col && xc === cs;
  }) ?? null;
}

/** Two panes merged into one (the first's operation and infill), then the grid lines no pane needs taken out. */
export function merge(s: GridSpec, sizes: { cols: number[]; rows: number[] }, pane: GridPanel, side: 'left' | 'right' | 'below' | 'above'): { spec: GridSpec; sizes: { cols: number[]; rows: number[] } } | null {
  const other = neighbour(s, pane, side);
  if (!other) return null;
  const panels = normalised(s);
  const p = panels.find((x) => x.col === pane.col && x.row === pane.row)!;
  const col = Math.min(p.col, other.col), row = Math.min(p.row, other.row);
  const horizontal = side === 'left' || side === 'right';
  const merged: GridPanel = {
    ...p, col, row,
    colSpan: horizontal ? span(p, 'col') + span(other, 'col') : span(p, 'col'),
    rowSpan: horizontal ? span(p, 'row') : span(p, 'row') + span(other, 'row'),
  };
  const same = (a: GridPanel, b: GridPanel) => a.col === b.col && a.row === b.row;
  return tidy({ ...s, panels: [...panels.filter((x) => !same(x, p) && !same(x, other)), merged] }, sizes);
}

/** The grid lines every pane spans across taken out: the two sizes either side and the bar between become one. */
export function tidy(s: GridSpec, sizes: { cols: number[]; rows: number[] }): { spec: GridSpec; sizes: { cols: number[]; rows: number[] } } {
  let spec = s, cur = { cols: [...sizes.cols], rows: [...sizes.rows] };
  for (const axis of ['col', 'row'] as const) {
    const key = axis === 'col' ? 'colSpan' : 'rowSpan';
    const frameBar = axis === 'col' ? s.frame.mullion : s.frame.transom;
    for (let i = (axis === 'col' ? cur.cols : cur.rows).length - 2; i >= 0; i--) {
      // Boundary i, between i and i+1: needed when a pane starts at i+1 or ends at i.
      const needed = spec.panels.some((x) => x[axis] === i + 1 || x[axis] + span(x, axis) - 1 === i);
      if (needed) continue;
      const list = axis === 'col' ? cur.cols : cur.rows;
      const bar = ((axis === 'col' ? spec.mullions : spec.transoms)?.[i] ?? frameBar) * 1000;
      const joined = [...list.slice(0, i), list[i] + bar + list[i + 1], ...list.slice(i + 2)];
      cur = axis === 'col' ? { ...cur, cols: joined } : { ...cur, rows: joined };
      spec = {
        ...spec,
        columns: axis === 'col' ? joined : spec.columns,
        rows: axis === 'row' ? joined : spec.rows,
        ...(axis === 'col' ? { mullions: withoutLine(spec.mullions, list.length, i) } : { transoms: withoutLine(spec.transoms, list.length, i) }),
        panels: spec.panels.map((x) => {
          const xs = x[axis], xn = span(x, axis);
          if (xs > i) return { ...x, [axis]: xs - 1 };
          if (xs + xn - 1 > i) return { ...x, [key]: xn - 1 };
          return x;
        }),
      };
    }
  }
  return { spec, sizes: cur };
}
