/**
 * The Archicad connector's interface side (docs/plan-archicad.md, A1): the running Archicads the engine finds (ports 19723–19744,
 * Tapir's JSON API), what a receive would do, the receive, and Archicad's IFC put on the project's Atrium server as a reference
 * model. The pure parts (labels, rows, defaults, the body of the server's reference) are here and tested.
 */

import { engine } from '../../engine/session';
import { LONG_TIMEOUT_MS } from '../../engine/client';
import { need, putObject } from '../../engine/atrium';
import { syncReferences } from '../../engine/remote';
import { archicadProposal, propose, proposalMade } from '../core/projectInfo/serverInfo';

export const GUIDE_URL = 'https://lapinbim.com/docs/archicad/';

export interface ArchicadInstance {
  port: number; archicad: string | null; build: number | null; tapir: string | null; project: string | null;
  untitled: boolean; teamwork: boolean; ok: boolean; problem: string | null;
}
export interface ArchicadStorey { index: number; name: string; level: number; height: number }
export interface ArchicadSurvey {
  port: number; project: string | null; archicad: string | null; tapir: string | null; storeys: ArchicadStorey[];
  elements: number; byType: Record<string, number>; native: Record<string, number>; references: number; skipped: Record<string, number>;
  existing: number; updates: number; adds: number; removes: number; fromThisProject: boolean; ms: number; notes: string[];
  projectInfo?: ArchicadProjectInfo | null;
}
export interface ArchicadRecognition { recognized: number; offered: number; byClass: Record<string, number>; kept: Record<string, number>; reverted: number }
export interface ArchicadLevels { matched: number; made: number; renamed: number; moved: number; removed: number; kept: number }
/**
 * What Archicad's Project Info does to the project's information (A1b): the empty fields it fills, those set here with another value
 * (left as they are: the dialog offers to take them), those the same, and what no field takes (kept apart as `info.source.archicad`).
 */
export interface ArchicadProjectInfo {
  source: 'archicad';
  filled: { key: string; value: string }[];
  differs: { key: string; lapin: string; archicad: string }[];
  same: string[];
  kept: { id: string; label: string | null; value: string }[];
}
export interface ArchicadReceived {
  elements: number; added: number; updated: number; unchanged: number; removed: number; native: number; references: number;
  storeys: Record<string, number>; levels: ArchicadLevels | null; recognition: ArchicadRecognition | null;
  skipped: Record<string, number>; notes: string[]; groupId: string | null; projectInfo?: ArchicadProjectInfo | null;
}
/** Archicad's Project Info for Send to Atrium's proposal: Tapir's answer as it is, the project file's name, and what it would do here. */
export interface ArchicadProjectInfoRead extends ArchicadProjectInfo { raw: unknown[]; file: string | null }
export interface ArchicadTranslator { name: string; preview: boolean }
export interface ArchicadIfc { name: string; size: number; sha256: string; data: string }

export type ReceiveStoreys = 'create' | 'existing';
export type ReceiveOrigin = 'project' | 'survey';
export interface ReceiveOptions { storeys: ReceiveStoreys; origin: ReceiveOrigin; recognize: boolean; asCopy: boolean }
export const DEFAULT_OPTIONS: ReceiveOptions = { storeys: 'create', origin: 'project', recognize: true, asCopy: false };

const KEY_PORT = 'lapin.archicad.port';
const read = (k: string): string | null => { try { return localStorage.getItem(k); } catch { return null; } };
const write = (k: string, v: string) => { try { localStorage.setItem(k, v); } catch { /* this session only */ } };
export const lastPort = (): number | null => { const n = Number(read(KEY_PORT)); return Number.isInteger(n) && n > 0 ? n : null; };
export const rememberPort = (port: number) => write(KEY_PORT, String(port));

// ------------------------------------------------------------------------------------------------ pure helpers

/** The instance to show first: the port used last if it still works, else the first that works, else the first. */
export function pickInstance(list: ArchicadInstance[], last: number | null = null): ArchicadInstance | null {
  return list.find((i) => i.port === last && i.ok) ?? list.find((i) => i.ok) ?? list[0] ?? null;
}

/** An instance as the picker names it: the project (or "Untitled project", said by `untitled`), Archicad's version and the port. */
export function instanceLabel(i: ArchicadInstance, untitled = 'Untitled project'): string {
  const name = i.project?.trim() || untitled;
  return [name, i.archicad ? `Archicad ${i.archicad}` : 'Archicad', `port ${i.port}`].join(' · ');
}

/** A map's entries, the largest count first, then by name. */
export const countRows = (m: Record<string, number> | null | undefined): [string, number][] =>
  Object.entries(m ?? {}).filter(([, n]) => n > 0).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));

/** The rows of a report: label (an interface phrase) and its number, only those worth showing. */
export function reportRows(r: ArchicadReceived): { label: string; value: number }[] {
  const rows: { label: string; value: number; always?: boolean }[] = [
    { label: 'Native elements', value: r.native, always: true },
    { label: 'References', value: r.references, always: true },
    { label: 'Added', value: r.added, always: true },
    { label: 'Updated', value: r.updated, always: true },
    { label: 'Unchanged', value: r.unchanged, always: true },
    { label: 'Removed', value: r.removed, always: true },
    { label: 'Storeys made', value: r.levels?.made ?? 0 },
    { label: 'Storeys renamed', value: r.levels?.renamed ?? 0 },
    { label: 'Storeys moved', value: r.levels?.moved ?? 0 },
    { label: 'Kept as references by the fidelity gate', value: r.recognition?.reverted ?? 0 },
  ];
  return rows.filter((x) => x.always || x.value > 0).map(({ label, value }) => ({ label, value }));
}

/** Why elements stayed references, as the engine says it (English), the most frequent first. */
export const keptRows = (r: ArchicadReceived): [string, number][] => countRows(r.recognition?.kept);

/** The engine's arguments for archicad.receive (and survey: the port alone). */
export function receiveArgs(port: number, o: ReceiveOptions): Record<string, unknown> {
  const a: Record<string, unknown> = { port, storeys: o.storeys, recognize: o.recognize, origin: o.origin };
  if (o.asCopy) a.asCopy = true;
  return a;
}

export const defaultTitle = (project: string | null | undefined) => project?.trim() || 'Archicad model';
export const defaultFileName = (title: string) => `${title.trim().replace(/[\\/:*?"<>|]+/g, '-') || 'Archicad model'}.ifc`;

/** The translator to show first: the one made for previews (the IFC the viewer reads), else the first. */
export const pickTranslator = (list: ArchicadTranslator[]): string => (list.find((t) => t.preview) ?? list[0])?.name ?? '';

export interface SendForm { server: string | null; port: number | null; translator: string; title: string; folder: string; file: string }

/** What is missing from a send's form, as interface phrases (empty when it can go). */
export function sendProblems(f: SendForm): string[] {
  const p: string[] = [];
  if (!f.server) p.push('Connect the project to a server first: File → Collaborate…');
  if (f.port == null) p.push('Open Archicad with the Tapir add-on first.');
  if (!f.translator) p.push('Choose an IFC translator.');
  if (!f.title.trim()) p.push('Give the model a title.');
  if (!f.folder) p.push('Choose a folder.');
  if (!f.file.trim()) p.push('Give the file a name.');
  return p;
}

const IFC64 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$';

/** A new IFC GlobalId (22 characters, the 128 bits in IFC's base 64). */
export function newGlobalId(random: (n: number) => Uint8Array = (n) => crypto.getRandomValues(new Uint8Array(n))): string {
  const b = random(16);
  let n = 0n;
  for (const x of b) n = (n << 8n) | BigInt(x);
  let s = '';
  for (let i = 0; i < 22; i++) { s = IFC64[Number(n & 63n)] + s; n >>= 6n; }
  return s;
}

/** The body of the server's `POST references` for an IFC sent from Archicad (ReferenceSync.Create's body). */
export function referenceBody(id: string, f: { title: string; folder: string; discipline: string }, file: { name: string; sha256: string; size: number }): Record<string, unknown> {
  const body: Record<string, unknown> = {
    id, title: f.title.trim(), type: 'model', folder: f.folder, status: 'received',
    files: [{ name: file.name, sha256: file.sha256, size: file.size, mime: 'application/x-step' }],
  };
  if (f.discipline.trim()) body.discipline = f.discipline.trim();
  return body;
}

export const fromBase64 = (data: string): Uint8Array => Uint8Array.from(atob(data), (c) => c.charCodeAt(0));

/** A receive's report in a line (the notice). */
export const receivedSummary = (r: ArchicadReceived) => r.recognition ? 'Received from Archicad: native elements and references.' : 'Received from Archicad.';

// ------------------------------------------------------------------------------------------------ the engine

const ask = <T>(name: string, args: Record<string, unknown> = {}, long = false) =>
  engine.call<T>('doc.query', { name, args }, long ? { timeoutMs: LONG_TIMEOUT_MS } : undefined);

export const instances = () => ask<ArchicadInstance[]>('archicad.instances');
export const survey = (port: number) => ask<ArchicadSurvey>('archicad.survey', { port }, true);
export const translators = (port: number) => ask<ArchicadTranslator[]>('archicad.translators', { port });
export const saveIfc = (port: number, translator: string) => ask<ArchicadIfc>('archicad.saveIfc', { port, translator }, true);
export const readProjectInfo = (port: number) => ask<ArchicadProjectInfoRead>('archicad.projectInfo', { port });

/** The fields ticked among those that differ, as project.setInfo takes them (Archicad's values). */
export function takenFields(info: ArchicadProjectInfo, keys: Iterable<string>): Record<string, string> {
  const want = new Set(keys);
  return Object.fromEntries(info.differs.filter((d) => want.has(d.key)).map((d) => [d.key, d.archicad]));
}

/** The model into the project: one undo step; its diff arrives as an event. */
export async function receive(port: number, o: ReceiveOptions): Promise<ArchicadReceived> {
  const r = await engine.call<ArchicadReceived>('doc.execute', { command: 'archicad.receive', args: receiveArgs(port, o) }, { timeoutMs: LONG_TIMEOUT_MS });
  rememberPort(port);
  return r;
}

/** The server's reference document for an IFC: its bytes in the project's store first, then the record. */
export async function createReference(server: string, project: string, body: Record<string, unknown>): Promise<void> {
  await need<unknown>(server, 'POST', `atrium/1.0/projects/${encodeURIComponent(project)}/references`, body);
}

export type SendStep = 'saving' | 'uploading' | 'creating' | 'syncing' | 'proposing';

/** What the proposal of Send to Atrium did: its number and how many changes it holds, or nothing differed, or why it was not made. */
export type ProposalOutcome = { made: true; number: number; changes: number } | { made: false; reason: string };

/** Archicad's IFC to the server as a reference model, step by step (`step` is told before each). */
export async function sendToServer(
  a: { server: string; project: string; port: number; translator: string; title: string; folder: string; discipline: string; file: string; proposeInfo?: boolean },
  step: (s: SendStep) => void = () => undefined,
): Promise<{ id: string; size: number; proposal?: ProposalOutcome }> {
  step('saving');
  const ifc = await saveIfc(a.port, a.translator);
  step('uploading');
  await putObject(a.server, a.project, ifc.sha256, fromBase64(ifc.data));
  step('creating');
  const id = newGlobalId();
  await createReference(a.server, a.project, referenceBody(id, a, { name: a.file.trim(), sha256: ifc.sha256, size: ifc.size }));
  step('syncing');
  await syncReferences(a.server, true);
  rememberPort(a.port);
  if (!a.proposeInfo) return { id, size: ifc.size };
  // A1b: Archicad's Project Info proposed to the server's record (an owner applies it); a failure here leaves the model sent.
  step('proposing');
  let proposal: ProposalOutcome;
  try {
    const info = await readProjectInfo(a.port);
    const answer = await propose(a.server, a.project, archicadProposal(info.raw, info.file, id));
    proposal = proposalMade(answer) ? { made: true, number: answer.number, changes: answer.changes.length } : { made: false, reason: 'no_changes' };
  } catch (e) { proposal = { made: false, reason: e instanceof Error ? e.message : String(e) }; }
  return { id, size: ifc.size, proposal };
}
