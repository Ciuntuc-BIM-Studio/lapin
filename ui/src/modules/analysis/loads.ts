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
export type CombinationKind = 'uls' | 'sls-characteristic' | 'sls-frequent' | 'sls-quasi' | 'seismic' | 'accidental';
/** A set of factors: Lapin's en and cr0, or one of the rule pack's (docs/plan-national-packs.md N1). */
export type FactorSet = string;
export type PsiCategory = Record<'0' | '1' | '2', number>;
export interface CombinationDto {
  key: string; kind: CombinationKind; name: string; factors: { caseId: string; code: string; factor: number }[];
  formula?: string; source?: 'generated' | 'user'; enabled?: boolean;
  /** A user combination only: its id, and the cases it names that no longer exist. */
  id?: string; missing?: string[];
}
export interface CombinationSetDto {
  name: string; label: string; gammaGSup: number; gammaGInf: number; gammaQ: number; psi: Record<string, PsiCategory>;
  expression?: '6.10' | '6.10ab'; xi?: number; safetyClasses?: Record<string, number>; snowPsi?: { from: number; psi: PsiCategory }[];
  /** Whether it holds exactly Lapin's values of its name. */
  builtin?: boolean;
}
export interface CombinationOptionsDto {
  set: FactorSet; gammaGSup: number; gammaGInf: number; gammaQ: number;
  own: { gammaGSup: number | null; gammaGInf: number | null; gammaQ: number | null; xi: number | null; psi: Record<string, PsiCategory> };
  psi: Record<string, PsiCategory>; expression: '6.10' | '6.10ab'; xi: number; disabled: string[];
  sets: Record<string, CombinationSetDto>;
  /** The project's safety class and its γd (1 without one); the set it names that the rules in use lack; the snow zone the site falls in. */
  safetyClass?: string | null; gammaD?: number; missingSet?: string; snowZone?: { from: number; s0k: number; psi: PsiCategory };
}
export interface CombinationsDto { combinations: CombinationDto[]; options?: CombinationOptionsDto; kinds?: CombinationKind[]; notes: string[] }

/** The kinds a user combination can have, in the order the form lists them. */
export const COMBINATION_KINDS: CombinationKind[] = ['uls', 'sls-characteristic', 'sls-frequent', 'sls-quasi', 'seismic', 'accidental'];
export const COMBINATION_KIND_LABEL: Record<CombinationKind, string> = {
  uls: 'Ultimate (ULS)', 'sls-characteristic': 'Characteristic (SLS)', 'sls-frequent': 'Frequent (SLS)', 'sls-quasi': 'Quasi-permanent (SLS)',
  seismic: 'Seismic (with AEd)', accidental: 'Accidental',
};
export const FACTOR_SET_LABEL: Record<string, string> = { cr0: 'CR 0-2012 (Romania)', en: 'EN 1990 Annex A1, recommended' };

/** The sets offered, Lapin's first (cr0, en), then the rule pack's by name; each its label and whether it is a translated one of Lapin's. */
export function factorSetOptions(sets: Record<string, CombinationSetDto>): { key: string; label: string; ours: boolean }[] {
  const keys = Object.keys(sets);
  const lapin = ['cr0', 'en'].filter((k) => keys.includes(k));
  const own = keys.filter((k) => !lapin.includes(k)).sort();
  return [
    ...lapin.map((k) => ({ key: k, label: sets[k]!.builtin === false ? sets[k]!.label : FACTOR_SET_LABEL[k]!, ours: sets[k]!.builtin !== false })),
    ...own.map((k) => ({ key: k, label: sets[k]!.label || k, ours: false })),
  ];
}
/** The categories of the ψ table, in the engine's order. */
export const PSI_CATEGORIES = ['A', 'A-stairs', 'A-balcony', 'B', 'C1', 'C2', 'C3', 'C4', 'C5', 'D1', 'D2', 'E1', 'F', 'G', 'H', 'snow', 'wind'] as const;

/** The ultimate group holds the ULS, accidental and seismic combinations; the serviceability group the SLS ones. */
export const isUltimate = (k: CombinationKind): boolean => k === 'uls' || k === 'accidental' || k === 'seismic';
export function groupCombinations(list: CombinationDto[]): { ultimate: CombinationDto[]; serviceability: CombinationDto[] } {
  return { ultimate: list.filter((c) => isUltimate(c.kind)), serviceability: list.filter((c) => !isUltimate(c.kind)) };
}

/** The factors typed for a combination (one text per case id): empty and zero fields left out; undefined when one does not read. */
export function factorsPayload(texts: Record<string, string>): { caseId: string; factor: number }[] | undefined {
  const out: { caseId: string; factor: number }[] = [];
  for (const [caseId, text] of Object.entries(texts)) {
    if (!text.trim()) continue;
    const n = parseNumber(text);
    if (n == null) return undefined;
    if (n !== 0) out.push({ caseId, factor: n });
  }
  return out;
}

/** The texts of a combination's factors, for editing: one per case id. */
export const factorTexts = (factors: { caseId: string; factor: number }[]): Record<string, string> => Object.fromEntries(factors.map((f) => [f.caseId, formatFactor(f.factor)]));

/** A factor typed within [min, max]: null when empty (back to the set's value), undefined when it does not read or is out of range. */
export function boundedOrNull(text: string, min: number, max: number): number | null | undefined {
  if (!text.trim()) return null;
  const n = parseNumber(text);
  return n != null && n >= min && n <= max ? n : undefined;
}
export const GAMMA_RANGES = { gammaGSup: [1, 2], gammaGInf: [0, 1.5], gammaQ: [1, 2], xi: [0.5, 1] } as const;

/** The arguments of analysis.addCombination: undefined when the name is empty or a factor does not read. */
export function addCombinationArgs(name: string, kind: CombinationKind, texts: Record<string, string>) {
  const factors = factorsPayload(texts);
  return name.trim() && factors ? { name: name.trim(), kind, factors } : undefined;
}

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

export const combinationName = (c: CombinationDto): string => c.name || c.formula || c.factors.map((f) => `${formatFactor(f.factor)} ${f.code}`).join(' + ');

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
  /** The rule pack's regions (none in Lapin's), their label (Kommun, County) and the one the site's values were picked from. */
  regions?: ClimateRegionDto[];
  regionLabel?: string;
  region?: string | null;
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

/** A region of the rule pack's climate list (docs/plan-national-packs.md N2), as the climate query gives it. */
export interface ClimateRegionDto { name: string; sk?: number; vb0?: number; qb?: number; qbDerived?: number; note?: string }

/** The region a typed name picks from the list (any case, trimmed); undefined when the list has none of that name. */
export function regionArgs(name: string, regions: ClimateRegionDto[]): { region: string } | undefined {
  const n = name.trim().toLowerCase();
  const r = n ? regions.find((x) => x.name.toLowerCase() === n) : undefined;
  return r ? { region: r.name } : undefined;
}

/** What a region gives, in words: sk and qb (from vb,0 when it gives that). */
export function regionSummary(r: ClimateRegionDto): string {
  const parts: string[] = [];
  if (r.sk != null) parts.push(`sk ${formatKn(r.sk)} kN/m²`);
  if (r.vb0 != null) parts.push(`vb,0 ${formatKn(r.vb0)} m/s`);
  if (r.qbDerived != null) parts.push(`qb ${String(Math.round(r.qbDerived * 1e4) / 1e4)} kN/m²`);
  return parts.join(' · ');
}
