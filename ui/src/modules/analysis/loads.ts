/** Structural loads (docs/plan-fem.md §8): the pure parts of the interface: command arguments, factor names, the engine's reports. */

export const ACTION_TYPES = ['PERMANENT_G', 'VARIABLE_Q', 'EXTRAORDINARY_A'] as const;
export const ACTION_SOURCES = ['DEAD_LOAD_G', 'COMPLETION_G1', 'LIVE_LOAD_Q', 'SNOW_S', 'WIND_W', 'EARTHQUAKE_E', 'USERDEFINED'] as const;
export const ACTION_TYPE_LABEL: Record<string, string> = { PERMANENT_G: 'Permanent', VARIABLE_Q: 'Variable', EXTRAORDINARY_A: 'Accidental' };
export const ACTION_SOURCE_LABEL: Record<string, string> = {
  DEAD_LOAD_G: 'Self weight / dead load', COMPLETION_G1: 'Completion (finishes)', LIVE_LOAD_Q: 'Imposed', SNOW_S: 'Snow', WIND_W: 'Wind', EARTHQUAKE_E: 'Earthquake', USERDEFINED: 'User defined',
};
export const DERIVES = ['finishes', 'partitions', 'occupancy', 'snow', 'wind'] as const;

export type Psi = Record<'0' | '1' | '2', number>;

export interface LoadCaseDto {
  id: string; code: string; name: string; actionType: string; actionSource: string; category?: string | null;
  selfWeight: boolean; derives: string[]; psi?: Psi | null; userLoads: number; total: number; empty: boolean;
  /** A wind case's direction (+x, -x, +y, -y), a snow case's drift; a wind case's roof coefficients (min: suction, max: pressure). */
  windDirection?: string | null; roofCpe?: string | null;
}
export interface DerivedLoadDto { kind: string; caseId: string; sourceId: string; sourceName: string; storeyId: string; value: number; unit: string; total: number }
export interface LoadsDto {
  cases: LoadCaseDto[];
  storeys: { id: string; name: string; byCase: Record<string, number> }[];
  derived: DerivedLoadDto[];
  missing: { category: string; rooms: number }[];
  notes: string[];
}
export interface CategoryDto { key: string; label: string; qk: number; default: number; overridden: boolean }
export interface CombinationDto { key: string; kind: 'uls' | 'sls-characteristic' | 'sls-frequent' | 'sls-quasi'; name: string; factors: { caseId: string; code: string; factor: number }[] }
export interface CombinationsDto { combinations: CombinationDto[]; notes: string[] }

/** A number typed (a comma allowed) that is more than nothing; null otherwise. */
export function parsePositive(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'));
  return Number.isFinite(n) && n > 0 ? n : null;
}

/** A number typed, any sign; null when it does not read. */
export function parseNumber(text: string): number | null {
  const t = text.trim().replace(',', '.');
  if (!t) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** A factor as the combinations write it: at most three decimals, no trailing zeros (1.35, 1.5, 0.9). */
export const formatFactor = (f: number): string => String(Math.round(f * 1000) / 1000);

/** A load or total in kN (or kN/m², kN/m): up to two decimals. */
export const formatKn = (v: number): string => (Math.round(v * 100) / 100).toLocaleString('en-US', { maximumFractionDigits: 2, minimumFractionDigits: 0 }).replace(/,/g, '');

export const combinationName = (c: CombinationDto): string => c.name || c.factors.map((f) => `${formatFactor(f.factor)} ${f.code}`).join(' + ');

/** The arguments of the three load commands, `caseId` left out when none is chosen (the engine then takes Q-A). */
export interface Pt { x: number; y: number }
const withCase = (caseId: string | null) => (caseId ? { caseId } : {});
export const areaLoadArgs = (storeyId: string, points: Pt[], value: number, caseId: string | null) => ({ storeyId, points: points.map((p) => [p.x, p.y]), value, ...withCase(caseId) });
export const lineLoadArgs = (storeyId: string, a: Pt, b: Pt, value: number, caseId: string | null) => ({ storeyId, a: [a.x, a.y], b: [b.x, b.y], value, ...withCase(caseId) });
export const pointLoadArgs = (storeyId: string, at: Pt, value: number, caseId: string | null) => ({ storeyId, at: [at.x, at.y], value, ...withCase(caseId) });

/** The psi factors typed (three fields): null when all are empty (the table's default), undefined when one does not read. */
export function psiFrom(t0: string, t1: string, t2: string): Psi | null | undefined {
  if (![t0, t1, t2].some((t) => t.trim())) return null;
  const v = [t0, t1, t2].map(parseNumber);
  if (v.some((x) => x == null || x < 0 || x > 1)) return undefined;
  return { '0': v[0]!, '1': v[1]!, '2': v[2]! };
}

/** The arguments of analysis.setImposedLoad: a qk typed, empty for the default again (null). Undefined when it does not read. */
export function imposedArgs(category: string, text: string): { category: string; qk: number | null } | undefined {
  if (!text.trim()) return { category, qk: null };
  const q = parseNumber(text);
  return q != null && q >= 0 ? { category, qk: q } : undefined;
}

/** The site's climate as the engine has it (analysis.climate): the user's snow and wind values, and the terrain categories. */
export interface ClimateDto {
  snow: { s0k: number; ce: number; ct: number; importance: number } | null;
  wind: { qb: number; terrain: string; importance: number } | null;
  terrains: { key: string; z0: number; zmin: number; label: string }[];
}

/** CR 1-1-3/2012 Table 4.1: the exposure coefficient Ce of a site. */
export const EXPOSURES: [number, string][] = [[0.8, 'Full exposure (0.8)'], [1.0, 'Partial exposure (1.0)'], [1.2, 'Sheltered (1.2)']];
export const TERRAINS = ['0', 'I', 'II', 'III', 'IV'] as const;

export interface SnowArgs { s0k: number; ce: number; ct: number; importance: number }
export interface WindArgs { qb: number; terrain: string; importance: number }

/** The snow's fields typed: analysis.setClimate's snow, undefined when one does not read (s0,k is needed; the others default to 1). */
export function snowArgs(s0k: string, ce: string, ct: string, importance: string): SnowArgs | undefined {
  const sk = parseNumber(s0k);
  if (sk == null || sk < 0 || sk > 20) return undefined;
  const [c, t, i] = [ce, ct, importance].map((x) => (x.trim() ? parseNumber(x) : 1));
  if (c == null || t == null || i == null || c < 0.5 || c > 2 || t < 0.1 || t > 1.5 || i < 0.5 || i > 2) return undefined;
  return { s0k: sk, ce: c, ct: t, importance: i };
}

/** The wind's fields typed: analysis.setClimate's wind, undefined when one does not read (qb is needed; γIw defaults to 1). */
export function windArgs(qb: string, terrain: string, importance: string): WindArgs | undefined {
  const q = parseNumber(qb);
  const i = importance.trim() ? parseNumber(importance) : 1;
  if (q == null || q < 0 || q > 10 || i == null || i < 0.5 || i > 2 || !(TERRAINS as readonly string[]).includes(terrain)) return undefined;
  return { qb: q, terrain, importance: i };
}
