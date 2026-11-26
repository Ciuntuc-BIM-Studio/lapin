/**
 * Manage → Reference documents… (docs/plan-cde-references.md, stage R2): what the interface knows on its own — the documents' shape
 * as the engine's `references.*` queries answer it, the filters and the sort of the list, the folders flattened for a picker, a
 * folder's documents with its subfolders', the state of a checklist item, a file's kind for the preview, the project's phases
 * and issuers as typed — and the calls to the engine. The engine keeps the metadata, the folders and the files; the interface
 * passes `today` (the reader's own day) to every query that takes it.
 */

import { execute, query } from '../../../engine/session';
import { toBase64 } from '../../../engine/files';
import type { NodeDto } from '../../../engine/protocol';
import { daysBetween, expiryOf, today, type Expiry } from './expiry';

export type Named = string | Record<string, string | undefined>;

export interface RefFile { name: string; sha256: string; size: number; mime?: string; held: boolean; source?: string }
export interface ChecklistRequire { key: string; label?: Named; document?: string }
export interface RefDoc {
  id: string; title: string; type?: string; typeName?: string; issuer?: string; number?: string; date?: string; validFrom?: string; validUntil?: string;
  phase?: string[]; discipline?: string; status: string; expired: boolean; folder?: string; folderPath?: string; files: RefFile[];
  requires?: ChecklistRequire[]; relatesTo?: string[]; fields?: Record<string, string>; supersededBy?: string; supersedes?: string[]; reason?: string;
  requestedAt?: string; requestedBy?: string; addedAt?: string; addedBy?: string; modifiedAt?: string; modifiedBy?: string; withdrawnAt?: string;
  /** On a server (R4): the names it knows for `addedBy` and `modifiedBy`, the files' text state (`pending` while being read) and the record's ETag. */
  addedByName?: string; modifiedByName?: string; textStatus?: string; etag?: string;
}
export interface RefFolder { id: string; name: Named; text: string; count: number; total: number; derived?: string; children: RefFolder[] }
export interface FolderTree {
  folders: RefFolder[]; typeFolders: Record<string, string>; roleFolders: Record<string, string>; unfiled: number; language: string; root: string; keepLimit: number; projectFolder: string | null;
}
export interface RefTypeField { key: string; label: Named; kind?: string }
export interface RefType { id: string; name: Named; folder?: string; fields: RefTypeField[]; validityMonths?: number; checklist?: boolean }
export interface ChecklistOption { key: string; group?: string; label: Named; text?: string; folder?: string }
export interface TypeList { types: RefType[]; typeFolders: Record<string, string>; checklist: ChecklistOption[]; issuers: string[]; statuses: string[] }
export type ChecklistState = 'missing' | 'requested' | 'received' | 'expired';
export interface ChecklistItem { key: string; label: Named; text: string; state: ChecklistState; document?: string; answeredBy?: string; number?: string; date?: string; issuer?: string; validUntil?: string }
export interface Checklist { id: string; items: ChecklistItem[]; options: ChecklistOption[]; missing: number; complete: boolean }
export interface Suggestion {
  number?: string; date?: string; issuer?: string; validUntil?: string; validityMonths?: number; validUntilIndicative?: boolean; type?: string;
  evidence?: Record<string, string>; warning?: string;
}
export interface ScanFile { path: string; relative: string; name: string; size: number; sha256: string; folder?: string; type?: string }
export interface ScanMissing { id: string; name: string; expected: string; foundAt?: string }
export interface ScanResult { root: string | null; exists: boolean; warning?: string; truncated?: boolean; files: ScanFile[]; missing: ScanMissing[] }
export interface AddResult { id: string; files: RefFile[]; duplicates: string[]; warnings: string[] }

/** A name in the language given: that one, else English, else Romanian, else any, as the engine's own `NameOf`. */
export function nameIn(name: Named | undefined, lang: string): string {
  if (!name) return '';
  if (typeof name === 'string') return name;
  return name[lang] ?? name.en ?? name.ro ?? Object.values(name).find((v) => !!v) ?? '';
}

/** The language the folders' names are shown in: Romanian when the interface is Romanian, English otherwise (the two the template has). */
export const folderLanguage = (ui: string) => (ui === 'ro' ? 'ro' : 'en');

/** Whether the project has no reference settings of its own yet (nothing changed since it was made), read from its project node. */
export function hasNoReferenceSettings(nodes: Map<string, NodeDto>): boolean {
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  return !!project && project.props.references == null;
}

// ---------------------------------------------------------------- the list

export interface RefFilter { folder: string | null; type: string; phase: string; status: string; within: number | null; q: string }
export const NO_FILTER: RefFilter = { folder: null, type: '', phase: '', status: '', within: null, q: '' };

/** The ids of a folder and everything below it. */
export function folderIds(tree: RefFolder[], id: string): Set<string> {
  const out = new Set<string>();
  const walk = (nodes: RefFolder[], inside: boolean) => {
    for (const n of nodes) {
      const here = inside || n.id === id;
      if (here) out.add(n.id);
      walk(n.children ?? [], here);
    }
  };
  walk(tree, false);
  return out;
}

/** The documents that pass the filters: by folder (with its subfolders), type, phase, status as shown, validity ending within N days, and text. */
export function filterRefs(docs: RefDoc[], f: RefFilter, tree: RefFolder[], now = today()): RefDoc[] {
  const inFolder = f.folder ? folderIds(tree, f.folder) : null;
  const q = f.q.trim().toLowerCase();
  return docs.filter((d) => {
    if (inFolder && !inFolder.has(d.folder ?? '')) return false;
    if (f.type && d.type !== f.type) return false;
    if (f.phase && !(d.phase ?? []).some((p) => p.toLowerCase() === f.phase.toLowerCase())) return false;
    if (f.status && d.status !== f.status) return false;
    if (f.within != null) {
      if (d.status !== 'received' || !d.validUntil) return false;
      if (daysBetween(now, d.validUntil) > f.within) return false;
    }
    if (q && ![d.title, d.number, d.issuer, d.typeName].some((x) => x?.toLowerCase().includes(q))) return false;
    return true;
  });
}

export type SortKey = 'title' | 'type' | 'issuer' | 'number' | 'date' | 'validUntil' | 'status';

/** The list in order of a column; empty values last whichever way it goes. */
export function sortRefs(docs: RefDoc[], key: SortKey, desc = false): RefDoc[] {
  const value = (d: RefDoc) => (key === 'type' ? d.typeName ?? d.type : d[key]) ?? '';
  return [...docs].sort((a, b) => {
    const x = value(a), y = value(b);
    if (!x && !y) return 0;
    if (!x) return 1;
    if (!y) return -1;
    const c = x.localeCompare(y, undefined, { numeric: true, sensitivity: 'base' });
    return desc ? -c : c;
  });
}

/** The folders as a flat list for a picker, each with its depth; the derived ones (which hold no documents) apart or left out. */
export function flattenFolders(tree: RefFolder[], withDerived = false): { id: string; text: string; depth: number; derived?: string }[] {
  const out: { id: string; text: string; depth: number; derived?: string }[] = [];
  const walk = (nodes: RefFolder[], depth: number) => {
    for (const n of nodes) {
      if (n.derived && !withDerived) continue;
      out.push({ id: n.id, text: n.text, depth, derived: n.derived });
      walk(n.children ?? [], depth + 1);
    }
  };
  walk(tree, 0);
  return out;
}

export type Tone = 'ok' | 'warn' | 'bad' | 'muted';

/** What a document's status badge says: its status, and whether its validity is running out or past (days: left, or ago). */
export function badgeOf(d: Pick<RefDoc, 'status' | 'validUntil'>, now = today()): { label: string; tone: Tone; expiry: Expiry } {
  const expiry = expiryOf(d.status, d.validUntil, now);
  switch (d.status) {
    case 'requested': return { label: 'Requested', tone: 'warn', expiry: null };
    case 'superseded': return { label: 'Superseded', tone: 'muted', expiry: null };
    case 'withdrawn': return { label: 'Withdrawn', tone: 'muted', expiry: null };
    case 'expired': return { label: 'Expired', tone: 'bad', expiry };
    default: return expiry ? { label: 'Received', tone: 'warn', expiry } : { label: 'Received', tone: 'ok', expiry: null };
  }
}

/** The checklist item's state word and its tone. */
export const CHECKLIST_STATES: Record<ChecklistState, { label: string; tone: Tone }> = {
  missing: { label: 'Missing', tone: 'bad' },
  requested: { label: 'Requested', tone: 'warn' },
  received: { label: 'Received', tone: 'ok' },
  expired: { label: 'Expired', tone: 'bad' },
};

/** The documents a checklist item can be answered by: the live ones (not withdrawn, not superseded), the item's own folder's first. */
export function answerCandidates(docs: RefDoc[], selfId: string, folder?: string): RefDoc[] {
  return docs
    .filter((d) => d.id !== selfId && d.status !== 'withdrawn' && d.status !== 'superseded')
    .sort((a, b) => Number((b.folder ?? '') === folder) - Number((a.folder ?? '') === folder) || a.title.localeCompare(b.title));
}

/** The checklist as the engine takes it: a key, a label and the document that answers it. */
export const requiresOf = (items: ChecklistItem[]): ChecklistRequire[] => items.map((i) => ({ key: i.key, label: i.label, ...(i.document ? { document: i.document } : {}) }));

export type FileKind = 'pdf' | 'image' | 'ifc' | 'text' | 'other';

/** What the preview does with a file: a PDF is drawn, an image shown, an IFC offered as a link, text read. */
export function fileKind(name: string, mime?: string): FileKind {
  const ext = name.toLowerCase().match(/\.([a-z0-9]+)$/)?.[1] ?? '';
  if (ext === 'pdf' || mime === 'application/pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg'].includes(ext) || mime?.startsWith('image/')) return 'image';
  if (ext === 'ifc' || ext === 'ifczip') return 'ifc';
  if (['txt', 'md', 'csv'].includes(ext)) return 'text';
  return 'other';
}

/** The phases typed in one field, as a list: separated by commas or semicolons. */
export const splitList = (s: string) => s.split(/[,;]/).map((x) => x.trim()).filter(Boolean);

/** The phases the project's documents name and the usual ones, once each. */
export function phaseChoices(docs: RefDoc[], projectPhases: string[] = []): string[] {
  const seen = new Map<string, string>();
  for (const p of [...projectPhases, ...docs.flatMap((d) => d.phase ?? []), 'SF', 'DTAC', 'PT', 'DE']) if (!seen.has(p.toLowerCase())) seen.set(p.toLowerCase(), p);
  return [...seen.values()];
}

/** Names of the documents a drop or a pick holds, read to base64 for `references.add`. */
export async function readFiles(files: File[]): Promise<{ name: string; data: string }[]> {
  return Promise.all(files.map(async (f) => ({ name: f.name, data: toBase64(new Uint8Array(await f.arrayBuffer())) })));
}

/** Whether a drag carries files from outside the page. */
export const dragHasFiles = (e: { dataTransfer: DataTransfer | null }) => !!e.dataTransfer && [...e.dataTransfer.types].includes('Files');

/** The mime type of a drag of the dialog's own documents. */
export const DRAG_TYPE = 'application/x-lapin-reference';

// ---------------------------------------------------------------- the engine

export const listRefs = () => query<RefDoc[]>('references.list', { today: today() }).then((r) => r ?? []);
export const folderTree = () => query<FolderTree>('references.folders');
export const typeList = () => query<TypeList>('references.types');
export const checklistOf = (id: string) => query<Checklist>('references.checklist', { id, today: today() });
export const suggestFor = (id: string, name?: string) => query<Suggestion>('references.suggest', { id, ...(name ? { name } : {}) });
export const scanFolder = () => query<ScanResult>('references.scan');
export interface RefFileData { name: string; mime?: string; size: number; data: string }
/**
 * A file's bytes as base64: the project's copy, else (a file above the project's keep limit that a sync left on the server) fetched
 * from the project's server through `remote.referenceFile` when the project's remote is a signed-in server (stage R4).
 */
export async function fileOf(id: string, name?: string): Promise<RefFileData | null> {
  const here = await query<RefFileData>('references.file', { id, ...(name ? { name } : {}) });
  if (here) return here;
  if (!name) return null;
  try {
    const { historyStatus, remoteCall, serverOf, signedIn } = await import('../../../engine/remote');
    const server = serverOf(await historyStatus());
    if (!server || !await signedIn(server)) return null;
    return await remoteCall<RefFileData>('remote.referenceFile', { id, name }, server);
  } catch { return null; }
}
export const textOf = (id: string, name?: string, pages?: string) => query<{ pageCount: number; pages: { page: number; text: string }[]; warning?: string }>('references.text', { id, ...(name ? { name } : {}), ...(pages ? { pages } : {}) });

export const addRef = (args: Record<string, unknown>) => execute('references.add', { ...args, today: today() }) as Promise<AddResult | null>;
export const updateRef = (id: string, patch: Record<string, unknown>) => execute('references.update', { id, ...patch, today: today() });
export const moveRefs = (ids: string[], folder: string) => execute('references.move', { ids, folder });
export const attachFiles = (id: string, files: { name: string; data: string }[]) => execute('references.attach', { id, files }) as Promise<{ files: RefFile[]; warnings: string[] } | null>;
export const detachFile = (id: string, name: string) => execute('references.detach', { id, name });
export const supersedeRef = (id: string, by: string) => execute('references.supersede', { id, by });
export const withdrawRef = (id: string, reason: string) => execute('references.withdraw', { id, reason });
export const deleteRef = (id: string) => execute('references.delete', { id });
export const setFolders = (args: { folders?: unknown; typeFolders?: Record<string, string>; roleFolders?: Record<string, string>; language?: string }) => execute('references.setFolders', args);
export const setTypes = (types: RefType[]) => execute('references.setTypes', { types });
