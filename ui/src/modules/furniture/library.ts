/**
 * My furniture library (docs/plan-furniture.md, Configuratorul flexibil, stage 3; decision 476): the furniture types a user
 * keeps to use in any project. Furniture types → Save to my library… keeps the type (its spec, IFC type and materials, as the
 * engine's `furniture.libraryItem` gives them) with a name, a category, tags, a thumbnail drawn from the engine's preview, an
 * article code and an indicative price of the user's own; My furniture… lists them to insert or place in the open project
 * (`furniture.insertFromLibrary`: materials matched by name or made, an identical type reused, one undo step).
 *
 * Where they live, as project templates do (decision 470): on the desktop, JSON files in the user's folder kept by the engine
 * (`furnitureLibrary.*`); in the browser, the page's IndexedDB (`lapin-furniture`), the engine only checking, packing and
 * unpacking entries. Either way a library (or one entry) goes out as a .lapinlib file and comes back from one.
 */

import { create } from 'zustand';
import { notify, setTool, useUi } from '../../app/store';
import { engine, engineInBrowser, query } from '../../engine/session';
import { indexedRows, type Rows } from '../../engine/templates';
import { chooseType, type PreviewDto } from './furniture';

export const CATEGORIES = ['kitchen', 'living', 'bedroom', 'bathroom', 'office', 'storage', 'other'] as const;
export type Category = (typeof CATEGORIES)[number];
export const CATEGORY_LABELS: Record<Category, string> = {
  kitchen: 'Kitchen', living: 'Living', bedroom: 'Bedroom', bathroom: 'Bathroom', office: 'Office', storage: 'Storage', other: 'Other',
};

/** The furniture type an entry holds, as the engine gives it. */
export interface LibraryType { class: string; name: string; predefinedType?: string; spec: Record<string, unknown>; materials: { name: string }[] }
export interface LibraryEntry {
  id: string; name: string; category: Category; tags: string[]; article?: string; price?: number; thumbnail?: string; at: string;
  type: LibraryType;
}
/** What Save to my library… asks. */
export interface EntryMeta { name: string; category: Category; tags: string[]; article?: string; price?: number | null; thumbnail?: string }

/** Tags as typed: split at commas, trimmed, once each (case aside), the first spelling kept. */
export function parseTags(text: string): string[] {
  const out: string[] = [];
  for (const t of text.split(',').map((x) => x.trim()).filter(Boolean)) if (!out.some((o) => o.toLowerCase() === t.toLowerCase())) out.push(t);
  return out;
}

/** What the browser shows: matching the search (name, tags, article, kind) and the category and tag chosen, by name. */
export function filterEntries(entries: LibraryEntry[], f: { search?: string; category?: string | null; tag?: string | null }): LibraryEntry[] {
  const words = (f.search ?? '').toLowerCase().split(/\s+/).filter(Boolean);
  return entries
    .filter((e) => !f.category || e.category === f.category)
    .filter((e) => !f.tag || e.tags.some((t) => t.toLowerCase() === f.tag!.toLowerCase()))
    .filter((e) => {
      const text = [e.name, ...e.tags, e.article ?? '', String(e.type.spec.kind ?? ''), String(e.type.spec.preset ?? '')].join(' ').toLowerCase();
      return words.every((w) => text.includes(w));
    })
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** Every tag of the library, once, by name. */
export const allTags = (entries: LibraryEntry[]): string[] => {
  const seen = new Map<string, string>();
  for (const e of entries) for (const t of e.tags) if (!seen.has(t.toLowerCase())) seen.set(t.toLowerCase(), t);
  return [...seen.values()].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
};

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
const n = (v: number) => (Math.round(v * 10000) / 10000).toString();

/** The engine's preview of a type drawn as its thumbnail: the front, its pieces in their materials' colours, as an SVG data URL. */
export function thumbnailSvg(p: PreviewDto, size = 160): string {
  const front = p.front ?? [];
  const x0 = Math.min(...front.map((f) => f.x0), -0.1), x1 = Math.max(...front.map((f) => f.x1), 0.1);
  const z1 = Math.max(...front.map((f) => f.z1), 0.1);
  const span = Math.max(x1 - x0, z1) * 1.12;
  const cx = (x0 + x1) / 2, cy = -z1 / 2;
  const vb = `${n(cx - span / 2)} ${n(cy - span / 2)} ${n(span)} ${n(span)}`;
  const rects = front.map((f) => `<rect x="${n(f.x0)}" y="${n(-f.z1)}" width="${n(f.x1 - f.x0)}" height="${n(f.z1 - f.z0)}" fill="${esc(f.color)}" stroke="rgba(0,0,0,.35)" stroke-width="${n(span / 260)}"/>`).join('');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="${vb}">${rects}<line x1="${n(cx - span / 2)}" x2="${n(cx + span / 2)}" y1="0" y2="0" stroke="#777" stroke-width="${n(span / 200)}"/></svg>`;
  const bytes = new TextEncoder().encode(svg);
  let bin = '';
  for (const b of bytes) bin += String.fromCharCode(b);
  return `data:image/svg+xml;base64,${btoa(bin)}`;
}

/** One text for an entry's content whatever order its keys are in: what "the same" compares. */
export function canonical(v: unknown): string {
  if (Array.isArray(v)) return `[${v.map(canonical).join(',')}]`;
  if (v && typeof v === 'object') return `{${Object.keys(v).sort().map((k) => `${JSON.stringify(k)}:${canonical((v as Record<string, unknown>)[k])}`).join(',')}}`;
  return JSON.stringify(v ?? null);
}
const body = (e: LibraryEntry) => { const rest: Partial<LibraryEntry> = { ...e }; delete rest.id; delete rest.at; return canonical(rest); };

/**
 * A library file's entries taken in beside those kept (the engine's rule, for the browser): one whose id is kept with the
 * same content is skipped, one whose id is kept with other content comes in under a new id.
 */
export function mergeImported(kept: LibraryEntry[], incoming: LibraryEntry[], newId: () => string): { add: LibraryEntry[]; skipped: number } {
  const byId = new Map(kept.map((e) => [e.id, e]));
  const add: LibraryEntry[] = [];
  let skipped = 0;
  for (const e of incoming) {
    const there = byId.get(e.id);
    if (there && body(there) === body(e)) { skipped++; continue; }
    const entry = there ? { ...e, id: newId() } : e;
    byId.set(entry.id, entry);
    add.push(entry);
  }
  return { add, skipped };
}

const newId = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(16)}${Math.random().toString(16).slice(2)}`).replace(/-/g, '');

/** The browser's library over its rows: the same answers the desktop's engine gives (the engine checks each entry). */
export class BrowserFurniture {
  constructor(private rows: Rows<LibraryEntry>, private check: (e: unknown) => Promise<LibraryEntry>) {}

  async list(): Promise<LibraryEntry[]> { return filterEntries(await this.rows.all(), {}); }

  async get(id: string): Promise<LibraryEntry> {
    const e = (await this.rows.all()).find((r) => r.id === id);
    if (!e) throw new Error('There is no such furniture in your library.');
    return e;
  }

  async add(entry: Omit<LibraryEntry, 'id' | 'at'>): Promise<string> {
    const e = await this.check({ ...entry, id: newId(), at: new Date().toISOString() });
    await this.rows.put(e);
    return e.id;
  }

  async update(id: string, patch: Partial<EntryMeta>): Promise<LibraryEntry> {
    const e: Record<string, unknown> = { ...(await this.get(id)) };
    for (const [k, v] of Object.entries(patch)) { if (v === null) delete e[k]; else if (v !== undefined) e[k] = v; }
    const checked = await this.check(e);
    await this.rows.put(checked);
    return checked;
  }

  async duplicate(id: string): Promise<string> {
    const e = await this.get(id);
    return this.add({ ...e, name: `${e.name} copy` });
  }

  remove(id: string): Promise<void> { return this.rows.remove(id); }

  async import(entries: LibraryEntry[]): Promise<{ added: string[]; skipped: number }> {
    const { add, skipped } = mergeImported(await this.rows.all(), entries, newId);
    for (const e of add) await this.rows.put(e);
    return { added: add.map((e) => e.id), skipped };
  }
}

let browser: BrowserFurniture | null = null;
const inBrowser = () => (browser ??= new BrowserFurniture(indexedRows<LibraryEntry>('lapin-furniture', 'furniture', 'furniture'),
  (entry) => engine.call<LibraryEntry>('furnitureLibrary.check', { entry })));

/** The list the dialogs show, read again after each change. */
export const useFurnitureLibrary = create<{ entries: LibraryEntry[]; read: boolean }>(() => ({ entries: [], read: false }));

/** Save to my library…'s type, set when it opens. */
export const useSaveToLibrary = create<{ typeId: string | null }>(() => ({ typeId: null }));

export async function refreshLibrary(): Promise<LibraryEntry[]> {
  const entries = engineInBrowser ? await inBrowser().list() : await engine.call<LibraryEntry[]>('furnitureLibrary.list').catch(() => [] as LibraryEntry[]);
  useFurnitureLibrary.setState({ entries, read: true });
  return entries;
}

/** A type's thumbnail: the engine's preview of its spec, drawn; none when the engine cannot preview it. */
export async function thumbnailOf(spec: Record<string, unknown>): Promise<string | undefined> {
  const p = await query<PreviewDto>('furniture.preview', { kind: spec.kind, spec });
  return p?.front?.length ? thumbnailSvg(p) : undefined;
}

/** A project's furniture type kept in the library: the entry's id. */
export async function saveToLibrary(typeId: string, meta: EntryMeta): Promise<string> {
  const type = await engine.call<LibraryType>('doc.query', { name: 'furniture.libraryItem', args: { typeId } });
  const thumbnail = meta.thumbnail ?? (await thumbnailOf(type.spec));
  const entry = { ...meta, price: meta.price ?? undefined, thumbnail, type };
  const id = engineInBrowser ? await inBrowser().add(entry) : (await engine.call<{ id: string }>('furnitureLibrary.add', { entry })).id;
  await refreshLibrary();
  return id;
}

export async function entryOf(id: string): Promise<LibraryEntry> {
  return engineInBrowser ? inBrowser().get(id) : engine.call<LibraryEntry>('furnitureLibrary.get', { id });
}

/** An entry's type in the open project (one undo step): the type's id and whether an identical one was there. */
export async function insertEntry(id: string): Promise<{ id: string; reused: boolean }> {
  const entry = await entryOf(id);
  const r = await engine.call<{ id: string; reused: boolean }>('doc.execute', { command: 'furniture.insertFromLibrary', args: { item: entry.type } });
  return r;
}

export async function updateEntry(id: string, patch: Partial<EntryMeta>): Promise<void> {
  if (engineInBrowser) await inBrowser().update(id, patch);
  else await engine.call('furnitureLibrary.update', { id, patch });
  await refreshLibrary();
}

export async function duplicateEntry(id: string): Promise<string> {
  const to = engineInBrowser ? await inBrowser().duplicate(id) : (await engine.call<{ id: string }>('furnitureLibrary.duplicate', { id })).id;
  await refreshLibrary();
  return to;
}

export async function deleteEntry(id: string): Promise<void> {
  if (engineInBrowser) await inBrowser().remove(id);
  else await engine.call('furnitureLibrary.delete', { id });
  await refreshLibrary();
}

/** Entries (none: all) as a .lapinlib file: written at `path` on the desktop, or handed back as base64 to download. */
export async function exportEntries(ids: string[] | null, path?: string): Promise<{ data?: string; name?: string }> {
  if (engineInBrowser) {
    const all = await inBrowser().list();
    const entries = ids ? all.filter((e) => ids.includes(e.id)) : all;
    return engine.call<{ data: string }>('furnitureLibrary.pack', { entries });
  }
  return engine.call<{ data?: string; name?: string }>('furnitureLibrary.export', { ...(ids ? { ids } : {}), ...(path ? { path } : {}) });
}

/** A .lapinlib file taken in (its path on the desktop, its bytes in the browser). */
export async function importLibrary(file: { path?: string; data?: string }): Promise<{ added: string[]; skipped: number }> {
  let r: { added: string[]; skipped: number };
  if (engineInBrowser) r = await inBrowser().import(await engine.call<LibraryEntry[]>('furnitureLibrary.unpack', { data: file.data }));
  else r = await engine.call<{ added: string[]; skipped: number }>('furnitureLibrary.import', file);
  await refreshLibrary();
  return r;
}

/** A category from a type's kind (and a casework preset), to start Save to my library… with. */
export function guessCategory(kind: string, preset = ''): Category {
  if (kind === 'kitchen') return 'kitchen';
  if (preset === 'vanity') return 'bathroom';
  if (kind === 'desk' || preset === 'desk') return 'office';
  if (['bed', 'bedside', 'wardrobe'].includes(kind) || ['bed', 'nightstand', 'wardrobe', 'wardrobeSliding', 'chest'].includes(preset)) return 'bedroom';
  if (['sofa', 'cornerSofa', 'armchair', 'coffeeTable', 'table', 'chair', 'sideboard'].includes(kind) || ['sideboard', 'tvUnit'].includes(preset)) return 'living';
  if (['bookcase', 'chest'].includes(kind) || kind === 'casework') return 'storage';
  return 'other';
}

/** The tool that places a type of the kind (a kitchen is drawn along a wall, the others put down at a click). */
export const toolOfKind = (kind: string) => `furniture.${kind}`;

/** An entry's type in the open project; `place`: the tool that puts it down starts with it. */
export async function takeEntry(e: LibraryEntry, place: boolean): Promise<void> {
  try {
    const r = await insertEntry(e.id);
    const kind = String(e.type.spec.kind ?? '');
    chooseType(kind, r.id);
    if (place) {
      useUi.setState({ dialog: null });
      setTool(toolOfKind(kind));
      notify(kind === 'kitchen' ? 'Draw the kitchen along a wall: two points.' : 'Click where it stands.');
    } else notify(r.reused ? 'The project already has this type: it is chosen for its tool.' : 'Inserted: it is in Furniture types and chosen for its tool.');
  } catch (err) { notify(err instanceof Error ? err.message : String(err)); }
}

/** Opens Save to my library… on a type. */
export function saveTypeToLibrary(typeId: string) {
  useSaveToLibrary.setState({ typeId });
  useUi.setState({ dialog: 'furnitureSaveToLibrary' });
}
