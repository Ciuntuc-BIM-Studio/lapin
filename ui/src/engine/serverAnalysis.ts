/**
 * The analyses a CDE server runs with Lapin's engine (docs/headless-run.md; Atrium docs/atrium-server.md § Clash detection on the
 * server and § The sun study on the server): a commit's clash report (the project's clash tests, run on each submission and as a dry
 * run), and the sun study when the server offers it. Only what the server answers: the screens are modules/core/CdeClash.tsx and
 * CdeSunStudy.tsx, fetched when first shown.
 */

import { need, serverBytes, type Person } from './atrium';
import type { Finding } from './checks';

const P = (project: string) => `atrium/1.0/projects/${encodeURIComponent(project)}`;

export type ClashStatus = 'new' | 'active' | 'reviewed' | 'approved';
export interface ClashElement { globalId: string; name: string | null; class: string | null; storey: string | null; link: string | null }
export interface ServerClash {
  test: string; testName: string; kind: string; number: number; key: string; status: ClashStatus; since: 'new' | 'seen';
  a: ClashElement; b: ClashElement; at: [number, number, number] | null; distance: number; crossing: boolean;
}
export interface ClashTestRow {
  id: string; name: string; kind: string; tolerance: number; enabled: boolean; ran: boolean;
  open?: number; new?: number; resolved?: number; newHard?: number; found?: number; truncated?: boolean;
}
export interface ClashSummary { tests: number; open: number; byStatus: Record<ClashStatus, number>; new: number; resolved: number; newHard: number; firstRun?: boolean }
/** A submission's `checks.clash` (without the clashes) or a whole report (`GET clash`). */
export interface ClashReport {
  commit: string; source?: 'submission' | 'dry'; ran?: string; ranBy?: Person; status: 'done' | 'failed'; error: string | null;
  previous: { commit: string; ran: string } | null; settings?: { enabled: boolean; blocking: boolean; maxNew: number };
  tests: ClashTestRow[]; summary: ClashSummary | null; findings: Finding[];
  clashes?: ServerClash[]; clashesTotal?: number; resolvedSincePrevious?: { test: string; testName: string; number: number; key: string }[]; resolvedTotal?: number;
}

/** The newest clash report of a commit (or of the project): its clashes with their elements. */
export const clashReport = (server: string, project: string, commit?: string) =>
  need<ClashReport>(server, 'GET', `${P(project)}/clash${commit ? `?commit=${commit}` : ''}`);
/** The dry run on a commit (editors and owners): the project's clash tests run now; stored as the commit's dry run. */
export const runClash = (server: string, project: string, commit: string) => need<ClashReport>(server, 'POST', `${P(project)}/clash/run?commit=${commit}`);

/** A report's clashes in the order to read them: the new ones first, then by test and number. */
export const clashOrder = (list: ServerClash[]) =>
  [...list].sort((x, y) => (x.since === y.since ? 0 : x.since === 'new' ? -1 : 1) || x.testName.localeCompare(y.testName) || x.number - y.number);

/** How deep (crossing) or how far (a clearance) a clash is, in millimetres. */
export const clashDistance = (c: Pick<ServerClash, 'distance'>) => `${Math.round(c.distance * 1000)} mm`;

// ------------------------------------------------------------ the sun study (premium, where the server offers sunStudy)

export interface SunHour { hour: number; label: string; azimuth: number | null; altitude: number | null; up: boolean; area: number; loops?: [number, number][][] }
export interface SunStudy {
  id: string; commit: string; params: { date: string; times: string; caster: string; utcOffset: number | null; summerTime: boolean | null };
  status: 'done' | 'failed'; error: string | null; place: { lon: number; lat: number; known: boolean } | null; hours: SunHour[];
  ran: string; ranBy?: Person; picture?: boolean;
}

export const sunStudies = async (server: string, project: string) => (await need<{ studies: SunStudy[] }>(server, 'GET', `${P(project)}/sun-study`)).studies;
/** A study run now (licensed editors and owners), on `commit` or main's latest. */
export const runSunStudy = (server: string, project: string, ask: { date: string; times: string }, commit?: string) =>
  need<SunStudy>(server, 'POST', `${P(project)}/sun-study/run${commit ? `?commit=${commit}` : ''}`, ask);
/** A study's picture (SVG) as a URL the page shows. */
export async function sunPicture(server: string, project: string, id: string): Promise<string> {
  const bytes = await serverBytes(server, `${P(project)}/sun-study/${encodeURIComponent(id)}/picture`);
  return URL.createObjectURL(new Blob([bytes as BlobPart], { type: 'image/svg+xml' }));
}

/** The next winter solstice (21 December), the day a permit's sun study is usually asked for. */
export function nextSolstice(now = new Date()): string {
  const year = now.getMonth() === 11 && now.getDate() > 21 ? now.getFullYear() + 1 : now.getFullYear();
  return `${year}-12-21`;
}
