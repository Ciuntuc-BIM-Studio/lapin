/**
 * Document templates kept on this computer (docs/plan-documents.md, stage 4): the app's or the browser's storage, as the price
 * lists are, so any project here starts documents from them; each kept whole (the engine's template JSON) with the day it was
 * kept. Pure parts too: the list the Documents dialog offers (the engine's built-in and project templates, then the library's
 * the project has not), a free id, and the style as the dialog edits it.
 */

import type { DocInput, DocTemplate } from './docModel';

/** A template whole, as `document.templateJson` gives it and `document.create` / `document.importTemplate` take it. */
export interface TemplateJson {
  id: string; title: string; description?: string; lang?: string; isoType?: string;
  page?: Record<string, unknown> & { style?: DocStyle }; inputs?: DocInput[]; blocks: unknown[];
  /** When it was kept in the library (YYYY-MM-DD). */
  added?: string;
}

/** A page's style (`page.style`): DOCX/ODT font, PDF font, body size (pt), headings' colour, the header's logo. */
export interface DocStyle {
  font?: string; pdfFont?: 'helvetica' | 'times'; size?: number; headingColor?: string;
  logo?: { data: string; w: number; position?: 'left' | 'right' };
}

export type TemplateOrigin = 'builtin' | 'project' | 'library';

/** The fonts offered for DOCX and ODT (the engine takes any family name). */
export const STYLE_FONTS = ['Arial', 'Calibri', 'Times New Roman', 'Georgia', 'Verdana', 'Liberation Sans'];
/** The logo's file at most (the engine refuses more): it travels in the project and in each document. */
export const MAX_LOGO_BYTES = 1 << 20;

const LIBRARY = 'lapin.documentTemplates';
/** What the library may take of the storage (the browser gives about 5 MB to a site, shared with the price lists). */
export const MAX_LIBRARY_CHARS = 3_000_000;

/** The templates kept on this computer. */
export function libraryTemplates(): TemplateJson[] {
  try {
    const all = JSON.parse(localStorage.getItem(LIBRARY) ?? '[]') as unknown;
    return Array.isArray(all) ? all.filter((t): t is TemplateJson => !!t && typeof t === 'object' && typeof (t as TemplateJson).id === 'string' && Array.isArray((t as TemplateJson).blocks)) : [];
  } catch { return []; }
}

export const libraryTemplate = (id: string) => libraryTemplates().find((t) => t.id === id) ?? null;

function store(all: TemplateJson[]): boolean {
  const text = JSON.stringify(all);
  if (text.length > MAX_LIBRARY_CHARS) return false;
  try { localStorage.setItem(LIBRARY, text); return true; } catch { return false; }
}

/**
 * A template kept in the library: 'kept'; 'exists' when one has its id and `overwrite` is not asked (nothing changed);
 * 'full' when the storage cannot take it (the library left as it was).
 */
export function keepTemplate(t: TemplateJson, overwrite = false): 'kept' | 'exists' | 'full' {
  const all = libraryTemplates();
  const at = all.findIndex((x) => x.id === t.id);
  if (at >= 0 && !overwrite) return 'exists';
  const kept = { ...t, added: new Date().toISOString().slice(0, 10) };
  if (at >= 0) all[at] = kept; else all.push(kept);
  return store(all) ? 'kept' : 'full';
}

export function forgetTemplate(id: string): void {
  store(libraryTemplates().filter((t) => t.id !== id));
}

/** A library template changed in place (renamed, its style set); false when it is not there or the storage refuses. */
export function changeLibraryTemplate(id: string, change: (t: TemplateJson) => TemplateJson): boolean {
  const all = libraryTemplates();
  const at = all.findIndex((t) => t.id === id);
  if (at < 0) return false;
  all[at] = change(all[at]);
  return store(all);
}

/** A page with its style set (null or an empty style: Lapin's own look). */
export function withStyle(page: TemplateJson['page'], style: DocStyle | null): TemplateJson['page'] {
  const p = { ...(page ?? {}) };
  if (style && Object.keys(style).length > 0) p.style = style; else delete p.style;
  return p;
}

/** An id from a title (letters and digits) that none of `taken` has. */
export function freeId(title: string, taken: Iterable<string>): string {
  const used = new Set(taken);
  const stem = title.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 40).replace(/-+$/, '') || 'template';
  if (!used.has(stem)) return stem;
  for (let i = 2; ; i++) if (!used.has(`${stem}-${i}`)) return `${stem}-${i}`;
}

/** A library template as the dialogs list it. */
export const summaryOf = (t: TemplateJson): DocTemplate => ({
  id: t.id, title: t.title || t.id, description: t.description ?? '', lang: t.lang ?? 'ro', isoType: t.isoType ?? 'RP', inputs: t.inputs ?? [], origin: 'library',
});

/** The templates a document starts from: the engine's (built-in, then the project's), then the library's the project has not. */
export function mergeTemplates(engine: DocTemplate[], library: TemplateJson[]): DocTemplate[] {
  const ids = new Set(engine.map((t) => t.id));
  return [...engine.map((t) => ({ ...t, origin: t.origin ?? 'builtin' as const })), ...library.filter((t) => !ids.has(t.id)).map(summaryOf)];
}

/** Whether a value is a template file's content (an id and blocks), to import it. */
export const isTemplateJson = (v: unknown): v is TemplateJson =>
  !!v && typeof v === 'object' && typeof (v as TemplateJson).id === 'string' && (v as TemplateJson).id.trim().length > 0 && Array.isArray((v as TemplateJson).blocks);
