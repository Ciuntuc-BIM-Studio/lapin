/**
 * The project folder as a CDE (docs/plan-cde.md): what the interface knows
 * on its own — the revision after the last, the container a status suggests,
 * the findings grouped for the list, where a finding takes the user — and the
 * calls to the engine. Issuing writes files, so it is the desktop app's; the
 * checks and the settings run anywhere.
 */

import { notify, select, useUi } from '../../../app/store';
import { engine, query, useSession } from '../../../engine/session';
import { isDesktop } from '../../../engine/files';
import { isGuest } from '../../../engine/share';
import { LONG_TIMEOUT_MS } from '../../../engine/client';

export interface Finding { rule: string; severity: 'block' | 'warn'; message: string; entityId?: string; sheetId?: string; viewId?: string }
export interface CheckResult { blocking: number; warnings: number; findings: Finding[]; rules: { id: string; label: string; blocking: boolean }[] }
export interface IssuedFile { name: string; kind: string; sheetId?: string; sha256: string; size: number; drawingHash?: string }
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

/** Whether issuing is possible here: the desktop app (or a development run that asks for it), never for a guest. */
export const canIssue = () => (isDesktop() || devOverride()) && !isGuest();

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

export interface IssueRequest { name: string; revision: string; status: string; container: string; sheets: string[]; model: boolean; dxf: boolean; set: boolean; note: string }
export interface IssueResult extends Issue { path: string; warnings: Finding[] }

/** The issue: the files written into the container and the issue recorded (the version is saved after, by the caller). */
export const issue = (r: IssueRequest) => engine.call<IssueResult>('cde.issue', { ...r }, { timeoutMs: LONG_TIMEOUT_MS });
