/**
 * Levels relative or absolute (decision 867, docs/plan-level-datums.md): the project's datum (relative to ±0.00, altitudes, or
 * both), the storey ±0.00 is at, the tie „±0.00 = 650.00” (it writes the base point's elevation; the model does not move), a
 * view's own over the project's, a sheet setting every view placed on it. The engine writes every level; this is what the
 * dialogs read and send (`project.levelDatum`, `view.levelDatum`).
 */

export type LevelMode = 'relative' | 'absolute' | 'both';

export interface LevelDatumDto {
  mode: LevelMode;
  zeroStorey: string | null;
  zero: number;
  altitude: number;
  /** The altitude of ±0.00 (zero + altitude). */
  zeroAltitude: number;
  tie: string;
  terrains: { id: string; name: string; datum: number }[];
}

/** The modes a project offers, as the dialog names them. */
export const LEVEL_MODES: [LevelMode, string][] = [
  ['relative', 'Relative to ±0.00'],
  ['absolute', 'Altitudes (absolute)'],
  ['both', 'Both: +0.00 (+650.00)'],
];

/** A view's (or a sheet's, a level dimension's) choices: its owner's first. */
export const VIEW_LEVEL_MODES: [string, string][] = [
  ['relative', 'relative to ±0.00'],
  ['absolute', 'absolute (altitudes)'],
  ['both', 'both'],
];

/** What `project.levelDatum` is sent for a change of the dialog. */
export function levelDatumPatch(change: { mode: LevelMode } | { zeroStorey: string | null } | { tie: number } | { fromTerrain: string }): Record<string, unknown> {
  if ('mode' in change) return { mode: change.mode === 'relative' ? null : change.mode };
  if ('zeroStorey' in change) return { zeroStorey: change.zeroStorey || null };
  return change;
}

/** A view's own datum as its select shows it: '' for the project's. */
export function viewDatumChoice(value: unknown): string {
  return typeof value === 'string' && VIEW_LEVEL_MODES.some(([k]) => k === value) ? value : '';
}

/** What `view.levelDatum` is sent: a view's ({id}) or every view on a sheet ({sheet}); '' back to the project's. */
export function viewDatumPatch(target: { id: string } | { sheet: string }, choice: string): Record<string, unknown> {
  return { ...target, datum: choice || 'project' };
}

/**
 * The spot levels' own datum as a terrain's select shows it: the one they all say, else '' (the view's; also when they
 * differ). Each spot is stored as [x, y] or [x, y, datum].
 */
export function spotDatumChoice(spots: unknown): string {
  if (!Array.isArray(spots) || spots.length === 0) return '';
  const own = spots.map((s) => (Array.isArray(s) ? viewDatumChoice(s[2]) : ''));
  return own.every((d) => d === own[0]) ? own[0] : '';
}

/** What `civil.setSpotDatum` is sent for every spot level of a terrain: '' back to the view's. */
export function spotDatumPatch(terrain: string, choice: string): Record<string, unknown> {
  return { id: terrain, all: true, datum: choice || null };
}
