/**
 * Clash detection's data in the interface (docs/plan-visualisation.md, stage 2): the search sets and tests the engine
 * keeps (clash.list), a test's clashes (clash.results), the commands that change them, and how a clash is shown: the
 * pair selected, a section box round where they meet and the camera looking at it (BimGo's framing, MIT-0).
 */

import { execute, query } from '../../engine/session';

export type RuleOp = 'is' | 'isOneOf' | 'isNot' | 'isNotAnyOf' | 'contains' | 'notContains' | 'atLeast' | 'atMost' | 'between';
export type RuleField = 'class' | 'predefinedType' | 'type' | 'storey' | 'material' | 'name' | 'link' | 'parameter';
export interface SetRule { field: RuleField; op: RuleOp; values: string[]; parameter?: string }
export interface RuleGroup { matchAll: boolean; rules: SetRule[]; groups: RuleGroup[] }
export interface SearchSet extends RuleGroup { id: string; name: string; count?: number }
export type ClashStatus = 'new' | 'active' | 'reviewed' | 'approved' | 'resolved';
export const STATUSES: ClashStatus[] = ['new', 'active', 'reviewed', 'approved', 'resolved'];
export const STATUS_LABELS: Record<ClashStatus, string> = { new: 'New', active: 'Active', reviewed: 'Reviewed', approved: 'Approved', resolved: 'Resolved' };
export interface RunSummary { date: string; found: number; new: number; reopened: number; resolved: number; countA: number; countB: number; pairs: number; truncated: boolean; seconds: number; id?: string; name?: string }
export interface ClashTest {
  id: string; name: string; kind: 'hard' | 'clearance'; setA: string; setB: string | null; tolerance: number; enabled: boolean;
  counts: Record<ClashStatus, number>; lastRun: RunSummary | null; stale?: boolean;
}
export interface Clash {
  n: number; key: string; a: string; b: string; at: [number, number, number]; d: number; crossing: boolean; status: ClashStatus;
  found: string; seen: string; resolved?: string; assignedTo?: string; notes?: string; remark?: string;
  aName?: string | null; aClass?: string | null; aStorey?: string | null; aLink?: string | null;
  bName?: string | null; bClass?: string | null; bStorey?: string | null; bLink?: string | null;
}
export interface ClashList { sets: SearchSet[]; tests: ClashTest[]; fields: RuleField[]; ops: RuleOp[] }

export const FIELD_LABELS: Record<RuleField, string> = {
  class: 'Class', predefinedType: 'Predefined type', type: 'Type', storey: 'Storey', material: 'Material', name: 'Name', link: 'Linked file', parameter: 'Parameter',
};
export const OP_LABELS: Record<RuleOp, string> = {
  is: 'is', isOneOf: 'is one of', isNot: 'is not', isNotAnyOf: 'is not any of', contains: 'contains', notContains: 'does not contain', atLeast: 'at least', atMost: 'at most', between: 'between',
};
/** Operators that take several values (one per line or comma). */
export const isList = (op: RuleOp) => op === 'isOneOf' || op === 'isNotAnyOf' || op === 'between';

export const loadClashes = () => query<ClashList>('clash.list');
export const loadResults = (id: string) => query<{ test: ClashTest; clashes: Clash[] }>('clash.results', { id });
export const loadMembers = (set: RuleGroup) => query<{ count: number; ids: string[] }>('clash.members', { set });
export const loadValues = (field: RuleField, parameter?: string) => query<{ value: string; count: number; label: string }[]>('clash.values', { field, parameter });
export const seedClashes = () => execute('clash.seed', {});
export const runClashes = (ids?: string[]) => execute('clash.run', ids ? { ids } : {}) as Promise<RunSummary[] | null>;
export const saveSet = (set: Partial<SearchSet>) => execute('clash.setSet', set) as Promise<{ id: string } | null>;
export const deleteSet = (id: string) => execute('clash.deleteSet', { id });
export const saveTest = (test: Partial<ClashTest>) => execute('clash.setTest', test) as Promise<{ id: string } | null>;
export const deleteTest = (id: string) => execute('clash.deleteTest', { id });
export const setStatus = (id: string, numbers: number[], patch: { status?: ClashStatus; assignedTo?: string; notes?: string }) => execute('clash.setStatus', { id, numbers, ...patch });
export const clashRemarks = (id: string, numbers: number[] | undefined, author: string) => execute('clash.remarks', { id, numbers, author }) as Promise<{ added: number; updated: number; ids: string[] } | null>;

/** Values typed in a rule's box: one per line or comma for a list ("600, 800"), the text whole otherwise. */
export function valuesOf(text: string, op: RuleOp): string[] {
  if (!isList(op)) return [text];
  return text.split(/[\n;]|,(?!\d{3}\b)/).map((s) => s.trim()).filter((s) => s.length > 0);
}

export const valuesText = (r: SetRule) => (isList(r.op) ? r.values.join(', ') : r.values[0] ?? '');

type Vec = [number, number, number];
export interface Box { min: Vec; max: Vec }
export interface Framing { box: { min: Vec; max: Vec }; target: { x: number; y: number; z: number }; azimuth: number; pitch: number; distance: number }

/**
 * Where to look at a clash (after BimGo's ClashFraming): a section box round where the two boxes meet (or the gap
 * between them), 1 m more on every side and at least 2 m along each axis; the camera 20° down, far enough to see the
 * box, from the one of eight directions that sees both elements most from the side (a wall and a pipe through it: at
 * 45° to both; along a duct you see nothing), the turn from where it looks now kept small.
 */
export function framing(at: Vec, a: Box | null, b: Box | null, azimuthNow = 0): Framing {
  const MARGIN = 1, MIN = 2, PITCH = -0.349;
  let min: Vec = [at[0], at[1], at[2]], max: Vec = [at[0], at[1], at[2]];
  if (a && b) {
    const lo: Vec = [0, 1, 2].map((k) => Math.max(a.min[k], b.min[k])) as Vec;
    const hi: Vec = [0, 1, 2].map((k) => Math.min(a.max[k], b.max[k])) as Vec;
    // Apart (clearance): the gap between them.
    min = [0, 1, 2].map((k) => Math.min(lo[k], hi[k])) as Vec;
    max = [0, 1, 2].map((k) => Math.max(lo[k], hi[k])) as Vec;
  }
  for (let k = 0; k < 3; k++) {
    min[k] -= MARGIN; max[k] += MARGIN;
    const short = MIN - (max[k] - min[k]);
    if (short > 0) { min[k] -= short / 2; max[k] += short / 2; }
  }
  // Each element's run in plan, when it has one (longer one way than the other).
  const runs: number[] = [];
  for (const x of [a, b]) {
    if (!x) continue;
    const dx = x.max[0] - x.min[0], dy = x.max[1] - x.min[1];
    if (Math.max(dx, dy) > 1.5 * Math.min(dx, dy)) runs.push(dx >= dy ? 0 : Math.PI / 2);
  }
  const turn = (y: number) => Math.abs(Math.atan2(Math.sin(y - azimuthNow), Math.cos(y - azimuthNow)));
  let best = azimuthNow, score = -Infinity;
  for (let k = 0; k < 8; k++) {
    const yaw = (k * Math.PI) / 4 - Math.PI;
    const side = runs.length ? Math.min(...runs.map((r) => Math.abs(Math.sin(yaw - r)))) : 1;
    const s = side - 0.1 * turn(yaw);
    if (s > score) { score = s; best = yaw; }
  }
  const size = Math.hypot(max[0] - min[0], max[1] - min[1], max[2] - min[2]);
  return { box: { min, max }, target: { x: at[0], y: at[1], z: at[2] }, azimuth: best, pitch: PITCH, distance: Math.max(2.5, size * 1.1) };
}

/** A test's clashes as a CSV list (comma separated, quoted, UTF-8 with its mark). */
export function clashCsv(test: ClashTest, clashes: Clash[]): string {
  const q = (s: string | null | undefined) => (s == null ? '' : /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s);
  const rows = [['No.', 'Status', 'Element A', 'Class A', 'Storey A', 'Element B', 'Class B', 'Storey B', test.kind === 'hard' ? 'Depth (mm)' : 'Distance (mm)', 'Crossing', 'X', 'Y', 'Z', 'Assigned to', 'Notes', 'Found', 'Resolved'].join(',')];
  for (const c of clashes)
    rows.push([c.n, c.status, q(c.aName), q(c.aClass), q(c.aStorey), q(c.bName), q(c.bClass), q(c.bStorey), Math.round(c.d * 1000), c.crossing ? 'yes' : 'no',
      c.at[0].toFixed(3), c.at[1].toFixed(3), c.at[2].toFixed(3), q(c.assignedTo), q(c.notes), c.found, c.resolved ?? ''].join(','));
  return '﻿' + rows.join('\n') + '\n';
}
