/**
 * Hatch units (docs/plan-material-graphics.md, stage 1): a pattern is drawn in paper millimetres (drafting: its spacing is the
 * same on every sheet) or in model millimetres (real size: a brick course is 75 mm of the wall whatever the scale). Pickers
 * group their patterns by it; the badge and the scale's label follow it.
 */

import { query } from '../../engine/session';

export type HatchUnits = 'paper' | 'model';
/** A pattern as hatch.list gives it. */
export type PatternInfo = { name: string; units?: HatchUnits; description?: string; solid?: boolean; project?: boolean };

export const unitsOf = (p: { units?: HatchUnits } | undefined): HatchUnits => (p?.units === 'model' ? 'model' : 'paper');

/** The units' names as the pickers show them. */
export const UNITS_LABEL: Record<HatchUnits, string> = { paper: 'Drafting', model: 'Real size' };
export const UNITS_HELP: Record<HatchUnits, string> = {
  paper: 'Drafting pattern: its spacing is paper millimetres, the same on every scale',
  model: 'Real-size pattern: its spacing is the real object\'s millimetres (brick courses, tiles, boards), so it follows the view\'s scale',
};

/** Patterns split by units, each group in the order given; a group left out when empty. */
export function groupByUnits<T extends { units?: HatchUnits }>(list: readonly T[]): { units: HatchUnits; items: T[] }[] {
  const model = list.filter((p) => unitsOf(p) === 'model');
  const paper = list.filter((p) => unitsOf(p) !== 'model');
  return [...(paper.length ? [{ units: 'paper' as const, items: paper }] : []), ...(model.length ? [{ units: 'model' as const, items: model }] : [])];
}

/** The scale's label by units: "×" a factor on the paper spacing; a real-size pattern's 1 is its real size. */
export function scaleLabel(units: HatchUnits): string { return units === 'model' ? 'Scale (1 = real size)' : 'Scale (×)'; }

/** The units a graphics' pattern is drawn in: the material's own choice, else the pattern's. */
export function effectiveUnits(own: HatchUnits | undefined, pattern: PatternInfo | undefined): HatchUnits { return own ?? unitsOf(pattern); }

/** Pattern names as <option>s grouped Drafting / Real size (one list when all are of one kind). */
export function PatternOptions({ patterns, skip, current }: { patterns: readonly PatternInfo[]; skip?: (p: PatternInfo) => boolean; current?: string }) {
  const list = patterns.filter((p) => !skip?.(p));
  const withCurrent = current && !list.some((p) => p.name === current) ? [{ name: current } as PatternInfo, ...list] : list;
  const groups = groupByUnits(withCurrent);
  const opt = (p: PatternInfo) => <option key={p.name} value={p.name}>{p.name}{p.project ? ' (project)' : ''}</option>;
  if (groups.length < 2) return <>{withCurrent.map(opt)}</>;
  return <>{groups.map((g) => <optgroup key={g.units} label={UNITS_LABEL[g.units]}>{g.items.map(opt)}</optgroup>)}</>;
}

/** A small badge for real-size patterns. */
export function UnitsBadge({ units }: { units: HatchUnits }) {
  return units === 'model' ? <span className="badge units-badge" title={UNITS_HELP.model} data-units="model">Real size</span> : null;
}

/** hatch.list, once per call. */
export const listPatterns = () => query<PatternInfo[]>('hatch.list');
