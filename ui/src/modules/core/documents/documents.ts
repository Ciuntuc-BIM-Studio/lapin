/**
 * The project's written documents through the engine (docs/plan-documents.md): the templates, the list, a document's preview,
 * its commands, and the exports. A DOCX or ODT carries its pictures as the interface makes them: each picture's pens as SVG,
 * and a PNG of it at 200 dpi (the engine has no rasteriser; Word reads the PNG, newer Word and LibreOffice the SVG too).
 */

import { engine, execute, query, useSession } from '../../../engine/session';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { saveFile } from '../../../engine/files';
import { create } from 'zustand';
import { notify, useUi } from '../../../app/store';
import { DOC_MIME, pensToSvg, pixelsAt, type DocImage, type DocPreview, type DocSummary, type DocTemplate, type ProjectInfo } from './docModel';
import { libraryTemplate, libraryTemplates, mergeTemplates, type DocStyle, type TemplateJson } from './templateLibrary';

export const listDocuments = () => query<DocSummary[]>('document.list').then((r) => r ?? []);
/** The engine's templates: Lapin's own and the project's, each with its origin. */
export const engineTemplates = () => query<DocTemplate[]>('document.templates').then((r) => r ?? []);
/** Every template a document can start from: the engine's, then this computer's library (origin library). */
export const documentTemplates = () => engineTemplates().then((r) => mergeTemplates(r, libraryTemplates()));
export const documentPreview = (id: string) => query<DocPreview>('document.preview', { id });
export const projectInfo = () => query<ProjectInfo>('project.info');

/** A template the engine is given: its id when the engine has it, else the library's template whole. */
async function templateArg(id: string): Promise<string | TemplateJson> {
  const kept = libraryTemplate(id);
  if (!kept) return id;
  return (await engineTemplates()).some((t) => t.id === id) ? id : kept;
}

/** A document from a template: one of the engine's by its id, the library's given whole. */
export const createDocument = async (template: string, name?: string) =>
  execute('document.create', { template: await templateArg(template), ...(name ? { name } : {}) }).then((r) => (r as { id?: string } | null)?.id ?? null);
export const updateDocument = (id: string, args: { name?: string; lang?: string; inputs?: Record<string, string | null> }) => execute('document.update', { id, ...args });
export const deleteDocument = (id: string) => execute('document.delete', { id });
export const duplicateDocument = (id: string) => execute('document.duplicate', { id }).then((r) => (r as { id?: string } | null)?.id ?? null);
/** Update from template: from the library's template, given whole, when the engine has not the one the document came from. */
export async function refreshDocument(id: string): Promise<unknown> {
  const from = (await listDocuments()).find((d) => d.id === id)?.template;
  const t = from ? await templateArg(from) : null;
  return execute('document.refresh', { id, ...(t && typeof t !== 'string' ? { template: t } : {}) });
}
export const setProjectInfo = (fields: Record<string, string | null>) => execute('project.setInfo', fields);

// ── Templates (stage 4): the project's, saved, renamed, deleted, imported; the style of a document or a template ──

/** A template whole: an engine template by its id, or one made from a document (title, description). */
export const templateJson = (args: { templateId: string } | { id: string; title?: string; description?: string; newId?: string }) => query<TemplateJson>('document.templateJson', args);
export const saveTemplate = (id: string, title: string, description?: string, templateId?: string) =>
  execute('document.saveTemplate', { id, title, ...(description != null ? { description } : {}), ...(templateId ? { templateId } : {}) }).then((r) => (r as { templateId?: string } | null)?.templateId ?? null);
export const deleteTemplate = (templateId: string) => execute('document.deleteTemplate', { templateId });
export const renameTemplate = (templateId: string, title: string, description?: string) => execute('document.renameTemplate', { templateId, title, ...(description != null ? { description } : {}) });
export const importTemplate = (template: TemplateJson, options: { overwrite?: boolean; title?: string } = {}) =>
  execute('document.importTemplate', { template: stripAdded(template), ...options }).then((r) => (r as { templateId?: string; replaced?: boolean } | null) ?? null);
export const documentStyle = (args: { id: string } | { templateId: string }) => query<DocStyle>('document.style', args).then((r) => r ?? {});
export const setDocumentStyle = (args: { id: string } | { templateId: string }, style: DocStyle | null) => execute('document.setStyle', { ...args, style });

/** A library template without what only the library keeps. */
function stripAdded(t: TemplateJson): TemplateJson {
  const { added: _added, ...rest } = t;
  return rest;
}

/** A picture's data URL for a pen's image (an orthophoto: a LapinImage the interface holds). */
function imageData(id: string): string | null {
  const n = useSession.getState().nodes.get(id);
  const data = n?.props.data, mime = n?.props.mime;
  return typeof data === 'string' && typeof mime === 'string' ? `data:${mime};base64,${data}` : null;
}

/** An SVG drawn onto a white canvas at `dpi`: a PNG's base64 (without its data: prefix); null when the browser cannot. */
export async function rasterize(svg: string, w: number, h: number, dpi = 200): Promise<string | null> {
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
  try {
    const img = new Image();
    img.decoding = 'async';
    await new Promise<void>((done, fail) => { img.onload = () => done(); img.onerror = () => fail(new Error('The picture could not be drawn.')); img.src = url; });
    const canvas = document.createElement('canvas');
    canvas.width = pixelsAt(w, dpi);
    canvas.height = pixelsAt(h, dpi);
    const g = canvas.getContext('2d');
    if (!g) return null;
    g.fillStyle = '#ffffff';
    g.fillRect(0, 0, canvas.width, canvas.height);
    g.drawImage(img, 0, 0, canvas.width, canvas.height);
    const png = canvas.toDataURL('image/png');
    return png.slice(png.indexOf(',') + 1);
  } catch {
    return null;
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** The pictures of a document for a DOCX or ODT: {key: {svg, png}}. */
export async function documentPictures(id: string): Promise<Record<string, { svg: string; png?: string }>> {
  const images = (await query<DocImage[]>('document.images', { id })) ?? [];
  const out: Record<string, { svg: string; png?: string }> = {};
  for (const img of images) {
    const svg = pensToSvg(img, imageData);
    const png = await rasterize(svg, img.w, img.h);
    out[img.key] = png ? { svg, png } : { svg };
  }
  return out;
}

/** A document written by the engine as a file: its name, its bytes as base64, what is left to complete. */
export async function exportBytes(id: string, format: 'docx' | 'odt' | 'pdf'): Promise<{ name: string; data: string; missing: string[] } | null> {
  const images = format === 'pdf' ? undefined : await documentPictures(id);
  // A query (it changes nothing), with time for a long document; its error said, not swallowed.
  return engine.call<{ name: string; data: string; missing: string[] }>('doc.query', { name: 'document.export', args: { id, format, ...(images ? { images } : {}) } }, { timeoutMs: LONG_TIMEOUT_MS });
}

/** Export DOCX, ODT or PDF: written by the engine, saved where the user says on the desktop, downloaded in a browser. */
export async function exportDocument(id: string, format: 'docx' | 'odt' | 'pdf'): Promise<void> {
  try {
    const r = await exportBytes(id, format);
    if (!r) return;
    const bytes = Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0));
    const saved = await saveFile(bytes, r.name, DOC_MIME[format]);
    // What the project does not fill yet is written empty: said, so it is not sent unnoticed.
    if (saved) notify(r.missing.length ? `Exported ${saved}; ${r.missing.length} field(s) still empty (To complete).` : `Exported ${saved}.`);
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

/** The document the Documents dialog shows (the browser opens it on one). */
export const useDocumentsDialog = create<{ id: string | null }>(() => ({ id: null }));

/** Manage → Documents…, on a document when one is given. */
export function openDocuments(id?: string | null): void {
  if (id) useDocumentsDialog.setState({ id });
  useUi.setState({ dialog: 'documents' });
}
