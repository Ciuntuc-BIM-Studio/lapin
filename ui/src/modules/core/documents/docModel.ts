/**
 * A written document as the engine fills it (docs/plan-documents.md; `document.preview`): its page, its blocks and their runs,
 * the inputs, the fields left to complete. The pure parts of the Documents dialog: a run's text, the inputs by group, the list
 * to complete with its labels, the pages the preview lays the blocks on, and a picture's pens as an SVG file.
 */

import type { PenDto } from '../../../viewport/drawing';

/** A piece of text: bold, italic, a page field ("page", "pages") or a field the project leaves empty (its key as text). */
export interface DocRun { text: string; bold?: boolean; italic?: boolean; field?: 'page' | 'pages'; missing?: boolean }

/** A picture: its size in paper mm and its pens in paper mm (y down), as `schedule.layout` gives them. */
export interface DocImage { key: string; w: number; h: number; caption?: string; pens: PenDto[] }

export type DocBlock =
  | { kind: 'heading'; level: number; runs: DocRun[] }
  | { kind: 'paragraph'; style: 'normal' | 'caption' | 'note' | string; runs: DocRun[] }
  | { kind: 'list'; ordered: boolean; items: DocRun[][] }
  | { kind: 'table'; title?: string | null; plain: boolean; total: boolean; header: string[]; numeric: number[]; widths?: number[] | null; rows: DocRun[][][] }
  | ({ kind: 'image' } & DocImage)
  | { kind: 'toc'; title?: string | null }
  | { kind: 'pageBreak' }
  | { kind: 'unknown' };

export interface DocInput { key: string; label: string; multiline: boolean; default?: string | null; group?: string | null; value?: string | null }

export interface DocPage {
  w: number; h: number; margins: [number, number, number, number]; header: DocRun[]; footer: DocRun[];
  /** The style as kept (fonts, size, headings' colour, logo), null for Lapin's own look. */
  style?: { font?: string; pdfFont?: string; size?: number; headingColor?: string; logo?: { data: string; w: number; position?: string } } | null;
}

export interface DocSummary { id: string; name: string; template: string | null; templateTitle: string | null; lang: string | null; revision: string | null; status: string | null; isoType: string }

export interface DocPreview {
  id: string; title: string; page: DocPage;
  meta: { author?: string | null; subject?: string | null; revision?: string | null; status?: string | null; lang?: string | null };
  blocks: DocBlock[]; missing: string[]; inputs: DocInput[]; summary: DocSummary;
}

/** A template as listed: where it is kept (Lapin's own, the project's, this computer's library). */
export interface DocTemplate { id: string; title: string; description: string; lang: string; isoType: string; inputs: DocInput[]; origin?: 'builtin' | 'project' | 'library' }

/** A parameter of the project's own (decision 340): a unique key, its label, type, group and value. */
export interface ProjectParameter { key: string; label: string; type: 'text' | 'number' | 'date'; group: string; value?: string }

export interface ProjectInfo {
  name: string; projectId?: string | null;
  fields: { key: string; label: string; group?: string }[]; phases: string[];
  groups?: { key: string; label: string }[];
  custom?: ProjectParameter[];
  [key: string]: unknown;
}

/** A run's text as the preview shows it: a page field as its number on that page, a missing field as ⟨key⟩. */
export function runText(r: DocRun, page = 1, pages = 1): string {
  if (r.field === 'page') return String(page);
  if (r.field === 'pages') return String(pages);
  return r.missing ? `⟨${r.text}⟩` : r.text;
}

/** Runs as plain text (a heading in the table of contents, a test). */
export const runsText = (runs: DocRun[], page = 1, pages = 1) => runs.map((r) => runText(r, page, pages)).join('');

/** The inputs by their group, the groups in the order they first appear; an input without a group goes under "". */
export function groupInputs(inputs: DocInput[]): { group: string; inputs: DocInput[] }[] {
  const out: { group: string; inputs: DocInput[] }[] = [];
  for (const i of inputs) {
    const g = i.group?.trim() ?? '';
    let at = out.find((x) => x.group === g);
    if (!at) out.push(at = { group: g, inputs: [] });
    at.inputs.push(i);
  }
  return out;
}

/** Whether an input differs from its template's text (Reset to default is offered then). */
export const isChanged = (i: DocInput) => (i.value ?? '') !== (i.default ?? '');

/**
 * What is left to complete, each with a label and where it is written: an input (`input.<key>`) in the Inputs tab, a project
 * field (`project.<key>`) in Project information, the rest (a document's own, a table or a view the project lacks) in the model.
 */
export function toComplete(missing: string[], inputs: DocInput[], info: { key: string; label: string }[]): { key: string; label: string; where: 'inputs' | 'info' | 'model'; kind?: 'table' | 'view' }[] {
  return missing.map((key) => {
    if (key.startsWith('input.')) {
      const k = key.slice(6);
      return { key, label: inputs.find((i) => i.key === k)?.label ?? k, where: 'inputs' as const };
    }
    if (key.startsWith('project.')) {
      const k = key.slice(8);
      const f = info.find((x) => x.key === k);
      if (f) return { key, label: f.label, where: 'info' as const };
      return { key, label: k === 'name' ? 'Project name' : key, where: k === 'name' ? 'info' as const : 'model' as const };
    }
    if (key.startsWith('table:')) return { key, label: key.slice(6), where: 'model' as const, kind: 'table' as const };
    if (key.startsWith('view:')) return { key, label: key.slice(5), where: 'model' as const, kind: 'view' as const };
    return { key, label: key, where: 'model' as const };
  });
}

/**
 * The blocks laid on pages: in order, a page break starting a new page, a block that does not fit what is left starting one
 * too (a block taller than a page stands alone on its own). `heights` are the blocks' heights in the page's units, `room` the
 * text's height on a page. A heading is kept with the block after it.
 */
export function paginate(blocks: { kind: string }[], heights: number[], room: number): number[][] {
  const pages: number[][] = [[]];
  let used = 0;
  for (let i = 0; i < blocks.length; i++) {
    const b = blocks[i];
    if (b.kind === 'pageBreak') {
      if (pages[pages.length - 1].length > 0) { pages.push([]); used = 0; }
      continue;
    }
    let h = heights[i] ?? 0;
    // A heading never ends a page: it goes with what it heads.
    if (b.kind === 'heading' && i + 1 < blocks.length && blocks[i + 1].kind !== 'pageBreak') h += Math.min(heights[i + 1] ?? 0, room / 4);
    if (used > 0 && used + h > room) { pages.push([]); used = 0; }
    pages[pages.length - 1].push(i);
    used += heights[i] ?? 0;
  }
  return pages;
}

// ─── A picture's pens as an SVG file (the exports' pictures) ──────────────

/** SVG units per paper mm: finer than the SVG's one decimal (as the sheets' print). */
const U = 10;

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const n = (v: number) => (Math.round(v * U * 10) / 10).toString();

/**
 * A picture's pens as a standalone SVG at its paper size (width and height in mm, a viewBox of U units a mm): fills, hatches and
 * lines a path each, true to their paper widths and dashes; texts with their height, weight and angle; on white. A pen's
 * pictures (an orthophoto) are drawn when `image` gives their data URL.
 */
export function pensToSvg(img: { w: number; h: number; pens: PenDto[] }, image?: (id: string) => string | null): string {
  const out: string[] = [];
  out.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${img.w}mm" height="${img.h}mm" viewBox="0 0 ${n(img.w)} ${n(img.h)}">`);
  out.push(`<rect x="0" y="0" width="${n(img.w)}" height="${n(img.h)}" fill="#ffffff"/>`);
  for (const p of img.pens) {
    if (p.images?.length) {
      for (const im of p.images) {
        const href = image?.(im.id);
        const c = im.corners;
        if (!href || c.length < 8) continue;
        out.push(`<image href="${esc(href)}" x="0" y="0" width="1" height="1" preserveAspectRatio="none" opacity="${im.opacity}" transform="matrix(${n(c[2] - c[0])} ${n(c[3] - c[1])} ${n(c[6] - c[0])} ${n(c[7] - c[1])} ${n(c[0])} ${n(c[1])})"/>`);
      }
      continue;
    }
    if (p.texts?.length) {
      out.push(`<g fill="${esc(p.stroke ?? '#000000')}">`);
      for (const t of p.texts) {
        const family = t.font === 'serif' ? 'Georgia, serif' : t.font === 'mono' ? 'Menlo, monospace' : 'Arial, Helvetica, sans-serif';
        const anchor = t.anchor === 'start' ? 'start' : t.anchor === 'end' ? 'end' : 'middle';
        const base = t.baseline === 'middle' ? 'central' : 'alphabetic';
        const rot = t.angle ? ` transform="rotate(${-t.angle} ${n(t.x)} ${n(t.y)})"` : '';
        out.push(`<text x="${n(t.x)}" y="${n(t.y)}" font-size="${n(t.height)}" font-family="${family}" font-weight="${t.bold ? 700 : 400}"${t.italic ? ' font-style="italic"' : ''} text-anchor="${anchor}" dominant-baseline="${base}"${rot}>${esc(t.text)}</text>`);
      }
      out.push('</g>');
      continue;
    }
    let d = '';
    for (const loop of p.loops ?? []) {
      for (let k = 0; k + 1 < loop.length; k += 2) d += `${k === 0 ? 'M' : 'L'}${n(loop[k])} ${n(loop[k + 1])}`;
      if (loop.length >= 2) d += 'Z';
    }
    const s = p.segments ?? [];
    for (let k = 0; k + 3 < s.length; k += 4) d += `M${n(s[k])} ${n(s[k + 1])}L${n(s[k + 2])} ${n(s[k + 3])}`;
    if (!d) continue;
    const stroke = p.stroke ? ` stroke="${esc(p.stroke)}" stroke-width="${n(Math.max(p.width, 0.05))}" stroke-linecap="round" stroke-linejoin="round"` : ' stroke="none"';
    const dash = p.stroke && p.dash?.length ? ` stroke-dasharray="${p.dash.map(n).join(' ')}"` : '';
    out.push(`<path d="${d}" fill="${p.fill ? esc(p.fill) : 'none'}" fill-rule="evenodd"${stroke}${dash}/>`);
  }
  out.push('</svg>');
  return out.join('');
}

/** A picture's size in pixels at a resolution: paper mm at `dpi` dots an inch. */
export const pixelsAt = (mm: number, dpi: number) => Math.max(1, Math.round((mm / 25.4) * dpi));

/** The MIME type of an exported document. */
export const DOC_MIME: Record<string, string> = {
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  odt: 'application/vnd.oasis.opendocument.text',
  pdf: 'application/pdf',
};
