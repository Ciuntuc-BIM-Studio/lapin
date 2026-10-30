/**
 * The CDE (docs/plan-cde.md): what the interface knows on its own — the
 * revision after the last, the container a status suggests, the findings
 * grouped for the list, where a finding takes the user, what the Issue
 * dialog shows after an issue — and the calls to the engine. The project's
 * file holds the issues and their deliverables, so issuing, the checks and
 * the deliverables (open, save, download as zip) work everywhere, the
 * browser too; only what needs a real folder (the project folder, publishing
 * to it, scanning it) is the desktop app's.
 */

import { notify, select, useUi } from '../../../app/store';
import { engine, execute, query, useSession } from '../../../engine/session';
import { download, exportFile, isDesktop } from '../../../engine/files';
import { isGuest } from '../../../engine/share';
import { LONG_TIMEOUT_MS } from '../../../engine/client';

export interface Finding { rule: string; severity: 'block' | 'warn'; message: string; entityId?: string; sheetId?: string; viewId?: string }
export interface CheckResult { blocking: number; warnings: number; findings: Finding[]; rules: { id: string; label: string; blocking: boolean }[] }
export interface IssuedFile { name: string; kind: string; sheetId?: string; sha256: string; size: number; drawingHash?: string; stored?: boolean }
export interface Issue { id: string; number: number; name: string; at: string; revision: string; status: string; container: string; folder: string | null; note: string | null; version: number | null; files: IssuedFile[] }
export interface Naming { project: string; originator: string; volume: string; level: string; role: string; separator: string }
export interface CdeSettings {
  folder: string | null; exists: boolean; containers: string[]; naming: Naming; example: string; requiredProps: string[];
  roles: { code: string; label: string }[]; statuses: { code: string; label: string; container: string }[];
  lastRevision: string | null; nextRevision: string; nextNumber: number;
}
export interface Names { revision: string; status: string; number: number; sheets: { id: string; number: string; title: string; name: string; file: string; problems: string[] }[]; model: string; set: string; transmittal: string }

/** The revision after `last`: P02 after P01, C01 for the first contractual one, C02 after C01; P01 with none. */
export function nextRevision(last: string | null | undefined, contractual?: boolean): string {
  const m = last ? /^([PC])(\d{2,3})$/.exec(last) : null;
  const wasC = m?.[1] === 'C';
  const toC = contractual ?? wasC;
  if (!m) return toC ? 'C01' : 'P01';
  if (toC && !wasC) return 'C01';
  if (!toC && wasC) return last!;
  return `${m[1]}${String(Number(m[2]) + 1).padStart(m[2].length, '0')}`;
}

/** The container a status goes to by default: S0 in WIP, the other S codes Shared, A, B and CR Published. */
export const containerFor = (status: string) => (status === 'S0' ? 'WIP' : status.startsWith('S') ? 'Shared' : 'Published');

export const isRevision = (s: string) => /^[PC]\d{2,3}$/.test(s);

/** The findings by rule, blocking rules first, each group in the order the engine gave. */
export function groupFindings(findings: Finding[], rules: CheckResult['rules']): { id: string; label: string; blocking: boolean; findings: Finding[] }[] {
  const groups = new Map<string, Finding[]>();
  for (const f of findings) (groups.get(f.rule) ?? groups.set(f.rule, []).get(f.rule)!).push(f);
  return [...groups.entries()]
    .map(([id, list]) => ({ id, label: rules.find((r) => r.id === id)?.label ?? id, blocking: list.some((f) => f.severity === 'block'), findings: list }))
    .sort((a, b) => Number(b.blocking) - Number(a.blocking));
}

/** Where a finding takes the user: the element selected, the sheet or the view opened. */
export function goTo(f: Finding): void {
  const nodes = useSession.getState().nodes;
  if (f.entityId && nodes.has(f.entityId)) { select(f.entityId); return; }
  if (f.sheetId && nodes.has(f.sheetId)) { useUi.setState({ view: 'sheet', sheetId: f.sheetId, selection: f.sheetId, selected: [f.sheetId], dialog: null }); return; }
  if (f.viewId) {
    if (f.viewId.startsWith('plan:')) { useUi.setState({ view: 'plan', storeyId: f.viewId.slice(5), dialog: null }); return; }
    if (nodes.has(f.viewId)) { useUi.setState({ view: 'drawing', drawingViewId: f.viewId, selection: f.viewId, selected: [f.viewId], dialog: null }); return; }
  }
  notify('This is not in the project as it is now.');
}

/** The development switch (`pnpm dev` in a browser): the folder typed in, issuing let through to the local engine. Never in a build. */
export function devOverride(): boolean {
  if (!import.meta.env.DEV) return false;
  try { return localStorage.getItem('lapin.cde.dev') === '1'; } catch { return false; }
}

/** Whether issuing is possible here: anywhere the project's file is (the desktop, the browser), never for a guest of a shared session. */
export const canIssue = () => !isGuest();

/** Whether a real folder can be reached here (the project folder, publishing to it, scanning it): the desktop app, or a development run that asks for it. */
export const canUseFolder = () => (isDesktop() || devOverride()) && !isGuest();

export const cdeSettings = () => query<CdeSettings>('cde.settings');
export const cdeIssues = () => query<Issue[]>('cde.issues').then((r) => r ?? []);
export const cdeCheck = (sheets?: string[], revision?: string) => query<CheckResult>('cde.check', { ...(sheets ? { sheets } : {}), ...(revision ? { revision } : {}) });
export const cdeNames = (args: { sheets?: string[]; revision?: string; status?: string }) => query<Names>('cde.naming', args);

/** A folder picked with the desktop's dialog; null when stepped back (or no desktop). */
export async function pickFolder(current: string | null): Promise<string | null> {
  if (!isDesktop()) return null;
  const dialog = await import('@tauri-apps/plugin-dialog');
  const r = await dialog.open({ directory: true, multiple: false, defaultPath: current ?? undefined });
  return typeof r === 'string' ? r : null;
}

/** The folder shown in the system's file manager (the desktop). */
export async function openFolder(path: string): Promise<void> {
  if (!isDesktop()) { notify('Opening a folder needs the desktop app.'); return; }
  try {
    const { invoke } = await import('@tauri-apps/api/core');
    await invoke('open_path', { path });
  } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
}

export interface IssueRequest {
  name: string; revision: string; status: string; container: string; sheets: string[]; model: boolean; dxf: boolean; set: boolean; note: string;
  /** Keep the IFC in the project file too (off by default: a large model is tens of MB an issue). */
  keepModel: boolean;
  /** Also write the files into the project folder's container (when it has one, on the desktop). */
  toFolder: boolean;
  /** The engine in the browser: the project's file comes back with the reply, to keep. */
  bytes?: boolean;
}
export interface IssueResult extends Issue {
  /** The container's folder the files were also written into; null when they are in the project file alone. */
  path: string | null; warnings: Finding[];
  /** Whether the project was saved as a version in the same call (it is clean after). */
  saved?: boolean; versionError?: string; folderProblem?: string;
  /** The project's file, when the engine handed it back (the browser). */
  file?: { name: string; data: string };
}

/**
 * The issue: the files made, kept in the project file (and written into the project folder's container when asked), the issue recorded and the
 * project saved as a version named after it, all in one call; the project is clean after.
 */
export const issue = (r: IssueRequest) => engine.call<IssueResult>('cde.issue', { ...r }, { timeoutMs: LONG_TIMEOUT_MS });

// ─── The Issue dialog after an issue ─────────────────────────────────────

/** Where the dialog is: the form, an issue going out, or the issue just made, shown as issued until a new one is started or the dialog closes. */
export type IssuePhase = { kind: 'form' } | { kind: 'issuing' } | { kind: 'done'; result: IssueResult };
export type IssueEvent = { type: 'start' } | { type: 'issued'; result: IssueResult } | { type: 'failed' } | { type: 'new' };

export function issuePhase(state: IssuePhase, event: IssueEvent): IssuePhase {
  switch (event.type) {
    case 'start': return state.kind === 'form' ? { kind: 'issuing' } : state;
    case 'issued': return { kind: 'done', result: event.result };
    case 'failed': return state.kind === 'issuing' ? { kind: 'form' } : state;
    case 'new': return { kind: 'form' };
  }
}

/** What the dialog shows of an issue made: its own number, revision, status and files as issued — never the next issue's names. */
export function issuedSummary(r: IssueResult): { title: string; files: { name: string; kept: boolean }[]; kept: number; where: string } {
  const files = r.files.map((f) => ({ name: f.name, kept: f.stored === true }));
  return {
    title: `${r.name} · ${r.revision} ${r.status} · ${r.container}`,
    files,
    kept: files.filter((f) => f.kept).length,
    where: r.path ?? '',
  };
}

// ─── The deliverables kept in the project file ────────────────────────────

export interface IssueStorage { size: number; blobs: number; issues: { id: string; number: number; files: number; size: number; shared: number }[] }

/** What the issues keep in the project file, in all and per issue (bytes shared with an earlier issue counted there). */
export const issueStorage = () => query<IssueStorage>('cde.storage');

/** The hashes a record says are kept that the project file does not hold. */
export const missingHashes = () => engine.call<string[]>('cde.missing').catch(() => [] as string[]);

const extOf = (name: string) => name.split('.').pop()?.toLowerCase() ?? '';
const MIME: Record<string, string> = { pdf: 'application/pdf', csv: 'text/csv', json: 'application/json', dxf: 'application/dxf', ifc: 'application/x-step', zip: 'application/zip' };

/** An issued file opened from the project file: on the desktop written to a temporary file and opened with its program; in a browser downloaded. */
export async function openIssued(id: string, name: string): Promise<void> {
  try {
    if (isDesktop()) {
      const r = await engine.call<{ path: string }>('cde.issueFile', { id, name, temp: true }, { timeoutMs: LONG_TIMEOUT_MS });
      const { invoke } = await import('@tauri-apps/api/core');
      await invoke('open_path', { path: r.path });
      return;
    }
    const r = await engine.call<{ name: string; data: string }>('cde.issueFile', { id, name }, { timeoutMs: LONG_TIMEOUT_MS });
    download(Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0)), r.name, MIME[extOf(r.name)]);
  } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
}

/** An issued file saved where the user says (the desktop) or downloaded. */
export const saveIssued = (id: string, name: string) => exportFile('cde.issueFile', { id, name }, name, extOf(name));

/** An issue's kept files as one zip, named after its transmittal: saved where the user says, or downloaded (the browser). */
export const downloadIssueZip = (issue: Issue) => {
  const manifest = issue.files.find((f) => f.kind === 'json')?.name;
  return exportFile('cde.issueZip', { id: issue.id }, manifest ? manifest.replace(/\.json$/i, '.zip') : `Issue ${issue.number} ${issue.revision}.zip`, 'zip');
};

/**
 * An issue written out to a folder later: the project folder's container (as an issue writes it), or a folder picked for it alone (flat).
 * The desktop's; what the project file does not keep (the IFC unless asked) is named as missing.
 */
export async function publishIssue(issue: Issue, folder?: string): Promise<void> {
  try {
    const r = await engine.call<{ path: string; written: string[]; same: number; missing: string[] }>('cde.publishIssue', { id: issue.id, ...(folder ? { folder, flat: true } : {}) }, { timeoutMs: LONG_TIMEOUT_MS });
    notify(`Published issue ${issue.number}: ${r.written.length} written, ${r.same} already there${r.missing.length ? `, ${r.missing.length} not kept in the project file` : ''}.`);
  } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
}

/** An issue's files no longer kept in the project file (all, or the kinds given); its record, names and hashes stay. */
export const dropIssueFiles = (id: string, kinds?: string[]) => execute('cde.dropIssueFiles', { id, ...(kinds ? { kinds } : {}) });

/** A size as people read it. */
export const formatSize = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : bytes >= 1024 ? `${Math.round(bytes / 1024)} kB` : `${bytes} B`);
