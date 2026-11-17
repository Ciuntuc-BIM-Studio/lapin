/**
 * A drafting table's logic in the interface (issue #71, docs/plan-drafting-tables.md): cell addresses as a spreadsheet's (A1,
 * B3, AA1), the range a selection in the editor makes, and the ops of `drafting.editTable` the editor's buttons send, each
 * call one undo step. The engine keeps the table, works out its formulas and draws it; this only builds what is asked.
 */

/** A column's letters: 0 → A, 25 → Z, 26 → AA. */
export function colName(c: number): string {
  let s = '';
  for (let n = c + 1; n > 0; n = Math.floor((n - 1) / 26)) s = String.fromCharCode(65 + ((n - 1) % 26)) + s;
  return s;
}

export const address = (r: number, c: number): string => `${colName(c)}${r + 1}`;

/** A cell's row and column (from 0) by its address (`B3` → 2, 1), case aside; null when it is not one. */
export function parseAddress(a: string): { r: number; c: number } | null {
  const m = /^([A-Za-z]{1,3})([0-9]{1,5})$/.exec(a.trim());
  if (!m) return null;
  let c = 0;
  for (const ch of m[1].toUpperCase()) c = c * 26 + (ch.charCodeAt(0) - 64);
  const r = Number(m[2]);
  return r < 1 ? null : { r: r - 1, c: c - 1 };
}

/** A selection: its anchor and the cell it reaches to (shift-click), any way round. */
export interface Sel { r0: number; c0: number; r1: number; c1: number }

export const norm = (s: Sel): Sel => ({ r0: Math.min(s.r0, s.r1), c0: Math.min(s.c0, s.c1), r1: Math.max(s.r0, s.r1), c1: Math.max(s.c0, s.c1) });

/** The range a selection names (`A1:C3`, or `B2` for one cell). */
export function rangeOf(s: Sel): string {
  const n = norm(s);
  const a = address(n.r0, n.c0), b = address(n.r1, n.c1);
  return a === b ? a : `${a}:${b}`;
}

export const inSel = (s: Sel | null, r: number, c: number): boolean => {
  if (!s) return false;
  const n = norm(s);
  return r >= n.r0 && r <= n.r1 && c >= n.c0 && c <= n.c1;
};

/** A cell as the engine reads it back (`drafting.table`). */
export interface TableCellInfo { t: string; shown: string; covered?: string; rs?: number; cs?: number }
export interface TableInfo { rows: number; cols: number; cells: TableCellInfo[][]; colW: number[]; rowH: number[]; errors: string[] }

/** A selection grown to take in whole every merged cell it touches (as a spreadsheet's does). */
export function grow(s: Sel, info: TableInfo): Sel {
  let n = norm(s);
  for (let changed = true; changed;) {
    changed = false;
    for (let r = n.r0; r <= n.r1; r++)
      for (let c = n.c0; c <= n.c1; c++) {
        const cell = info.cells[r]?.[c];
        const m = cell?.covered ? parseAddress(cell.covered) : { r, c };
        if (!m) continue;
        const master = info.cells[m.r]?.[m.c];
        const r1 = m.r + (master?.rs ?? 1) - 1, c1 = m.c + (master?.cs ?? 1) - 1;
        const g = { r0: Math.min(n.r0, m.r), c0: Math.min(n.c0, m.c), r1: Math.max(n.r1, r1), c1: Math.max(n.c1, c1) };
        if (g.r0 !== n.r0 || g.c0 !== n.c0 || g.r1 !== n.r1 || g.c1 !== n.c1) { n = g; changed = true; }
      }
  }
  return n;
}

/** The presets a new table takes: every line, a header row, lines under the header only, none. */
export const PRESETS = [
  { id: 'header', label: 'Header row' },
  { id: 'grid', label: 'Grid lines' },
  { id: 'minimal', label: 'Minimal' },
  { id: 'none', label: 'No lines' },
] as const;
export type Preset = (typeof PRESETS)[number]['id'];

export const BORDERS = [
  { id: 'all', label: 'All lines' },
  { id: 'outer', label: 'Outside' },
  { id: 'inner', label: 'Inside' },
  { id: 'top', label: 'Top' },
  { id: 'bottom', label: 'Bottom' },
  { id: 'left', label: 'Left' },
  { id: 'right', label: 'Right' },
  { id: 'none', label: 'No lines' },
  { id: 'default', label: "The table's" },
] as const;

export type TableOp = Record<string, unknown> & { op: string };

/** A colour with an opacity (0–1) as the engine keeps it: `#rrggbb` at full, `#rrggbbaa` below. */
export function withOpacity(hex: string, opacity: number): string {
  const m = /^#?([0-9a-f]{6})/i.exec(hex.trim());
  if (!m) return hex;
  const a = Math.round(Math.min(1, Math.max(0, opacity)) * 255);
  return a >= 255 ? `#${m[1].toLowerCase()}` : `#${m[1].toLowerCase()}${a.toString(16).padStart(2, '0')}`;
}

/** A colour's opacity 0–1 (`#rrggbbaa`), 1 without one. */
export function opacityOf(hex: string | null | undefined): number {
  const m = /^#?[0-9a-f]{6}([0-9a-f]{2})$/i.exec((hex ?? '').trim());
  return m ? parseInt(m[1], 16) / 255 : 1;
}

/** The ops the editor's row and column buttons send: before or after the selection, or the selection's rows (columns) taken out. */
export function rowsOp(kind: 'above' | 'below' | 'delete', s: Sel): TableOp {
  const n = norm(s);
  if (kind === 'delete') return { op: 'deleteRows', at: n.r0, count: n.r1 - n.r0 + 1 };
  return { op: 'insertRows', at: kind === 'above' ? n.r0 : n.r1 + 1, count: 1 };
}

export function colsOp(kind: 'left' | 'right' | 'delete', s: Sel): TableOp {
  const n = norm(s);
  if (kind === 'delete') return { op: 'deleteCols', at: n.c0, count: n.c1 - n.c0 + 1 };
  return { op: 'insertCols', at: kind === 'left' ? n.c0 : n.c1 + 1, count: 1 };
}

/** Text pasted from a spreadsheet (tab-separated, a line per row) as the rows of texts `set` takes. */
export function pasteBlock(text: string): string[][] {
  const lines = text.replace(/\r\n/g, '\n').replace(/\n$/, '').split('\n');
  return lines.map((l) => l.split('\t'));
}

/** The table's attributes on the node, as stored (its JSON's own shape, docs/plan-drafting-tables.md §2). */
export interface TableJson {
  cols?: { w?: number; fit?: boolean; min?: number; max?: number }[];
  rows?: { h?: number; fit?: boolean; header?: boolean; cells?: (Record<string, unknown> | null)[] }[];
  style?: Record<string, unknown>;
  preset?: string;
}

/** A cell's own style value (`bold`, `fill`…) as stored, its master's for a covered one; undefined when it has none. */
export function ownStyle(table: TableJson | undefined, r: number, c: number, key: string): unknown {
  return table?.rows?.[r]?.cells?.[c]?.[key] ?? undefined;
}

/** Whether a table's name can be read by a formula (`table.<name>.B4`): one word of letters, digits and _, not starting with a digit. */
export const isTableName = (name: string): boolean => /^[A-Za-z_][A-Za-z0-9_]{0,63}$/.test(name.trim());

/** A reference to another table's cell or range, as a formula writes it: `table.Areas.B4`, `table.Areas.B2:B9`. */
export function tableRef(name: string, s: Sel): string {
  return `table.${name}.${rangeOf(s)}`;
}

/** A text with `insert` put in at the caret (or in place of the selection `from`–`to`), and where the caret goes after it. */
export function insertAt(text: string, from: number, to: number, insert: string): { text: string; caret: number } {
  const a = Math.max(0, Math.min(from, text.length)), b = Math.max(a, Math.min(to, text.length));
  return { text: text.slice(0, a) + insert + text.slice(b), caret: a + insert.length };
}

/**
 * What the picker puts in: inside a formula (the caret between `{=` and its `}`) the reference alone; elsewhere a token that
 * shows the cell (`{table.Areas.B4}`), or the range's total as a formula (`{= sum(table.Areas.B2:B9)}`).
 */
export function refInsert(text: string, caret: number, ref: string): string {
  const before = text.slice(0, caret);
  const open = before.lastIndexOf('{=');
  const inFormula = open >= 0 && before.indexOf('}', open) < 0;
  if (inFormula) return ref;
  return ref.includes(':') ? `{= sum(${ref})}` : `{${ref}}`;
}
