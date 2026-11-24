/**
 * Checks on foreign documents (Atrium docs/plan-cde-web.md W5; the server's contract in its docs/atrium-server.md § Checks on foreign
 * documents): the report a submission carries for its PDF and DXF files, the poller that waits for a run to end, where a finding's
 * box falls on a rendered page, and each finding said from its code, field and details (the interface translates the words; values
 * stay as they are). The screens are modules/core/CdeChecks.tsx.
 */

import type { Person } from './atrium';

export type Severity = 'error' | 'warning' | 'info';
export type Bbox = [number, number, number, number];

export interface Finding {
  check: string; code: string; severity: Severity; message: string;
  page: number | null; bbox: Bbox | null; field: string | null; details: Record<string, unknown> | null;
}
export interface TitleField { value: string; bbox: Bbox | null; source: string }
export interface CheckedPage { page: number; width: number; height: number; titleBlock: { found: boolean; bbox: Bbox | null; fields: Record<string, TitleField> } | null }
export interface CheckedFile { name: string; sha256: string; kind: 'pdf' | 'dxf' | null; checked: boolean; code: string | null; revision: string | null; pages: CheckedPage[]; findings: Finding[] }
export interface CheckSummary { errors: number; warnings: number; infos: number }
export interface ChecksReport {
  status: 'pending' | 'done' | 'failed'; run: number; requested: string | null; requestedBy: Person | null; finished: string | null; error: string | null;
  summary: CheckSummary | null; files?: CheckedFile[];
}
/** The checks' part of the publish gate. */
export interface GateChecks { status: ChecksReport['status']; errors: number; warnings: number; infos: number }

// ------------------------------------------------------------ waiting for a run

/** The waits between two reads while the checks run: quick at first (most runs take a second or two), then every 10 s. */
export const POLL_DELAYS = [1000, 1500, 2000, 3000, 5000, 8000, 10000];

const pause = (ms: number, signal?: AbortSignal) => new Promise<void>((resolve) => {
  if (signal?.aborted) { resolve(); return; }
  const t = setTimeout(done, ms);
  function done() { clearTimeout(t); signal?.removeEventListener('abort', done); resolve(); }
  signal?.addEventListener('abort', done);
});

export interface PollOptions<T> {
  /** Stops the waiting (the dialog closed): the poller ends with null. */
  signal?: AbortSignal;
  /** Each read, the last one too. */
  onUpdate?: (s: T) => void;
  delays?: number[];
  /** Gives up after this long (null): 10 minutes. */
  maxWaitMs?: number;
  /** Reads that may fail in a row (a network hiccup) before the error is thrown. */
  retries?: number;
  sleep?: (ms: number, signal?: AbortSignal) => Promise<void>;
  now?: () => number;
}

/**
 * Reads a submission until its checks are no longer `pending` (`done` or `failed`, or a submission without checks: an older server):
 * the submission then, or null when stopped or given up. The first read is at once.
 */
export async function pollChecks<T extends { checks?: ChecksReport | null }>(get: () => Promise<T>, o: PollOptions<T> = {}): Promise<T | null> {
  const delays = o.delays ?? POLL_DELAYS;
  const sleep = o.sleep ?? pause;
  const now = o.now ?? Date.now;
  const until = now() + (o.maxWaitMs ?? 600_000);
  let failures = 0;
  for (let i = 0; ; i++) {
    if (o.signal?.aborted) return null;
    let s: T | null = null;
    try { s = await get(); failures = 0; } catch (e) { if (++failures > (o.retries ?? 3)) throw e; }
    if (o.signal?.aborted) return null;
    if (s) {
      o.onUpdate?.(s);
      if (!s.checks || s.checks.status !== 'pending') return s;
    }
    if (now() >= until) return null;
    await sleep(delays[Math.min(i, delays.length - 1)], o.signal);
  }
}

// ------------------------------------------------------------ boxes on a page

/** Points (pdf.js's viewport unit at scale 1) in a millimetre. */
export const PT_PER_MM = 72 / 25.4;

/** A box in mm from the page's top-left corner, ordered (x0 < x1, y0 < y1). */
const ordered = (b: Bbox): Bbox => [Math.min(b[0], b[2]), Math.min(b[1], b[3]), Math.max(b[0], b[2]), Math.max(b[1], b[3])];

/** A finding's box over a page rendered by pdf.js at `scale`, in CSS pixels from the page's top-left corner. */
export function bboxToPixels(bbox: Bbox, scale: number): { left: number; top: number; width: number; height: number } {
  const [x0, y0, x1, y1] = ordered(bbox);
  const k = PT_PER_MM * scale;
  return { left: x0 * k, top: y0 * k, width: (x1 - x0) * k, height: (y1 - y0) * k };
}

/** The same box as fractions of the page (any rendering, a DXF's outline too). */
export function bboxToFractions(bbox: Bbox, page: { width: number; height: number }): { left: number; top: number; width: number; height: number } {
  const [x0, y0, x1, y1] = ordered(bbox);
  return { left: x0 / page.width, top: y0 / page.height, width: (x1 - x0) / page.width, height: (y1 - y0) / page.height };
}

/** The scale that fits a page of `widthMm` into `pixels` CSS pixels across. */
export const fitScale = (widthMm: number, pixels: number) => (widthMm > 0 ? pixels / (widthMm * PT_PER_MM) : 1);

// ------------------------------------------------------------ the findings in words

/** A fact shown after a finding's sentence: its label is translated, its value is not, unless `translate`. */
export interface Fact { label: string; value: string; translate?: boolean }
export interface FindingWords { text: string; facts: Fact[] }

/** The default title block's fields in words (a project's own fields keep their names). */
export const FIELD_LABELS: Record<string, string> = {
  code: 'Drawing number', title: 'Drawing title', revision: 'Revision', date: 'Date', scale: 'Drawing scale', project: 'Project name',
  projectNumber: 'Project number', client: 'Client', drawnBy: 'Drawn by', checkedBy: 'Checked by',
};
export const fieldFact = (field: string): Fact => (FIELD_LABELS[field] ? { label: 'Field', value: FIELD_LABELS[field], translate: true } : { label: 'Field', value: field });

const str = (v: unknown): string | null => (v == null ? null : typeof v === 'string' ? v : typeof v === 'number' || typeof v === 'boolean' ? String(v) : JSON.stringify(v));
/** A similarity (0–1) as a percentage. */
export const percent = (v: unknown) => (typeof v === 'number' ? `${Math.round(v * 100)} %` : str(v) ?? '');

/** What each code says (Atrium's table § What is checked); a severity-dependent one has both. */
const TEXT: Record<string, string | { error: string; warning: string }> = {
  'R-01': 'The file could not be read, so nothing else in it was checked.',
  'R-02': 'The file is larger than 64 MiB and was not checked.',
  'R-03': 'The file has more than 300 pages: the first 300 were checked.',
  'V1-01': 'The code does not match the project\'s naming rule.',
  'V1-02': 'The code is not in the document register.',
  'V1-03': 'The revision in the file name is not the submission\'s.',
  'V1-04': 'The revision goes back: an issue tag already issued this document at a later revision.',
  'V1-05': { error: 'The revision is lower than the register expects.', warning: 'The revision is higher than the register expects.' },
  'V1-06': 'An issue tag already issued this document at the same revision with other content.',
  'V1-07': 'An issue tag already issued this document at the same revision with the same content.',
  'V1-08': 'The title block\'s drawing number differs from the file name\'s.',
  'V1-09': 'The title block\'s revision differs from the file\'s.',
  'V1-10': 'The title block\'s title differs from the register\'s.',
  'V2-01': 'The page has no text layer (a scan, or text drawn as curves): its title block cannot be read.',
  'V2-02': 'No title block was found on the page.',
  'V2-03': 'A required title-block field is missing.',
  'V2-04': { error: 'A title-block field does not show the expected value.', warning: 'A title-block field shows a value close to the expected one, but not the same.' },
  'V2-05': 'The revision field is not a revision (P01, C02…).',
  'V2-06': 'The scale field is not a scale (1:100, 1:50, NTS…).',
  'V2-07': 'The date field is not a date.',
  'V2-08': 'The date is in the future.',
  'V3-01': 'An empty sheet: next to nothing in the drawing area.',
  'V3-02': { error: 'Objects lie entirely off the page and the drawing area is empty: the content was shifted when it was printed.', warning: 'Some objects lie entirely off the page.' },
  'V4-01': 'A placeholder: the drawing area holds little more than a cross (an X).',
  'V5-01': 'The title block names another project.',
  'V5-02': 'The code\'s project field is not this project\'s code.',
  'V5-03': 'The title block\'s project number is not this project\'s.',
  'V5-04': 'A keyword of another project is on the page.',
};

/** The labels of the details members each code carries (Atrium's table), in the order shown. */
const DETAILS: Record<string, [string, string][]> = {
  'R-01': [['reason', 'Reason']],
  'V1-01': [['code', 'Code'], ['pattern', 'Naming rule']],
  'V1-02': [['code', 'Code']],
  'V1-03': [['revision', 'In the name'], ['submission', 'Submission']],
  'V1-04': [['revision', 'Revision'], ['issued', 'Issued'], ['tag', 'Issue tag']],
  'V1-05': [['revision', 'Revision'], ['expected', 'Expected']],
  'V1-06': [['tag', 'Issue tag'], ['sha256', 'SHA-256']],
  'V1-07': [['tag', 'Issue tag']],
  'V1-08': [['titleBlock', 'Title block'], ['name', 'File name']],
  'V1-09': [['titleBlock', 'Title block'], ['file', 'File']],
  'V3-01': [['objects', 'Objects'], ['span', 'Area covered']],
  'V3-02': [['offPage', 'Off the page']],
  'V5-04': [['keyword', 'Keyword'], ['inTitleBlock', 'In the title block']],
};
const SHARED: [string, string][] = [['expected', 'Expected'], ['similarity', 'Similarity'], ['code', 'Code'], ['revision', 'Revision']];

function factValue(key: string, v: unknown): Fact['value'] {
  if (key === 'similarity' || key === 'span') return percent(v);
  if (key === 'sha256' && typeof v === 'string') return v.slice(0, 12);
  if (key === 'tag' && typeof v === 'string') return v.replace(/^refs\/tags\/issue\//, '');
  return str(v) ?? '';
}

/**
 * A finding in words: its sentence (from its code and severity; an unknown code keeps the server's English) and its facts (the
 * field, then the details the code carries, then any other detail by its own name).
 */
export function describeFinding(f: Pick<Finding, 'code' | 'severity' | 'message' | 'field' | 'details'>): FindingWords {
  const t = TEXT[f.code];
  const text = t == null ? f.message : typeof t === 'string' ? t : f.severity === 'error' ? t.error : t.warning;
  const facts: Fact[] = [];
  if (f.field) facts.push(fieldFact(f.field));
  const d = f.details ?? {};
  const used = new Set<string>();
  for (const [key, label] of [...(DETAILS[f.code] ?? []), ...SHARED]) {
    if (!(key in d) || used.has(key) || d[key] == null) continue;
    used.add(key);
    if (key === 'inTitleBlock') { facts.push({ label, value: d[key] ? 'yes' : 'no', translate: true }); continue; }
    facts.push({ label, value: factValue(key, d[key]) });
  }
  for (const [key, v] of Object.entries(d)) if (!used.has(key) && v != null) facts.push({ label: key, value: factValue(key, v) });
  return { text, facts };
}

/** A fact as plain English (tests, a finding copied as text). */
export const factText = (x: Fact) => `${x.label}: ${x.value}`;

// ------------------------------------------------------------ grouping a file's findings

export const SEVERITIES: Severity[] = ['error', 'warning', 'info'];

/** A file's findings by severity (errors first), each list by page then code, with those on the whole file apart. */
export function findingsBySeverity(findings: Finding[]): Record<Severity, Finding[]> {
  const out: Record<Severity, Finding[]> = { error: [], warning: [], info: [] };
  const sorted = [...findings].sort((a, b) => (a.page ?? 0) - (b.page ?? 0) || a.code.localeCompare(b.code));
  for (const f of sorted) (out[f.severity] ?? out.info).push(f);
  return out;
}

/** The findings drawn over one page (those with a page and a box), and those listed beside it (the file's, and boxless ones). */
export function onPage(findings: Finding[], page: number): { drawn: Finding[]; listed: Finding[] } {
  const here = findings.filter((f) => f.page === page);
  return { drawn: here.filter((f) => f.bbox), listed: here.filter((f) => !f.bbox) };
}
export const fileLevel = (findings: Finding[]) => findings.filter((f) => f.page == null);

/** The counts of a report, or of the gate's checks. */
export const counts = (c: { summary?: CheckSummary | null } | GateChecks | null | undefined): CheckSummary | null =>
  !c ? null : 'errors' in c ? { errors: c.errors, warnings: c.warnings, infos: c.infos } : c.summary ?? null;

/** The counts a checks_done event carries (`new`, as JSON). */
export function checksDone(json: string | null): (CheckSummary & { run: number; status: string }) | null {
  if (!json) return null;
  try {
    const o = JSON.parse(json) as Partial<CheckSummary & { run: number; status: string }>;
    return { run: o.run ?? 0, status: o.status ?? 'done', errors: o.errors ?? 0, warnings: o.warnings ?? 0, infos: o.infos ?? 0 };
  } catch { return null; }
}

// ------------------------------------------------------------ the settings

export type FieldKind = 'text' | 'code' | 'title' | 'revision' | 'date' | 'scale' | 'project' | 'projectNumber';
export const FIELD_KINDS: FieldKind[] = ['text', 'code', 'title', 'revision', 'date', 'scale', 'project', 'projectNumber'];
export interface FieldSpec { name: string; kind: FieldKind; labels: string[]; required: boolean; zone: Bbox | null; attributes: string[] }
export type Anchor = 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left';
export interface TitleBlockSpec { region: { anchor: Anchor; width: number; height: number; marginX: number; marginY: number } | null; fields: FieldSpec[]; minLabels: number }
export interface CheckSettings {
  enabled: string[]; projectCode: string | null; projectNumber: string | null; projectName: string | null;
  foreignKeywords: string[]; expected: Record<string, string>; futureDays: number; titleBlock: TitleBlockSpec | null;
}
export const CHECKS = ['V1', 'V2', 'V3', 'V4', 'V5'];

/** The default title block (Atrium's table), the start of a project's own. */
export const DEFAULT_FIELDS: FieldSpec[] = [
  { name: 'code', kind: 'code', required: true, labels: ['DRAWING NO', 'DRAWING NUMBER', 'DWG NO', 'DOCUMENT NO', 'DOCUMENT NUMBER', 'DOC NO', 'COD PLANȘĂ', 'COD DOCUMENT', 'NR. PLANȘĂ'] },
  { name: 'title', kind: 'title', required: true, labels: ['DRAWING TITLE', 'TITLE', 'TITLU PLANȘĂ', 'TITLU', 'DENUMIRE PLANȘĂ'] },
  { name: 'revision', kind: 'revision', required: true, labels: ['REVISION', 'REV', 'REVIZIE', 'REVIZIA'] },
  { name: 'date', kind: 'date', required: true, labels: ['DATE', 'DATA'] },
  { name: 'scale', kind: 'scale', required: true, labels: ['SCALE', 'SCARA', 'SCARĂ'] },
  { name: 'project', kind: 'project', required: false, labels: ['PROJECT', 'PROJECT NAME', 'PROIECT', 'DENUMIRE PROIECT'] },
  { name: 'projectNumber', kind: 'projectNumber', required: false, labels: ['PROJECT NO', 'PROJECT NUMBER', 'JOB NO', 'NR. PROIECT', 'NR PROIECT'] },
  { name: 'client', kind: 'text', required: false, labels: ['CLIENT', 'BENEFICIAR'] },
  { name: 'drawnBy', kind: 'text', required: false, labels: ['DRAWN', 'DRAWN BY', 'DESENAT'] },
  { name: 'checkedBy', kind: 'text', required: false, labels: ['CHECKED', 'CHECKED BY', 'VERIFICAT'] },
].map((f) => ({ ...f, kind: f.kind as FieldKind, zone: null, attributes: [] }));

export const DEFAULT_SETTINGS: CheckSettings = {
  enabled: [...CHECKS], projectCode: null, projectNumber: null, projectName: null, foreignKeywords: [], expected: {}, futureDays: 0, titleBlock: null,
};

/** A list typed one per line or separated by commas or semicolons, trimmed, empty ones dropped, each once. */
export function splitList(text: string): string[] {
  const out: string[] = [];
  for (const x of text.split(/[\n;,]/)) { const t = x.trim(); if (t && !out.includes(t)) out.push(t); }
  return out;
}
/** Keywords: one per line (a keyword may hold a comma: "Caransebeș, km 12"). */
export const splitLines = (text: string) => [...new Set(text.split('\n').map((x) => x.trim()).filter(Boolean))];

/** Expected values typed as `field = value` lines. */
export function parseExpected(text: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const line of text.split('\n')) {
    const i = line.search(/[=:]/);
    if (i <= 0) continue;
    const k = line.slice(0, i).trim(), v = line.slice(i + 1).trim();
    if (k && v) out[k] = v;
  }
  return out;
}
export const expectedText = (e: Record<string, string>) => Object.entries(e).map(([k, v]) => `${k} = ${v}`).join('\n');

/**
 * The naming rule tried on a code as the server does (anchored, whole code), in the browser's own regex: .NET's syntax is mostly the
 * same; `(?<project>…)` names the project field. `error` when it does not compile here.
 */
export function tryNaming(pattern: string, code: string): { matches: boolean; project: string | null; error: string | null } {
  if (!pattern.trim()) return { matches: true, project: null, error: null };
  let re: RegExp;
  try { re = new RegExp(`^(?:${pattern.trim()})$`, 'u'); } catch (e) {
    try { re = new RegExp(`^(?:${pattern.trim()})$`); } catch { return { matches: false, project: null, error: e instanceof Error ? e.message : String(e) }; }
  }
  const m = re.exec(code.trim());
  return { matches: Boolean(m), project: m?.groups?.project ?? (m ? code.trim().split('-')[0] || null : null), error: null };
}

/**
 * Which setting a 400 `bad_request` of `PUT rules` is about, from the server's message (it names the member first: "naming does not
 * compile…", "checks.futureDays is 0 to 3650.", "Field date: zone is…", "A field's name is…"); null when it names none.
 */
export function badMember(message: string): { key: string; field?: string } | null {
  if (/^naming\b/.test(message)) return { key: 'naming' };
  const m = /^checks\.([A-Za-z]+(?:\.[A-Za-z]+)*)/.exec(message);
  if (m) {
    const path = m[1];
    if (path.startsWith('titleBlock.region')) return { key: 'titleBlock.region' };
    if (path.startsWith('titleBlock')) return { key: path === 'titleBlock.minLabels' ? 'titleBlock.minLabels' : 'titleBlock.fields' };
    return { key: path.split('.')[0] };
  }
  const f = /^(?:Field|Two fields are named) ([A-Za-z0-9_]+)/.exec(message);
  if (f) return { key: 'titleBlock.fields', field: f[1] };
  if (/^(A field's name|Every title-block field|Two fields are of kind)/.test(message)) return { key: 'titleBlock.fields' };
  if (/^checks is/.test(message)) return { key: 'checks' };
  return null;
}
