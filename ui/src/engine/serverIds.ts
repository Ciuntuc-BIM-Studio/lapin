/**
 * IDS on the CDE server (Atrium docs/plan-cde-web.md W6c; the server's contract in its docs/atrium-server.md § IDS on the server):
 * the project's IDS files (owners put and delete them, members read), the dry run on a commit, the `ids` rule, and the `ids` group
 * of a submission's checks said from its codes and details. The screens are modules/core/CdeIds.tsx, fetched when first shown.
 */

import { need, putFile, type Person } from './atrium';
import type { Fact, Finding, FindingWords } from './checks';

export interface IdsFileInfo {
  name: string; sha256: string; size: number; title: string | null; version: string | null; author: string | null; date: string | null;
  specifications: number; ifcVersions: string[]; uploaded: string; uploadedBy: Person | null;
}
export interface IdsFacet { kind: string; text: string; cardinality: string; instructions?: string }
export interface IdsSpecView { index: number; name: string; identifier?: string | null; description?: string | null; ifcVersion: string[]; usage: string; applicability: IdsFacet[]; requirements: IdsFacet[] }
export type IdsStatus = 'pass' | 'fail' | 'skipped';
export interface IdsSpecResult { index: number; name: string; identifier: string | null; usage: string; status: IdsStatus; applicable: number; passed: number; failed: number }
export interface IdsResult { ids: string; model: 'viewer' | 'file'; name: string | null; specifications: IdsSpecResult[] }
export interface IdsFailure { globalId: string; class: string; name: string | null; requirement: string; actual: string; expected: string }
/** A submission's `checks.ids` (null when the rule is off or the project has no IDS file), and a dry run's `ids`. */
export interface IdsReport {
  enabled: boolean; blocking: boolean; files: { name: string; title: string | null; specifications: number }[];
  results: IdsResult[]; summary: { specifications: number; passed: number; failed: number; skipped: number }; findings: Finding[];
}

const I = (project: string) => `atrium/1.0/projects/${encodeURIComponent(project)}/ids`;
export const idsPath = (project: string, name: string) => `${I(project)}/${encodeURIComponent(name)}`;

export const listIds = (server: string, project: string) => need<{ settings: { enabled: boolean; blocking: boolean }; files: IdsFileInfo[] }>(server, 'GET', I(project));
/** An IDS file put as it is (owners): new, or replacing the one of that name; a file that is not IDS 1.0 comes back as 400 `bad_ids` with its `problems`. */
export const putIds = (server: string, project: string, name: string, bytes: Uint8Array) => putFile<IdsFileInfo>(server, idsPath(project, name), bytes, 'application/xml');
export const deleteIds = (server: string, project: string, name: string) => need<unknown>(server, 'DELETE', idsPath(project, name));
export const viewIds = (server: string, project: string, name: string) => need<{ specifications: IdsSpecView[] }>(server, 'GET', `${idsPath(project, name)}?view=json`);
/** The dry run on a commit (editors and owners): nothing stored; severities as the project's `blocking` says. */
export const dryRunIds = (server: string, project: string, commit: string) => need<{ commit: string; ids: IdsReport }>(server, 'POST', `${I(project)}/run?commit=${encodeURIComponent(commit)}`);

/** A 400 `bad_ids`'s problems, each with its line when it names one ("Line 12: …"). */
export const problemLines = (problems: unknown): { line: number | null; text: string }[] =>
  (Array.isArray(problems) ? problems : []).map((p) => {
    const m = /^Line (\d+): ([\s\S]*)$/.exec(String(p));
    return m ? { line: Number(m[1]), text: m[2] } : { line: null, text: String(p) };
  });

// ------------------------------------------------------------ the report in words

/** What each code says (the server's table); the server's English only for a code not here. */
const TEXT: Record<string, string> = {
  'IDS-02': 'The commit has no IFC copy on the server: IDS did not run on the model.',
  'IDS-03': 'A model could not be read: it was not checked.',
  'IDS-04': 'A model is larger than the server allows: it was not checked.',
  'IDS-05': 'The time limit was reached: the rest was not checked.',
  'IDS-06': 'A stored IDS file no longer reads: it was not run.',
};

const mb = (n: unknown) => (typeof n === 'number' ? `${Math.round(n / 1048576)} MB` : String(n));

/** A model as the report names it: the commit's IFC copy, or the submitted file's name. */
export const modelFact = (d: { model?: unknown; name?: unknown }): Fact =>
  (d.model === 'viewer' ? { label: 'Model', value: 'IFC copy of the commit', translate: true } : { label: 'Model', value: String(d.name ?? '') });

/** An IDS finding in words: its sentence from its code, its facts from its details (values as the server gives them). */
export function describeIds(f: Pick<Finding, 'code' | 'message' | 'details'>): FindingWords {
  const d = (f.details ?? {}) as Record<string, unknown>;
  const facts: Fact[] = [];
  if (d.ids != null) facts.push({ label: 'IDS file', value: String(d.ids) });
  if (d.model != null) facts.push(modelFact(d));
  if (f.code === 'IDS-02' && d.commit != null) facts.push({ label: 'Commit', value: String(d.commit).slice(0, 8) });
  if (f.code === 'IDS-03' && d.reason != null) facts.push({ label: 'Reason', value: String(d.reason) });
  if (f.code === 'IDS-04') facts.push({ label: 'Size', value: `${mb(d.size)} / ${mb(d.limit)}` });
  if (f.code === 'IDS-05') facts.push({ label: 'Checked', value: `${d.checked} / ${d.total} · ${d.seconds} s` });
  if (f.code === 'IDS-06' && Array.isArray(d.problems)) facts.push({ label: 'Problems', value: d.problems.join(' · ') });
  return { text: TEXT[f.code] ?? f.message, facts };
}

/** IDS-01's details: the failures told in full, the failing GlobalIds (the first 50 of `globalIdsTotal`), what is not listed. */
export interface IdsFailDetails { requirements: string[]; reason: string | null; failures: IdsFailure[]; globalIds: string[]; globalIdsTotal: number; more: number }

const sameModel = (d: Record<string, unknown>, r: IdsResult) => d.ids === r.ids && d.model === r.model && (d.name ?? null) === (r.name ?? null);

/**
 * The report by IDS file and model: each specification with its IDS-01 details when it fails; and the other findings (IDS-02–06:
 * no IFC copy, a model not read or too large, the time limit, a file that no longer reads) apart, as notices.
 */
export function idsGroups(r: IdsReport): { results: (IdsResult & { specs: (IdsSpecResult & { fail: (IdsFailDetails & { finding: Finding }) | null })[] })[]; notices: Finding[] } {
  const fails = r.findings.filter((f) => f.code === 'IDS-01');
  return {
    results: r.results.map((res) => ({
      ...res,
      specs: res.specifications.map((s) => {
        const f = fails.find((x) => { const d = (x.details ?? {}) as Record<string, unknown>; return sameModel(d, res) && (d.specification as { index?: number } | undefined)?.index === s.index; });
        return { ...s, fail: f ? { ...(f.details as unknown as IdsFailDetails), finding: f } : null };
      }),
    })),
    notices: r.findings.filter((f) => f.code !== 'IDS-01'),
  };
}
