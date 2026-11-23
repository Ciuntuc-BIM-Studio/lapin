/**
 * An array along a path stands its copies as the original does, on the ground under them, or on the path's own height
 * (docs/plan-site-grading.md, T6); `rise` lifts them above that. What the dialog and Properties send to element.array and
 * array.update.
 */

export type PathHeight = 'none' | 'ground' | 'line';

/** The rule's `level` and `rise` from a dialog's answers: none leaves the copies as the original (level null); a rise counts only on the ground or the line. */
export function heightArgs(level: string, rise: number): { level: 'ground' | 'line' | null; rise: number } {
  const l = level === 'ground' || level === 'line' ? level : null;
  return { level: l, rise: l && Number.isFinite(rise) ? rise : 0 };
}

/** The level a rule has now, as the select shows it. */
export const heightOf = (level: unknown): PathHeight => (level === 'ground' || level === 'line' ? level : 'none');
