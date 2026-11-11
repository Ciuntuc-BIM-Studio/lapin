/**
 * Lapin for the browser (decision 482, docs/html-export.md): what the page the engine writes carries, and the pure
 * parts of its viewer — reading the blocks, the phrases, numbers as the page shows them, the tables sorted, the storeys
 * filtered. No DOM beyond what is passed in, no three.js: tested on its own (data.test.ts).
 */

/** An element as the properties panel shows it (HtmlExport.Properties). */
export interface PageElement {
  name: string;
  class: string;
  type?: string;
  storey?: string;
  material?: string;
  sets: { name: string; props: [string, string][] }[];
  quantities: { name: string; props: [string, string][] }[];
}

export interface PageDrawing { id: string; name: string; kind: 'plan' | 'section' | 'elevation' | 'sheet' | string; number?: string; w: number; h: number }
export interface PageColumn { key: string; label: string; kind: 'text' | 'number' | 'money' | string }
export interface PageRow { cells: (string | number | null)[]; elements: string[]; heading?: boolean }
export interface PageTotal { label: string; value: number; percent?: number | null }
export interface PageTable { key: string; title: string; name?: string | null; note?: string | null; indicative: boolean; currency?: string | null; columns: PageColumn[]; rows: PageRow[]; totals: PageTotal[] }
export interface PageStorey { id: string; name: string; elevation: number }

/** The data block (lapin-data), as HtmlExport writes it. */
export interface PageData {
  format: 'lapin-html';
  version: number;
  project: string;
  edition: string;
  app: string;
  date: string;
  lang: string;
  strings: Record<string, string>;
  theme?: { name?: string } | null;
  hasProject: boolean;
  model3d: boolean;
  storeys: PageStorey[];
  drawings: PageDrawing[];
  tables: PageTable[];
  elements: Record<string, PageElement>;
}

/** The data block of a page; null when the page holds none (or another format). */
export function readData(doc: Document): PageData | null {
  const block = doc.getElementById('lapin-data');
  if (!block?.textContent) return null;
  try {
    const d = JSON.parse(block.textContent) as PageData;
    return d && d.format === 'lapin-html' ? d : null;
  } catch {
    return null;
  }
}

/** A script block's text as it was given: the engine wrote `<\/script` and `<\!--` so nothing in it closed the block (HtmlExport.Script). */
export const unscript = (s: string) => s.replace(/<\\\/(script)/gi, '</$1').replace(/<\\!--/g, '<!--');

/** Base64 to bytes, without the network (a page from a file can fetch nothing, not even a data: URL in some browsers). */
export function fromBase64(b64: string): Uint8Array {
  const bin = atob(b64.replace(/\s+/g, ''));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

/** A phrase in the exporting user's language: the page carries the interface's own translations; English when it has none. */
export const phrase = (strings: Record<string, string> | undefined, en: string): string => strings?.[en] || en;

/** A number as the page shows it, in the page's language: money with two decimals, quantities with up to three. */
export function formatNumber(v: number | string | null | undefined, kind: string, lang: string): string {
  if (v == null || v === '') return '';
  if (typeof v === 'string') return v;
  if (!Number.isFinite(v)) return '';
  const digits = kind === 'money' ? { minimumFractionDigits: 2, maximumFractionDigits: 2 } : { maximumFractionDigits: 3 };
  try {
    return new Intl.NumberFormat(lang || 'en', digits).format(v);
  } catch {
    return new Intl.NumberFormat('en', digits).format(v);
  }
}

/** The table's rows as shown: as written (chapters heading their lines), or its lines alone sorted by a column (headings dropped: a line leaves its chapter). */
export function sortRows(rows: PageRow[], column: number | null, descending: boolean): PageRow[] {
  if (column == null) return rows;
  const lines = rows.filter((r) => !r.heading);
  const value = (r: PageRow) => r.cells[column];
  const sorted = [...lines].sort((a, b) => {
    const x = value(a);
    const y = value(b);
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    const c = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), undefined, { numeric: true, sensitivity: 'base' });
    return descending ? -c : c;
  });
  return sorted;
}

/** The rows with a word in them (any cell, case aside); all of them for an empty filter. Headings stay when one of their lines does. */
export function filterRows(rows: PageRow[], q: string): PageRow[] {
  const t = q.trim().toLowerCase();
  if (!t) return rows;
  const hit = (r: PageRow) => r.cells.some((c) => c != null && String(c).toLowerCase().includes(t));
  const out: PageRow[] = [];
  let heading: PageRow | null = null;
  for (const r of rows) {
    if (r.heading) { heading = r; if (hit(r)) { out.push(r); heading = null; } continue; }
    if (!hit(r)) continue;
    if (heading) { out.push(heading); heading = null; }
    out.push(r);
  }
  return out;
}

/**
 * Which storeys the 3D view shows for the storey chosen: all (none chosen), that one alone, or it and those below it
 * (the model cut above the storey, as a plan looks down on it).
 */
export function storeysShown(storeys: PageStorey[], chosen: string | null, below: boolean): Set<string> {
  if (!chosen) return new Set(storeys.map((s) => s.id));
  const at = storeys.find((s) => s.id === chosen);
  if (!at) return new Set(storeys.map((s) => s.id));
  return new Set(storeys.filter((s) => s.id === chosen || (below && s.elevation < at.elevation)).map((s) => s.id));
}

/** The drawings grouped as the project browser groups them: plans, sections, elevations, sheets (each kept in its order). */
export function groupDrawings(drawings: PageDrawing[]): { kind: string; label: string; items: PageDrawing[] }[] {
  const kinds: [string, string][] = [['plan', 'Floor plans'], ['section', 'Sections'], ['elevation', 'Elevations'], ['sheet', 'Sheets']];
  const groups = kinds.map(([kind, label]) => ({ kind, label, items: drawings.filter((d) => d.kind === kind) }));
  const other = drawings.filter((d) => !kinds.some(([k]) => k === d.kind));
  if (other.length) groups.push({ kind: 'other', label: 'Views', items: other });
  return groups.filter((g) => g.items.length > 0);
}

/** A drawing's title in the list: a sheet with its number before its name. */
export const drawingTitle = (d: PageDrawing) => (d.kind === 'sheet' && d.number ? `${d.number} · ${d.name}` : d.name);

/** A zoom by the wheel about a point: the view's scale and offset after it, the point under the cursor staying where it is. */
export function zoomAbout(view: { scale: number; x: number; y: number }, at: { x: number; y: number }, factor: number, min = 0.05, max = 80): { scale: number; x: number; y: number } {
  const scale = Math.min(max, Math.max(min, view.scale * factor));
  const k = scale / view.scale;
  return { scale, x: at.x - (at.x - view.x) * k, y: at.y - (at.y - view.y) * k };
}

/** The scale and offset that fit a drawing of w × h into a box, with a margin. */
export function fitBox(w: number, h: number, boxW: number, boxH: number, margin = 24): { scale: number; x: number; y: number } {
  const scale = Math.max(0.01, Math.min((boxW - 2 * margin) / Math.max(w, 1e-6), (boxH - 2 * margin) / Math.max(h, 1e-6)));
  return { scale, x: (boxW - w * scale) / 2, y: (boxH - h * scale) / 2 };
}
