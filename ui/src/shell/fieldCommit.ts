/**
 * What a typed field commits, apart from React: the shared length and number
 * fields (`cells.tsx`) commit once, on blur or Enter, and only a value that
 * reads and differs from the current one. Empty text commits nothing (never
 * a 0) unless the field says blank means its default (null).
 */

import { parseLength } from '../engine/unitsStore';
import { parseAngle } from '../viewport/liveInput';

export interface CommitRules {
  /** Lowest value accepted; a smaller one is raised to it. */
  min?: number;
  /** Highest value accepted; a larger one is lowered to it. */
  max?: number;
  /** Empty text commits null (the default) instead of being ignored. */
  blank?: boolean;
  /** Rounded to a whole number (counts). */
  integer?: boolean;
}

/** No commit: the text is put back to the current value. */
export const NO_COMMIT = Symbol('no commit');

/**
 * The value a field's text commits: a number, null (blank means default), or
 * NO_COMMIT when it is empty without `blank`, does not read, or equals `current`.
 */
export function commitValue(text: string, current: number | null | undefined, parse: (t: string) => number | null, rules: CommitRules = {}): number | null | typeof NO_COMMIT {
  if (!text.trim()) return rules.blank && current != null ? null : NO_COMMIT;
  let v = parse(text);
  if (v == null || !Number.isFinite(v)) return NO_COMMIT;
  if (rules.integer) v = Math.round(v);
  if (rules.min != null && v < rules.min) v = rules.min;
  if (rules.max != null && v > rules.max) v = rules.max;
  if (current != null && Math.abs(v - current) <= 1e-9 * Math.max(1, Math.abs(current))) return NO_COMMIT;
  return v;
}

/** A typed length (metres) in the project's unit or its own. */
export const commitLength = (text: string, current: number | null | undefined, rules?: CommitRules) => commitValue(text, current, (t) => parseLength(t), rules);

/** A typed number: sums and differences, a decimal comma allowed. */
export const commitNumber = (text: string, current: number | null | undefined, rules?: CommitRules) => commitValue(text, current, parseAngle, rules);
