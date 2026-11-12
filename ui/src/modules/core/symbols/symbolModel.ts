/**
 * Symbols of the user's own (docs/plan-user-extensibility.md §3): the definition as the engine keeps it, and the pure parts of the
 * editor, the place tool and the library — the items' key points, bounds, moves, handles and hits (the title block designer's items
 * plus circles and arcs), snapping, a preview's pens moved and turned into a ghost for a view, and the symbols kept on this computer.
 */

import type { PenDto } from '../../../viewport/drawing';
import type { Vec2 } from '../../../platform/module';
import type { Shape } from '../../../viewport/cad/shapes';
import { bounds as tbBounds, dragHandle as tbDragHandle, handles as tbHandles, itemAt as tbItemAt, keyPoints as tbKeyPoints, moveItem as tbMoveItem, round, type Pt, type TbItem } from '../titleBlocks/titleBlockModel';

export { round };
export type { Pt };

type Hatch = { pattern: string; scale?: number; angle?: number; color?: string } | null;

/** An item of a symbol, in mm from its insertion point, +x its direction, y up. */
export type SymItem =
  | Extract<TbItem, { type: 'line' | 'rect' | 'polyline' | 'text' }>
  | { type: 'circle'; c: Pt; r: number; stroke?: number; color?: string; fill?: string | null; hatch?: Hatch; dash?: number[] }
  | { type: 'arc'; c: Pt; r: number; start: number; end: number; w?: number; color?: string; dash?: number[] };

export type SymType = SymItem['type'];
export type SymScale = 'annotation' | 'model';
export interface SymAttr { key: string; label: string; default: string }
export interface SymDef { scale: SymScale; category: string; items: SymItem[]; attributes: SymAttr[]; description?: string }

/** A symbol as listed. */
export interface SymSummary {
  id: string; name: string; builtin: boolean; category: string; scale: SymScale; description?: string; uses: number;
  attributes: SymAttr[]; preview?: SymPreview;
}
export interface SymPreview { box: [number, number, number, number]; pens: PenDto[] }

/** A symbol's file (.lapinsymbols entry) kept on this computer. */
export interface LibrarySymbol { id: string; name: string; def: SymDef; added?: string }

export const CATEGORY_FALLBACK = ['annotation', 'north', 'section', 'elevation', 'level', 'grid', 'callout', 'revision', 'break', 'furniture', 'sanitary', 'electrical', 'fire', 'landscape', 'other'];

/** A definition as the engine sent it, its missing parts filled in. */
export function normaliseDef(d: Partial<SymDef> | null | undefined): SymDef {
  return {
    scale: d?.scale === 'model' ? 'model' : 'annotation',
    category: d?.category || 'other',
    items: Array.isArray(d?.items) ? d!.items.map((i) => ({ ...i })) as SymItem[] : [],
    attributes: Array.isArray(d?.attributes) ? d!.attributes.map((a) => ({ key: a.key, label: a.label ?? a.key, default: a.default ?? '' })) : [],
    ...(d?.description ? { description: d.description } : {}),
  };
}

// ------------------------------------------------------------ geometry of the items

const rad = (deg: number) => (deg * Math.PI) / 180;
const onCircle = (c: Pt, r: number, deg: number): Pt => [c[0] + Math.cos(rad(deg)) * r, c[1] + Math.sin(rad(deg)) * r];
const degOf = (c: Pt, p: Pt) => round((Math.atan2(p[1] - c[1], p[0] - c[0]) * 180) / Math.PI);
const norm360 = (a: number) => ((a % 360) + 360) % 360;
/** The sweep of an arc, counter-clockwise from start to end, in degrees (a full circle when they meet). */
export const sweepDeg = (start: number, end: number) => { const s = norm360(end - start); return s < 1e-9 ? 360 : s; };
const arcMid = (it: Extract<SymItem, { type: 'arc' }>): Pt => onCircle(it.c, it.r, it.start + sweepDeg(it.start, it.end) / 2);

/** The points of an item a click snaps to. */
export function symKeyPoints(item: SymItem): Pt[] {
  if (item.type === 'circle') return [item.c, onCircle(item.c, item.r, 0), onCircle(item.c, item.r, 90), onCircle(item.c, item.r, 180), onCircle(item.c, item.r, 270)];
  if (item.type === 'arc') return [item.c, onCircle(item.c, item.r, item.start), onCircle(item.c, item.r, item.end), arcMid(item)];
  return tbKeyPoints(item);
}

/** An item's box (x0, y0, x1, y1). */
export function symBounds(item: SymItem): [number, number, number, number] {
  if (item.type === 'circle') return [item.c[0] - item.r, item.c[1] - item.r, item.c[0] + item.r, item.c[1] + item.r];
  if (item.type === 'arc') {
    const pts: Pt[] = [onCircle(item.c, item.r, item.start), onCircle(item.c, item.r, item.end)];
    for (const q of [0, 90, 180, 270]) if (norm360(q - item.start) <= sweepDeg(item.start, item.end)) pts.push(onCircle(item.c, item.r, q));
    const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
    return [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)];
  }
  return tbBounds(item);
}

/** The box of all the items (a small box round the origin when there are none). */
export function defBounds(items: SymItem[]): [number, number, number, number] {
  if (items.length === 0) return [-5, -5, 5, 5];
  const bs = items.map(symBounds);
  return [Math.min(...bs.map((b) => b[0])), Math.min(...bs.map((b) => b[1])), Math.max(...bs.map((b) => b[2])), Math.max(...bs.map((b) => b[3]))];
}

export function symMove(item: SymItem, dx: number, dy: number): SymItem {
  if (item.type === 'circle' || item.type === 'arc') return { ...item, c: [round(item.c[0] + dx), round(item.c[1] + dy)] };
  return tbMoveItem(item, dx, dy) as SymItem;
}

/** The handles that reshape an item: a circle's quadrants (the radius), an arc's two ends and its middle. */
export function symHandles(item: SymItem): Pt[] {
  if (item.type === 'circle') return [0, 90, 180, 270].map((a) => onCircle(item.c, item.r, a));
  if (item.type === 'arc') return [onCircle(item.c, item.r, item.start), onCircle(item.c, item.r, item.end), arcMid(item)];
  return tbHandles(item);
}

export function symDragHandle(item: SymItem, i: number, p: Pt): SymItem {
  const q: Pt = [round(p[0]), round(p[1])];
  if (item.type === 'circle') return { ...item, r: round(Math.max(0.25, Math.hypot(q[0] - item.c[0], q[1] - item.c[1]))) };
  if (item.type === 'arc') {
    if (i === 0) return { ...item, start: degOf(item.c, q) };
    if (i === 1) return { ...item, end: degOf(item.c, q) };
    return { ...item, r: round(Math.max(0.25, Math.hypot(q[0] - item.c[0], q[1] - item.c[1]))) };
  }
  return tbDragHandle(item, i, q) as SymItem;
}

/** The topmost item under a point (mm), within `tol`; -1 for none. */
export function symItemAt(items: SymItem[], p: Pt, tol: number): number {
  for (let i = items.length - 1; i >= 0; i--) {
    const it = items[i];
    if (it.type === 'circle') {
      const d = Math.hypot(p[0] - it.c[0], p[1] - it.c[1]);
      if (Math.abs(d - it.r) <= tol || (d <= it.r && (it.fill || it.hatch))) return i;
    } else if (it.type === 'arc') {
      const d = Math.hypot(p[0] - it.c[0], p[1] - it.c[1]);
      const a = degOf(it.c, p);
      if (Math.abs(d - it.r) <= tol && norm360(a - it.start) <= sweepDeg(it.start, it.end)) return i;
      if (symKeyPoints(it).slice(1, 3).some((k) => Math.hypot(k[0] - p[0], k[1] - p[1]) <= tol)) return i;
    } else if (tbItemAt([it], p, tol) === 0) return i;
  }
  return -1;
}

/** A point snapped to the nearest key point of the items (but `except`) within `tol`, else to the grid (0: none); Shift (`ortho`) from `from`. */
export function symSnap(p: Pt, items: SymItem[], opts: { grid: number; tol: number; objects?: boolean; except?: number; from?: Pt | null; ortho?: boolean }): { p: Pt; on: 'point' | 'grid' | 'origin' | 'none' } {
  const keep = (v: Pt): Pt => (opts.ortho && opts.from ? (Math.abs(p[0] - opts.from[0]) >= Math.abs(p[1] - opts.from[1]) ? [v[0], opts.from[1]] : [opts.from[0], v[1]]) : v);
  const q = keep(p);
  if (opts.objects !== false) {
    let best: Pt | null = null, d = opts.tol;
    const cands: Pt[] = [[0, 0]];
    items.forEach((it, i) => { if (i !== opts.except) cands.push(...symKeyPoints(it)); });
    for (const k of cands) { const dk = Math.hypot(k[0] - q[0], k[1] - q[1]); if (dk <= d) { d = dk; best = k; } }
    if (best) return { p: keep([best[0], best[1]]), on: best[0] === 0 && best[1] === 0 ? 'origin' : 'point' };
  }
  if (opts.grid > 0) { const g = opts.grid; return { p: keep([round(Math.round(q[0] / g) * g), round(Math.round(q[1] / g) * g)]), on: 'grid' }; }
  return { p: q, on: 'none' };
}

/** A new item of a kind at a point, with Lapin's defaults. */
export function newSymItem(type: SymType, p: Pt, text?: string): SymItem {
  switch (type) {
    case 'line': return { type, a: p, b: [p[0] + 5, p[1]], w: 0.25 };
    case 'rect': return { type, x: p[0], y: p[1], w: 8, h: 5, stroke: 0.25 };
    case 'polyline': return { type, pts: [p, [p[0] + 5, p[1]]], stroke: 0.25 };
    case 'circle': return { type, c: p, r: 3, stroke: 0.25 };
    case 'arc': return { type, c: p, r: 3, start: 0, end: 90, w: 0.25 };
    case 'text': return { type, at: p, text: text ?? 'Text', h: 2.5, align: 'start' };
  }
}

// ------------------------------------------------------------ attributes and text

/** The key an attribute label turns into: letters and digits, a letter first. */
export function attributeKey(label: string): string {
  const k = label.trim().replace(/[^A-Za-z0-9]+(.)?/g, (_, c: string | undefined) => (c ? c.toUpperCase() : '')).replace(/^[^A-Za-z]+/, '');
  return k ? k[0].toLowerCase() + k.slice(1) : '';
}

/** The tokens `{attr.x}` a definition's texts read that its attributes do not list yet. */
export function missingAttributes(def: SymDef): string[] {
  const have = new Set(def.attributes.map((a) => a.key.toLowerCase()));
  const out: string[] = [];
  for (const it of def.items) {
    if (it.type !== 'text') continue;
    for (const m of it.text.matchAll(/\{\{?\s*attr\.([A-Za-z]\w*)/g)) if (!have.has(m[1].toLowerCase()) && !out.includes(m[1])) out.push(m[1]);
    for (const m of it.text.matchAll(/\battr\.([A-Za-z]\w*)/g)) if (!have.has(m[1].toLowerCase()) && !out.includes(m[1])) out.push(m[1]);
  }
  return out;
}

/** The attribute values of a definition's defaults. */
export const defaultValues = (attrs: SymAttr[]): Record<string, string> => Object.fromEntries(attrs.map((a) => [a.key, a.default]));

// ------------------------------------------------------------ pens: thumbnails and the ghost

/** Pens moved by (dx, dy) paper mm, new objects. */
export function shiftPens(pens: PenDto[], dx: number, dy: number): PenDto[] {
  return pens.map((p) => ({
    ...p,
    segments: p.segments?.map((v, i) => v + (i % 2 === 0 ? dx : dy)),
    loops: p.loops?.map((l) => l.map((v, i) => v + (i % 2 === 0 ? dx : dy))),
    texts: p.texts?.map((t) => ({ ...t, x: t.x + dx, y: t.y + dy })),
  }));
}

/** A preview as a picture for pensToSvg: its box padded by `pad` mm, the pens shifted into it. */
export function previewImage(p: SymPreview, pad = 1.5): { w: number; h: number; pens: PenDto[] } {
  const [x0, y0, x1, y1] = p.box;
  return { w: Math.max(1, x1 - x0 + 2 * pad), h: Math.max(1, y1 - y0 + 2 * pad), pens: shiftPens(p.pens, pad - x0, pad - y0) };
}

/** Metres a symbol's millimetre comes to in a view: an annotation symbol is paper mm (at the view's scale 1:n), a model symbol real mm. */
export const ghostFactor = (scale: SymScale, viewScale: number, instanceScale = 1): number =>
  ((scale === 'annotation' ? Math.max(1, viewScale) : 1) / 1000) * (instanceScale > 0 ? instanceScale : 1);

/** A preview's pens as shapes in a view (metres, y up) with the insertion point at `at`: for the place tool's ghost. */
export function ghostShapes(pens: PenDto[], at: Vec2, k: number): Shape[] {
  const P = (x: number, y: number): Vec2 => ({ x: at.x + x * k, y: at.y - y * k });
  const out: Shape[] = [];
  for (const p of pens) {
    const s = p.segments ?? [];
    for (let i = 0; i + 3 < s.length; i += 4) out.push({ type: 'line', a: P(s[i], s[i + 1]), b: P(s[i + 2], s[i + 3]) });
    for (const l of p.loops ?? []) {
      const pts: Vec2[] = [];
      for (let i = 0; i + 1 < l.length; i += 2) pts.push(P(l[i], l[i + 1]));
      if (pts.length >= 2) out.push({ type: 'polyline', pts, closed: true });
    }
  }
  return out;
}

/** The flips after toggling one axis. */
export const toggled = (f: { flipX: boolean; flipY: boolean }, axis: 'x' | 'y') => (axis === 'x' ? { ...f, flipX: !f.flipX } : { ...f, flipY: !f.flipY });
/** An angle turned by `by` degrees, kept in [0, 360). */
export const turned = (angle: number, by = 90) => Math.round(norm360(angle + by) * 1e6) / 1e6;

// ------------------------------------------------------------ this computer's library

const LIBRARY = 'lapin.symbols';

export const isSymbolDef = (v: unknown): v is SymDef => !!v && typeof v === 'object' && Array.isArray((v as SymDef).items);

/** The symbols kept on this computer. */
export function librarySymbols(): LibrarySymbol[] {
  try {
    const all = JSON.parse(localStorage.getItem(LIBRARY) ?? '[]') as unknown;
    return Array.isArray(all) ? all.filter((x): x is LibrarySymbol => !!x && typeof (x as LibrarySymbol).id === 'string' && typeof (x as LibrarySymbol).name === 'string' && isSymbolDef((x as LibrarySymbol).def)) : [];
  } catch { return []; }
}

function store(all: LibrarySymbol[]): boolean {
  try { localStorage.setItem(LIBRARY, JSON.stringify(all)); return true; } catch { return false; }
}

/** A symbol kept in the library under a new id; null when the storage refuses it. */
export function keepSymbol(name: string, def: SymDef): LibrarySymbol | null {
  const all = librarySymbols();
  const ids = new Set(all.map((b) => b.id));
  let id = 'sy-' + Date.now().toString(36);
  for (let n = 2; ids.has(id); n++) id = `sy-${Date.now().toString(36)}-${n}`;
  const kept: LibrarySymbol = { id, name, def, added: new Date().toISOString().slice(0, 10) };
  return store([...all, kept]) ? kept : null;
}

export function forgetSymbol(id: string): void { store(librarySymbols().filter((b) => b.id !== id)); }

export function renameSymbol(id: string, name: string): boolean {
  const all = librarySymbols();
  const at = all.findIndex((b) => b.id === id);
  if (at < 0) return false;
  all[at] = { ...all[at], name };
  return store(all);
}

/** Symbols grouped by category, the categories in the engine's order (the others after, by name), each group by name. */
export function byCategory<T extends { name: string; category: string }>(items: T[], order: string[]): [string, T[]][] {
  const cats = [...new Set(items.map((i) => i.category))].sort((a, b) => {
    const [ia, ib] = [order.indexOf(a), order.indexOf(b)];
    return (ia < 0 ? 1e6 : ia) - (ib < 0 ? 1e6 : ib) || a.localeCompare(b);
  });
  return cats.map((c) => [c, items.filter((i) => i.category === c).sort((a, b) => a.name.localeCompare(b.name))]);
}

/** A library entry's name and definition replaced. */
export function updateLibrarySymbol(id: string, name: string, def: SymDef): boolean {
  const all = librarySymbols();
  const at = all.findIndex((b) => b.id === id);
  if (at < 0) return false;
  all[at] = { ...all[at], name, def };
  return store(all);
}

// ------------------------------------------------------------ .lapinsymbols files

export const PACK_FORMAT = 'lapin-symbols';

/** Symbols as a .lapinsymbols file's text (the engine's format, version 1). */
export function packText(entries: { name: string; def: SymDef }[], name?: string): string {
  return JSON.stringify({ format: PACK_FORMAT, version: 1, ...(name ? { name } : {}), symbols: entries.map((e) => ({ name: e.name, def: e.def })) }, null, 2) + '\n';
}

/** A .lapinsymbols file's symbols, read here (for the library); null when it is not one. */
export function parsePack(text: string): { name: string; def: SymDef }[] | null {
  try {
    const o = JSON.parse(text) as { format?: string; symbols?: { name?: unknown; def?: unknown }[] };
    if (o?.format !== PACK_FORMAT || !Array.isArray(o.symbols)) return null;
    const out = o.symbols.flatMap((s) => (typeof s.name === 'string' && s.name.trim() && isSymbolDef(s.def) ? [{ name: s.name.trim(), def: normaliseDef(s.def) }] : []));
    return out.length ? out : null;
  } catch { return null; }
}

/** Which symbol the Symbols dialog opens on, and whether in the editor (Properties → Edit symbol). */
export const symbolsDialog = { open: null as string | null, edit: false };
