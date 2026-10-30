/**
 * Remarks on sheets read back from PDF annotations (docs/plan-cde.md,
 * stage 3): what the interface knows on its own — the remarks among the
 * project's nodes (LapinSheetRemark entities), their shapes on the paper for the
 * overlay, the filters and groups of the list, who made one as initials —
 * the little store of the overlay (what is highlighted, whether it shows),
 * and the calls to the engine: a PDF read (by path on the desktop, by its
 * bytes in a browser), the import, the status, the project folder scanned.
 * The overlay is for the screen alone: a sheet prints without its remarks.
 */

import { create } from 'zustand';
import { notify, useUi } from '../../../../app/store';
import { engine, execute, useSession } from '../../../../engine/session';
import { isDesktop, pickFile, pickPath, toBase64 } from '../../../../engine/files';
import { LONG_TIMEOUT_MS } from '../../../../engine/client';
import type { NodeDto } from '../../../../engine/protocol';

export const REMARK_CLASS = 'LapinSheetRemark';

export type RemarkStatus = 'open' | 'resolved' | 'closed';
export type StatusFilter = RemarkStatus | 'all';
export interface RemarkReply { annot: string; author: string | null; date: string | null; text: string }
export interface RemarkSource { file: string; revision: string | null; issueId: string | null; page: number; annot: string; read: string }
export interface Remark {
  id: string; name: string; sheetId: string;
  /** note, text, callout, highlight, underline, squiggly, strike, ink, rect, ellipse, line, arrow, polygon, cloud, polyline, stamp, caret, redact, attachment, other. */
  kind: string;
  /** Its box on the paper, mm from the top-left corner: [x0, y0, x1, y1]. */
  rect?: number[];
  /** Its strokes, lines or marked quads as polylines (mm). */
  paths?: number[][][];
  text: string; subject: string | null; author: string | null; date: string | null; color: string | null;
  status: RemarkStatus; source: RemarkSource; replies: RemarkReply[];
}

const str = (v: unknown) => (typeof v === 'string' ? v : null);

/** A remark as the node carries it. */
export function remarkOf(n: NodeDto): Remark {
  const p = n.props as Record<string, unknown>;
  const source = (p.source ?? {}) as Partial<RemarkSource>;
  const status = p.status;
  return {
    id: n.id, name: n.name, sheetId: str(p.sheetId) ?? '', kind: str(p.kind) ?? 'other',
    rect: Array.isArray(p.rect) && p.rect.length === 4 ? (p.rect as number[]) : undefined,
    paths: Array.isArray(p.paths) ? (p.paths as number[][][]) : undefined,
    text: str(p.text) ?? '', subject: str(p.subject), author: str(p.author), date: str(p.date), color: str(p.color),
    status: status === 'resolved' || status === 'closed' ? status : 'open',
    source: { file: str(source.file) ?? '', revision: str(source.revision), issueId: str(source.issueId), page: Number(source.page ?? 1), annot: str(source.annot) ?? '', read: str(source.read) ?? '' },
    replies: Array.isArray(p.replies) ? (p.replies as RemarkReply[]) : [],
  };
}

/** The project's remarks, on one sheet when given, oldest first. */
export function remarksOf(nodes: Map<string, NodeDto>, sheetId?: string): Remark[] {
  const list: Remark[] = [];
  for (const n of nodes.values()) if (n.type === REMARK_CLASS) { const r = remarkOf(n); if (!sheetId || r.sheetId === sheetId) list.push(r); }
  return list.sort((a, b) => (a.date ?? '').localeCompare(b.date ?? '') || a.name.localeCompare(b.name));
}

/** How many remarks a sheet has that are open (neither resolved nor closed): the browser's badge, the sheet's toolbar. */
export const countOpen = (nodes: Map<string, NodeDto>, sheetId: string) => remarksOf(nodes, sheetId).filter((r) => r.status === 'open').length;

/** The remarks that pass a status filter. */
export const filterRemarks = (list: Remark[], filter: StatusFilter) => (filter === 'all' ? list : list.filter((r) => r.status === filter));

/** The remarks by sheet, the sheets in the order of their numbers, a sheet that is gone named by its id. */
export function groupBySheet(list: Remark[], nodes: Map<string, NodeDto>): { sheetId: string; label: string; remarks: Remark[] }[] {
  const groups = new Map<string, Remark[]>();
  for (const r of list) (groups.get(r.sheetId) ?? groups.set(r.sheetId, []).get(r.sheetId)!).push(r);
  return [...groups.entries()]
    .map(([sheetId, remarks]) => {
      const n = nodes.get(sheetId);
      return { sheetId, label: n ? `${String(n.props.number ?? '')} · ${n.name}`.replace(/^ · /, '') : sheetId, remarks };
    })
    .sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true }));
}

/** Who made a remark, as the initials of the marker: "Ana Pop" → AP, "ionut" → IO, nobody → ?. */
export function initials(author: string | null | undefined): string {
  const words = (author ?? '').trim().split(/[\s._-]+/).filter(Boolean);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

/** The kind as the list names it. */
export const KIND_LABELS: Record<string, string> = {
  note: 'Note', text: 'Text', callout: 'Callout', highlight: 'Highlight', underline: 'Underline', squiggly: 'Squiggly', strike: 'Strike-out', ink: 'Pen',
  rect: 'Rectangle', ellipse: 'Ellipse', line: 'Line', arrow: 'Arrow', polygon: 'Polygon', cloud: 'Cloud', polyline: 'Polyline', stamp: 'Stamp', caret: 'Insertion',
  redact: 'Redaction', attachment: 'Attachment', other: 'Mark',
};

/** What the overlay draws for a remark, in paper mm: its box, its polylines (closed as loops or open), an ellipse in the box, and where its marker sits. */
export interface RemarkShape { box: number[] | null; lines: number[][][]; closed: boolean; ellipse: boolean; marker: number[] }

export function remarkShape(r: Remark): RemarkShape {
  const box = r.rect && r.rect.length === 4 ? r.rect : null;
  const paths = (r.paths ?? []).filter((p) => p.length >= 2);
  const first = paths[0]?.[0];
  const marker = box ? [box[0], box[1]] : first ? [first[0], first[1]] : [0, 0];
  switch (r.kind) {
    case 'note': return { box: null, lines: [], closed: false, ellipse: false, marker };
    case 'highlight': case 'underline': case 'strike': case 'squiggly': case 'redact':
      return { box: paths.length ? null : box, lines: paths, closed: true, ellipse: false, marker };
    case 'cloud': case 'polygon': return { box: paths.length ? null : box, lines: paths, closed: true, ellipse: false, marker };
    case 'ink': case 'polyline': case 'line': case 'arrow': return { box: paths.length ? null : box, lines: paths, closed: false, ellipse: false, marker };
    case 'ellipse': return { box, lines: [], closed: false, ellipse: true, marker };
    default: return { box, lines: [], closed: false, ellipse: false, marker };
  }
}

/** The overlay's state: the remark in front (highlighted, its card open), whether the overlay shows at all. */
export const useRemarkUi = create<{ highlight: string | null; show: boolean }>(() => ({ highlight: null, show: true }));

/** A remark shown: its sheet opened in the pane and the remark highlighted. */
export function showRemark(r: Remark): void {
  const nodes = useSession.getState().nodes;
  if (!nodes.has(r.sheetId)) { notify('The sheet of this remark is no longer in the project.'); return; }
  useRemarkUi.setState({ highlight: r.id, show: true });
  useUi.setState({ view: 'sheet', sheetId: r.sheetId, selection: r.sheetId, selected: [r.sheetId], dialog: null });
}

// ------------------------------------------------------------ the engine

export interface PdfPageSheet { page: number; sheetId: string | null; number: string | null; name: string | null; annotations: number }
export interface PdfReadResult {
  file: string; path?: string; pages: number; annotations: number; skipped: number; unplaced: number;
  /** How the sheet was found: the project's issue record, a manifest beside the file, the file name's convention, the user's choice, or not at all. */
  by: 'issue' | 'manifest' | 'name' | 'chosen' | 'none';
  revision: string | null; issueId: string | null; sheets: PdfPageSheet[];
  remarks: Omit<Remark, 'id' | 'name' | 'status'>[]; warnings: string[];
}
export interface PdfSource { path?: string; data?: string; name: string }

/** A PDF picked: by path on the desktop, by its bytes in a browser; null when stepped back. */
export async function pickPdf(): Promise<PdfSource | null> {
  if (isDesktop()) {
    const path = await pickPath('open', undefined, [{ name: 'PDF', extensions: ['pdf'] }]);
    return path ? { path, name: path.split(/[\\/]/).pop() ?? path } : null;
  }
  const picked = await pickFile('PDF', 'pdf');
  return picked ? { name: picked.name, data: toBase64(picked.bytes) } : null;
}

/** The file read for its remarks, mapped onto the sheet matched or given; the interface confirms before importing. */
export const readPdf = (source: PdfSource, sheetId?: string) =>
  engine.call<PdfReadResult>('cde.readPdf', { ...source, ...(sheetId ? { sheetId } : {}) }, { timeoutMs: LONG_TIMEOUT_MS });

export const importRemarks = (remarks: PdfReadResult['remarks']) => execute('cde.importRemarks', { remarks }) as Promise<{ added: number; updated: number; ids: string[] } | null>;
export const setRemarkStatus = (ids: string[], status: RemarkStatus) => execute('cde.setRemarkStatus', { ids, status });
export const deleteRemarks = (ids: string[]) => execute('cde.deleteRemarks', { ids });

export interface ScannedPdf {
  path: string; name: string; container: string; sheetId: string | null; sheetNumber: string | null; sheetName: string | null; set: boolean;
  revision: string | null; issue: string | null; modified: string;
  /** Changed after the issue it belongs to. */
  newer: boolean;
  /** When its remarks were last read into the project; null never. */
  read: string | null;
  /** Changed after the issue and not read since. */
  unread: boolean;
}

/** The project folder's Shared and Published containers scanned for the issued PDFs (the desktop). */
export const scanRemarks = () => engine.call<ScannedPdf[]>('cde.scanRemarks', {}, { timeoutMs: LONG_TIMEOUT_MS });
