/**
 * Title blocks of the user's own (docs/plan-title-blocks.md, decision 340): the template as the engine keeps it, and the pure
 * parts of the designer and the library — live text filled from the fields, the items' key points, bounds, moves and hits,
 * snapping to the grid and to the other items, and the templates kept on this computer.
 */

/** An item of a template, in mm from the block's bottom-left corner, y up. */
export type TbItem =
  | { type: 'line'; a: [number, number]; b: [number, number]; w?: number; color?: string; dash?: number[] }
  | { type: 'rect'; x: number; y: number; w: number; h: number; stroke?: number; color?: string; fill?: string | null; hatch?: { pattern: string; scale?: number; angle?: number; color?: string } | null }
  | { type: 'polyline'; pts: [number, number][]; closed?: boolean; stroke?: number; color?: string; fill?: string | null; hatch?: { pattern: string; scale?: number; angle?: number; color?: string } | null }
  | { type: 'text'; at: [number, number]; text: string; h?: number; style?: string; bold?: boolean; italic?: boolean; align?: 'start' | 'middle' | 'end'; valign?: 'bottom' | 'middle'; angle?: number; color?: string; fit?: number; tag?: string; font?: string }
  | { type: 'image'; x: number; y: number; w: number; h: number; imageId?: string; data?: string; mime?: string; name?: string }
  | { type: 'revisions'; x: number; y: number; w: number; h: number; rowH?: number; stroke?: number; labels?: Record<string, string> };

export type TbType = TbItem['type'];

export interface TbFrame { show: boolean; left: number; top: number; right: number; bottom: number }

export interface TbTemplate {
  w: number; h: number;
  anchor: 'bottomRight' | 'bottomLeft' | 'topRight' | 'topLeft';
  frame: TbFrame;
  revisions: 'above' | 'none';
  items: TbItem[];
}

/** A title block as listed: Lapin's own, the project's or this computer's. */
export interface TbSummary { id: string; name: string; builtin: boolean; default?: boolean; uses?: number; description?: string; origin: 'builtin' | 'project' | 'library' }

/** A field to insert, with its value on the sample sheet. */
export interface TbField { key: string; label: string; group: 'project' | 'sheet' | 'system' | string; value?: string | null }

/** A title block's file (Export / Import): its name and template, its images inside. */
export interface TbFile { format?: string; version?: number; name: string; template: TbTemplate }

export const ANCHORS: TbTemplate['anchor'][] = ['bottomRight', 'bottomLeft', 'topRight', 'topLeft'];

export const DEFAULT_FRAME: TbFrame = { show: true, left: 10, top: 10, right: 10, bottom: 10 };

/** A template as the engine sent it, its missing parts filled in. */
export function normalise(t: Partial<TbTemplate> | null | undefined): TbTemplate {
  return {
    w: t?.w ?? 180, h: t?.h ?? 50, anchor: t?.anchor ?? 'bottomRight',
    frame: { ...DEFAULT_FRAME, ...(t?.frame ?? {}) }, revisions: t?.revisions === 'none' ? 'none' : 'above',
    items: Array.isArray(t?.items) ? t!.items.map((i) => ({ ...i })) as TbItem[] : [],
  };
}

// ------------------------------------------------------------ live text

const TOKEN = /\{\{?\s*([A-Za-z]\w*\.[A-Za-z0-9_.-]+)\s*\}\}?/g;

/** The tokens a text holds, in order. */
export const tokensOf = (text: string): string[] => [...text.matchAll(TOKEN)].map((m) => m[1]);

/** A run of a text as the designer shows it: plain, a value filled in, a known field with no value (its token), an unknown one. */
export interface TextRun { text: string; kind: 'plain' | 'value' | 'empty' | 'unknown'; key?: string }

/** A text cut into runs by its tokens, each filled from `values` (a key there with no value is empty; a key not there unknown). */
export function textRuns(text: string, values: Map<string, string | null | undefined>): TextRun[] {
  const runs: TextRun[] = [];
  let at = 0;
  for (const m of text.matchAll(TOKEN)) {
    if (m.index! > at) runs.push({ text: text.slice(at, m.index), kind: 'plain' });
    const key = m[1];
    if (!values.has(key)) runs.push({ text: `<${key}>`, kind: 'unknown', key });
    else {
      const v = values.get(key);
      runs.push(v ? { text: v, kind: 'value', key } : { text: `{${key}}`, kind: 'empty', key });
    }
    at = m.index! + m[0].length;
  }
  if (at < text.length) runs.push({ text: text.slice(at), kind: 'plain' });
  return runs;
}

/** A text as the sheet prints it: a value, nothing for an empty field, `<key>` for an unknown one. */
export const fillText = (text: string, values: Map<string, string | null | undefined>) =>
  textRuns(text, values).map((r) => (r.kind === 'empty' ? '' : r.text)).join('');

/** A token put into a text: at the end, a space before it unless the text ends in one (or is empty). */
export function insertToken(text: string, key: string): string {
  const token = `{${key}}`;
  return text.length === 0 || /\s$/.test(text) ? text + token : `${text} ${token}`;
}

// ------------------------------------------------------------ geometry

export type Pt = [number, number];

/** The points of an item a click snaps to: ends, corners, middles, a text's anchor. */
export function keyPoints(item: TbItem): Pt[] {
  switch (item.type) {
    case 'line': return [item.a, item.b, [(item.a[0] + item.b[0]) / 2, (item.a[1] + item.b[1]) / 2]];
    case 'polyline': {
      const o: Pt[] = [...item.pts];
      const n = item.pts.length;
      for (let i = 0; i + 1 < n + (item.closed ? 1 : 0); i++) {
        const [p, q] = [item.pts[i], item.pts[(i + 1) % n]];
        o.push([(p[0] + q[0]) / 2, (p[1] + q[1]) / 2]);
      }
      return o;
    }
    case 'text': return [item.at];
    default: {
      const { x, y, w, h } = item;
      return [[x, y], [x + w, y], [x + w, y + h], [x, y + h], [x + w / 2, y], [x + w, y + h / 2], [x + w / 2, y + h], [x, y + h / 2]];
    }
  }
}

/** An item's box (x0, y0, x1, y1); a text's guessed from its length and height. */
export function bounds(item: TbItem): [number, number, number, number] {
  switch (item.type) {
    case 'line': return [Math.min(item.a[0], item.b[0]), Math.min(item.a[1], item.b[1]), Math.max(item.a[0], item.b[0]), Math.max(item.a[1], item.b[1])];
    case 'polyline': {
      const xs = item.pts.map((p) => p[0]), ys = item.pts.map((p) => p[1]);
      return [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)];
    }
    case 'text': {
      const h = item.h ?? 2.5;
      const w = Math.max(1, item.text.length) * h * 0.55;
      const x0 = item.align === 'middle' ? item.at[0] - w / 2 : item.align === 'end' ? item.at[0] - w : item.at[0];
      const y0 = item.valign === 'middle' ? item.at[1] - h / 2 : item.at[1];
      return [x0, y0, x0 + w, y0 + h];
    }
    default: return [item.x, item.y, item.x + item.w, item.y + item.h];
  }
}

/** An item moved by dx, dy (mm), a new object. */
export function moveItem(item: TbItem, dx: number, dy: number): TbItem {
  const m = (p: Pt): Pt => [round(p[0] + dx), round(p[1] + dy)];
  switch (item.type) {
    case 'line': return { ...item, a: m(item.a), b: m(item.b) };
    case 'polyline': return { ...item, pts: item.pts.map(m) };
    case 'text': return { ...item, at: m(item.at) };
    default: return { ...item, x: round(item.x + dx), y: round(item.y + dy) };
  }
}

/** The handles of an item that reshape it: a line's ends, a polyline's vertices, a box's corners. */
export function handles(item: TbItem): Pt[] {
  switch (item.type) {
    case 'line': return [item.a, item.b];
    case 'polyline': return item.pts;
    case 'text': return [];
    default: return [[item.x, item.y], [item.x + item.w, item.y], [item.x + item.w, item.y + item.h], [item.x, item.y + item.h]];
  }
}

/** An item with its handle `i` dragged to `p`; a box keeps its opposite corner. */
export function dragHandle(item: TbItem, i: number, p: Pt): TbItem {
  const q: Pt = [round(p[0]), round(p[1])];
  switch (item.type) {
    case 'line': return i === 0 ? { ...item, a: q } : { ...item, b: q };
    case 'polyline': return { ...item, pts: item.pts.map((v, k) => (k === i ? q : v)) };
    case 'text': return item;
    default: {
      const corners = handles(item);
      const opposite = corners[(i + 2) % 4];
      const [x0, x1] = [Math.min(q[0], opposite[0]), Math.max(q[0], opposite[0])];
      const [y0, y1] = [Math.min(q[1], opposite[1]), Math.max(q[1], opposite[1])];
      return { ...item, x: x0, y: y0, w: round(Math.max(0.5, x1 - x0)), h: round(Math.max(0.5, y1 - y0)) };
    }
  }
}

const distToSegment = (p: Pt, a: Pt, b: Pt) => {
  const [dx, dy] = [b[0] - a[0], b[1] - a[1]];
  const l2 = dx * dx + dy * dy;
  const t = l2 === 0 ? 0 : Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2));
  return Math.hypot(p[0] - (a[0] + t * dx), p[1] - (a[1] + t * dy));
};

/** The topmost item under a point (mm), within `tol` of a line or inside a filled box or text; -1 for none. */
export function itemAt(items: TbItem[], p: Pt, tol: number): number {
  for (let i = items.length - 1; i >= 0; i--) {
    const it = items[i];
    if (it.type === 'line' && distToSegment(p, it.a, it.b) <= tol) return i;
    if (it.type === 'polyline') {
      const n = it.pts.length;
      for (let k = 0; k + 1 < n + (it.closed ? 1 : 0); k++) if (distToSegment(p, it.pts[k], it.pts[(k + 1) % n]) <= tol) return i;
      continue;
    }
    if (it.type === 'line') continue;
    const [x0, y0, x1, y1] = bounds(it);
    const inside = p[0] >= x0 - tol && p[0] <= x1 + tol && p[1] >= y0 - tol && p[1] <= y1 + tol;
    if (!inside) continue;
    // A box hit inside only when it is filled (or a text, an image, a table); else near its edges.
    if (it.type === 'rect' && !it.fill && !it.hatch) {
      const near = Math.min(Math.abs(p[0] - x0), Math.abs(p[0] - x1), Math.abs(p[1] - y0), Math.abs(p[1] - y1)) <= tol;
      if (!near) continue;
    }
    return i;
  }
  return -1;
}

/**
 * A point snapped: to the nearest key point of the items (but `except`) within `tol`, else to the grid (0: none); with
 * `from` and `ortho`, kept on the horizontal or vertical through it.
 */
export function snap(p: Pt, items: TbItem[], opts: { grid: number; tol: number; objects?: boolean; except?: number; from?: Pt | null; ortho?: boolean }): { p: Pt; on: 'point' | 'grid' | 'none' } {
  let q: Pt = p;
  if (opts.ortho && opts.from) q = Math.abs(p[0] - opts.from[0]) >= Math.abs(p[1] - opts.from[1]) ? [p[0], opts.from[1]] : [opts.from[0], p[1]];
  if (opts.objects !== false) {
    let best: Pt | null = null, d = opts.tol;
    items.forEach((it, i) => {
      if (i === opts.except) return;
      for (const k of keyPoints(it)) {
        const dk = Math.hypot(k[0] - q[0], k[1] - q[1]);
        if (dk <= d) { d = dk; best = k; }
      }
    });
    if (best) {
      const b = best as Pt;
      if (opts.ortho && opts.from) return { p: Math.abs(p[0] - opts.from[0]) >= Math.abs(p[1] - opts.from[1]) ? [b[0], opts.from[1]] : [opts.from[0], b[1]], on: 'point' };
      return { p: [b[0], b[1]], on: 'point' };
    }
  }
  if (opts.grid > 0) {
    const g = opts.grid;
    const r: Pt = [round(Math.round(q[0] / g) * g), round(Math.round(q[1] / g) * g)];
    if (opts.ortho && opts.from) return { p: Math.abs(p[0] - opts.from[0]) >= Math.abs(p[1] - opts.from[1]) ? [r[0], opts.from[1]] : [opts.from[0], r[1]], on: 'grid' };
    return { p: r, on: 'grid' };
  }
  return { p: q, on: 'none' };
}

/** A number to a hundredth of a millimetre. */
export const round = (v: number) => Math.round(v * 100) / 100;

/** A new item of a kind at a point, with Lapin's defaults (a text of the token given, or "Text"). */
export function newItem(type: TbType, p: Pt, text?: string): TbItem {
  switch (type) {
    case 'line': return { type, a: p, b: [p[0] + 20, p[1]], w: 0.25 };
    case 'rect': return { type, x: p[0], y: p[1], w: 30, h: 10, stroke: 0.25 };
    case 'polyline': return { type, pts: [p, [p[0] + 20, p[1]]], stroke: 0.25 };
    case 'text': return { type, at: p, text: text ?? 'Text', h: 2.5, align: 'start' };
    case 'image': return { type, x: p[0], y: p[1], w: 30, h: 15 };
    case 'revisions': return { type, x: p[0], y: p[1], w: 120, h: 20, rowH: 5 };
  }
}

// ------------------------------------------------------------ this computer's library

const LIBRARY = 'lapin.titleBlocks';

export interface LibraryBlock extends TbFile { id: string; added?: string }

/** Whether a value is a title block file's content (a name and a template with items). */
export const isTitleBlockFile = (v: unknown): v is TbFile =>
  !!v && typeof v === 'object' && typeof (v as TbFile).name === 'string' && !!(v as TbFile).template && Array.isArray((v as TbFile).template.items);

/** The title blocks kept on this computer. */
export function libraryBlocks(): LibraryBlock[] {
  try {
    const all = JSON.parse(localStorage.getItem(LIBRARY) ?? '[]') as unknown;
    return Array.isArray(all) ? all.filter((x): x is LibraryBlock => isTitleBlockFile(x) && typeof (x as LibraryBlock).id === 'string') : [];
  } catch { return []; }
}

function store(all: LibraryBlock[]): boolean {
  try { localStorage.setItem(LIBRARY, JSON.stringify(all)); return true; } catch { return false; }
}

/** A title block kept in the library under a new id; false when the storage refuses it. */
export function keepBlock(file: TbFile): LibraryBlock | null {
  const all = libraryBlocks();
  const ids = new Set(all.map((b) => b.id));
  let id = 'tb-' + Date.now().toString(36);
  for (let n = 2; ids.has(id); n++) id = `tb-${Date.now().toString(36)}-${n}`;
  const kept: LibraryBlock = { id, format: 'lapin.titleBlock', version: 1, name: file.name, template: file.template, added: new Date().toISOString().slice(0, 10) };
  return store([...all, kept]) ? kept : null;
}

export function forgetBlock(id: string): void { store(libraryBlocks().filter((b) => b.id !== id)); }

export function renameBlock(id: string, name: string): boolean {
  const all = libraryBlocks();
  const at = all.findIndex((b) => b.id === id);
  if (at < 0) return false;
  all[at] = { ...all[at], name };
  return store(all);
}

/** A name not among `taken`: the one asked, else with " 2", " 3"…. */
export function freeName(name: string, taken: Iterable<string>): string {
  const used = new Set([...taken].map((n) => n.toLowerCase()));
  if (!used.has(name.toLowerCase())) return name;
  for (let n = 2; ; n++) if (!used.has(`${name} ${n}`.toLowerCase())) return `${name} ${n}`;
}
