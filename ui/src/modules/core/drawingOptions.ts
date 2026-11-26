/**
 * Two drafting standards options (follow-ups of 23.11.2026): the smallest scale floors' slope marks (fall arrows, spot levels,
 * drains' levels) are drawn at, 1:50 by default (docs/plan-floor-slopes.md Q8), and the language of the words drawings write by
 * themselves (a channel's IL, a low point), English by default.
 */

/** Floors' slope marks drawn at 1:50 and larger unless the standards say otherwise (the engine's default). */
export const DEFAULT_FLOOR_SLOPE_MARKS = 50;

/** The choices offered: each a scale's denominator and its label, the project's own value added when it is none of them. */
export function floorSlopeMarkChoices(current?: number | null): { value: number; label: string }[] {
  const o = [20, 50, 100, 200].map((v) => ({ value: v, label: `at 1:${v} and larger` }));
  o.push({ value: 100000, label: 'at every scale' });
  if (current != null && !o.some((c) => c.value === current)) {
    o.push({ value: current, label: `at 1:${current} and larger` });
    o.sort((a, b) => a.value - b.value);
  }
  return o;
}

/** What standards.options is given for a choice: null for the default (so the project keeps no value of its own). */
export const floorSlopeMarksArg = (value: number): number | null => (value === DEFAULT_FLOOR_SLOPE_MARKS ? null : value);

/** What standards.options is given for a drawing language: null for English (as drawings were written before). */
export const drawingLangArg = (lang: string): string | null => (lang === 'en' ? null : lang);

/**
 * Whether a door's clear height ({clearHeight}) takes its threshold off (forum topic 204): ticked by default, as Lapin wrote it;
 * the Czech rule unticks it (the void less the frame only, 2050 − 50 = 2000). What standards.options is given: null for the default.
 */
export const clearHeightLessThreshold = (stored?: boolean | null): boolean => stored !== false;
export const clearHeightLessThresholdArg = (ticked: boolean): false | null => (ticked ? null : false);
