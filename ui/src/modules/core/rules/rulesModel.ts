/**
 * Rule packs, the pure side (docs/plan-user-extensibility.md §5.4): the sections a pack holds as the engine gives them — the
 * stairs' sets, the reinforcement's steels, diameters, mandrel and hooks, the roofs' least pitches — and how each is edited as a
 * table: a value set, a step added or removed (the last step of a mandrel or a hook has no "up to"), a set started from another.
 * Every function returns a new section; the dialog sends the whole section to `rules.update`.
 */

export type Section = Record<string, unknown>;
/** What a pack says of itself: the country, the edition of the rules it follows, its language, its sources (one a line). */
export interface PackMeta { country?: string; edition?: string; language?: string; sources?: string[] }
export interface PackItem { id: string; name: string; builtin: boolean; active: boolean; description?: string | null; meta?: PackMeta }
export interface RulesList { items: PackItem[]; active: string; sections: { key: string; label: string }[] }
export interface RulePack { id: string; name: string; builtin: boolean; indicative: boolean; sections: Record<string, Section>; defaults: Record<string, Section>; meta?: PackMeta }
export interface Finding { module: string; element: string | null; severity: string; message: string }

// ─── Stairs ──────────────────────────────────────────────────────────────────

/** The fields of a stair set, how each is shown: a length (m in the engine), a count, or a slope (a fraction, shown as a percentage). */
export interface StairField { key: string; label: string; kind: 'length' | 'count' | 'slope' }
export const STAIR_FIELDS: StairField[] = [
  { key: 'riserMin', label: 'Riser, least', kind: 'length' }, { key: 'riserTarget', label: 'Riser, aimed at', kind: 'length' }, { key: 'riserMax', label: 'Riser, most', kind: 'length' },
  { key: 'treadMin', label: 'Tread, least', kind: 'length' }, { key: 'treadTarget', label: 'Tread, aimed at', kind: 'length' }, { key: 'treadMax', label: 'Tread, most', kind: 'length' },
  { key: 'stepMin', label: 'Step (2 risers + a tread), least', kind: 'length' }, { key: 'stepMax', label: 'Step, most', kind: 'length' },
  { key: 'minWidth', label: 'Width, least', kind: 'length' }, { key: 'headroom', label: 'Headroom', kind: 'length' },
  { key: 'minRisers', label: 'Risers in a flight, at least', kind: 'count' }, { key: 'maxRisers', label: 'Risers in a flight, at most', kind: 'count' },
  { key: 'maxSlope', label: 'Slope, steepest', kind: 'slope' },
];

/** Lapin's own sets, which stay in every pack. */
export const BUILTIN_SETS = ['residential', 'public', 'entrance', 'ramp'];

export function setStairValue(section: Section, set: string, key: string, value: number): Section {
  const sets = section as Record<string, Record<string, unknown>>;
  return { ...section, [set]: { ...sets[set], [key]: value } };
}

/** A set of one's own started as a copy of another (its values the other's). */
export function addStairSet(section: Section, name: string, from: string): Section | null {
  const n = name.trim();
  const sets = section as Record<string, Record<string, unknown>>;
  if (!n || n.length > 40 || n in sets || !sets[from]) return null;
  return { ...section, [n]: { ...sets[from] } };
}

export function removeStairSet(section: Section, set: string): Section {
  if (BUILTIN_SETS.includes(set)) return section;
  const { [set]: _gone, ...rest } = section;
  return rest;
}

/** A least value above its most (the engine refuses it, saying so): found before sending. */
export function stairProblem(rules: Record<string, unknown>): string | null {
  const n = (k: string) => Number(rules[k]);
  if (n('riserMin') > n('riserMax')) return 'a least riser is above its most';
  if (n('treadMin') > n('treadMax')) return 'a least tread is above its most';
  if (n('stepMin') > n('stepMax')) return 'a least step is above its most';
  return null;
}

// ─── Reinforcement ───────────────────────────────────────────────────────────

export interface Step { upTo?: number; factor: number }

/** A step changed; the last step has no "up to" (it takes everything beyond). */
export function setStep(steps: Step[], at: number, patch: Partial<Step>): Step[] {
  return steps.map((s, i) => (i === at ? { ...s, ...patch } : s));
}

/** A step added before the last one, above the one before it (by `by`), the last's factor. */
export function addStep(steps: Step[], by: number): Step[] {
  if (steps.length >= 12) return steps;
  const last = steps[steps.length - 1];
  const before = steps.length > 1 ? steps[steps.length - 2].upTo ?? 0 : 0;
  return [...steps.slice(0, -1), { upTo: before + by, factor: last?.factor ?? 1 }, last ?? { factor: 1 }];
}

/** A step removed (one at least stays, and the last always stays). */
export function removeStep(steps: Step[], at: number): Step[] {
  if (steps.length <= 1) return steps;
  const next = steps.filter((_, i) => i !== at);
  if (at === steps.length - 1) { const { upTo: _u, ...last } = next[next.length - 1]; next[next.length - 1] = last; }
  return next;
}

/** Steps that go up, each above the one before (what the engine asks): the first problem in words, else null. */
export function stepsProblem(steps: Step[], what: string): string | null {
  let last = 0;
  for (let i = 0; i < steps.length - 1; i++) {
    const up = steps[i].upTo;
    if (up == null || !(up > last)) return `the ${what}'s steps go up, each above the one before`;
    last = up;
  }
  return steps.every((s) => s.factor > 0 && s.factor <= 50) ? null : `each step of the ${what} has a factor above 0 and at most 50`;
}

/** Diameters typed as a list (8, 10 12; 14): sorted, each once, between 4 and 50 mm; null when something does not read. */
export function parseDiameters(text: string): number[] | null {
  const nums = text.split(/[\s,;]+/).filter(Boolean).map((x) => Number(x.replace(',', '.')));
  if (nums.length === 0 || nums.some((n) => !Number.isFinite(n) || n < 4 || n > 50)) return null;
  return [...new Set(nums)].sort((a, b) => a - b);
}

export const diametersText = (d: number[]): string => d.join(', ');

/** A steel offered, added (once, trimmed, at most 30 characters). */
export function addSteel(steels: string[], name: string): string[] {
  const n = name.trim();
  return n && n.length <= 30 && !steels.includes(n) ? [...steels, n] : steels;
}

/** A steel taken off; the default moves to the first left when it was the one removed (one steel at least stays). */
export function removeSteel(rebar: Section, name: string): Section {
  const steels = (rebar.steels as string[]).filter((s) => s !== name);
  if (steels.length === 0) return rebar;
  return { ...rebar, steels, defaultSteel: rebar.defaultSteel === name ? steels[0] : rebar.defaultSteel };
}

// ─── Roofs ───────────────────────────────────────────────────────────────────

/** A covering's least pitch (degrees, 0 to 89). */
export function setPitch(roofs: Section, covering: string, deg: number): Section {
  const minPitch = (roofs.minPitch ?? {}) as Record<string, number>;
  return { ...roofs, minPitch: { ...minPitch, [covering]: Math.min(89, Math.max(0, deg)) } };
}

// ─── Bolts ───────────────────────────────────────────────────────────────────

/** A grade added (a name of 1 to 20 characters, not already there). */
export function addGrade(grades: string[], name: string): string[] {
  const n = name.trim();
  return n && n.length <= 20 && !grades.includes(n) ? [...grades, n] : grades;
}

/** A grade renamed in place (unchanged when the name is empty, too long or already there). */
export function renameGrade(grades: string[], at: number, name: string): string[] {
  const n = name.trim();
  if (!n || n.length > 20 || grades.some((g, i) => i !== at && g === n)) return grades;
  return grades.map((g, i) => (i === at ? n : g));
}

/** A grade taken off (one at least stays). */
export function removeGrade(grades: string[], at: number): string[] {
  return grades.length <= 1 ? grades : grades.filter((_, i) => i !== at);
}

// ─── Findings ────────────────────────────────────────────────────────────────

/** Findings by module, the worst first inside each. */
export function findingsByModule(findings: Finding[]): [string, Finding[]][] {
  const rank: Record<string, number> = { error: 0, warning: 1, info: 2 };
  const by = new Map<string, Finding[]>();
  for (const f of findings) by.set(f.module, [...(by.get(f.module) ?? []), f]);
  return [...by].map(([m, l]) => [m, [...l].sort((a, b) => (rank[a.severity] ?? 3) - (rank[b.severity] ?? 3))] as [string, Finding[]]);
}

// ─── Pack metadata ───────────────────────────────────────────────────────────

/** The sources typed (one a line) as rules.update takes them: blank lines left out. */
export const sourcesFrom = (text: string): string[] => text.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.length > 0);

/** A language code as a pack gives it (sv, ro, pt-BR), or empty; null when it does not read. */
export function languageFrom(text: string): string | null {
  const t = text.trim();
  return t === '' || /^[a-z]{2}(-[A-Z]{2})?$/.test(t) ? t : null;
}

// ─── Loads (docs/plan-national-packs.md N1) ──────────────────────────────────

export type PsiValues = Record<'0' | '1' | '2', number>;
export interface SnowZone { from: number; psi: PsiValues }
/** A factor set as the pack holds it: γ, the expression and ξ, ψ by category, ψ of snow by s0,k, γd by safety class. */
export interface LoadSet {
  label: string; gammaGSup: number; gammaGInf: number; gammaQ: number; expression: '6.10' | '6.10ab'; xi: number;
  psi: Record<string, PsiValues>; snowPsi: SnowZone[]; safetyClasses: Record<string, number>;
}
export interface LoadsSection { sets: Record<string, LoadSet>; imposed: Record<string, number> }

/** Lapin's own sets: a pack may change them; a set of the user's comes and goes. */
export const BUILTIN_LOAD_SETS = ['en', 'cr0'];

/** Each number of a set, its label and its range (as the engine checks it). */
export const LOAD_SET_FIELDS: { key: 'gammaGSup' | 'gammaGInf' | 'gammaQ' | 'xi'; label: string; min: number; max: number }[] = [
  { key: 'gammaGSup', label: 'γG,sup', min: 1, max: 2 }, { key: 'gammaGInf', label: 'γG,inf', min: 0, max: 1.5 },
  { key: 'gammaQ', label: 'γQ', min: 1, max: 2 }, { key: 'xi', label: 'ξ', min: 0.5, max: 1 },
];

export function setLoadSet(loads: LoadsSection, name: string, patch: Partial<LoadSet>): LoadsSection {
  return { ...loads, sets: { ...loads.sets, [name]: { ...loads.sets[name]!, ...patch } } };
}

/** A set of one's own started as a copy of another; null when the name is empty, too long or taken. */
export function addLoadSet(loads: LoadsSection, name: string, from: string): LoadsSection | null {
  const n = name.trim();
  if (!n || n.length > 32 || n in loads.sets || !loads.sets[from]) return null;
  return { ...loads, sets: { ...loads.sets, [n]: { ...loads.sets[from]!, label: n } } };
}

export function removeLoadSet(loads: LoadsSection, name: string): LoadsSection {
  if (BUILTIN_LOAD_SETS.includes(name) || Object.keys(loads.sets).length <= 1) return loads;
  const { [name]: _gone, ...rest } = loads.sets;
  return { ...loads, sets: rest };
}

/** One ψ of a category set (ψ0, ψ1 or ψ2). */
export function setPsi(set: LoadSet, category: string, i: 0 | 1 | 2, value: number): LoadSet {
  const had = set.psi[category] ?? { '0': 0, '1': 0, '2': 0 };
  return { ...set, psi: { ...set.psi, [category]: { ...had, [String(i)]: value } as PsiValues } };
}

/** A safety class given its γd (added or changed); null when the name is empty or too long, or γd out of 0.5–1.5. */
export function setSafetyClass(set: LoadSet, name: string, gammaD: number): LoadSet | null {
  const n = name.trim();
  if (!n || n.length > 32 || !(gammaD >= 0.5 && gammaD <= 1.5)) return null;
  return { ...set, safetyClasses: { ...set.safetyClasses, [n]: gammaD } };
}

export function removeSafetyClass(set: LoadSet, name: string): LoadSet {
  const { [name]: _gone, ...rest } = set.safetyClasses;
  return { ...set, safetyClasses: rest };
}

/** The snow zones in order of their s0,k; a new one starts 1 kN/m² past the last with its ψ. */
export function addSnowZone(set: LoadSet): LoadSet {
  const zones = [...set.snowPsi].sort((a, b) => a.from - b.from);
  const last = zones[zones.length - 1];
  const psi = last?.psi ?? set.psi.snow ?? { '0': 0.5, '1': 0.2, '2': 0 };
  return { ...set, snowPsi: [...zones, { from: last ? last.from + 1 : 0, psi: { ...psi } }] };
}

export function setSnowZone(set: LoadSet, at: number, patch: Partial<SnowZone>): LoadSet {
  return { ...set, snowPsi: set.snowPsi.map((z, i) => (i === at ? { ...z, ...patch, psi: { ...z.psi, ...(patch.psi ?? {}) } } : z)) };
}

export function removeSnowZone(set: LoadSet, at: number): LoadSet {
  return { ...set, snowPsi: set.snowPsi.filter((_, i) => i !== at) };
}

/** What the engine would refuse in a set, found before sending: two zones from the same s0,k, a value out of range. */
export function loadSetProblem(set: LoadSet): string | null {
  for (const f of LOAD_SET_FIELDS) if (!(set[f.key] >= f.min && set[f.key] <= f.max)) return `${f.label} is between ${f.min} and ${f.max}`;
  const froms = set.snowPsi.map((z) => z.from);
  if (new Set(froms).size !== froms.length) return 'two snow zones start at the same s0,k';
  if (froms.some((f) => !(f >= 0 && f <= 20))) return "a snow zone's s0,k is between 0 and 20 kN/m²";
  const psis = [...Object.values(set.psi), ...set.snowPsi.map((z) => z.psi)];
  if (psis.some((p) => [p['0'], p['1'], p['2']].some((v) => !(v >= 0 && v <= 1)))) return 'ψ0, ψ1 and ψ2 are between 0 and 1';
  return null;
}

// ─── Climate (docs/plan-national-packs.md N2) ────────────────────────────────

/** A region of a pack's climate list: its name, sk (kN/m²), vb,0 (m/s) or qb (kN/m²), a note. */
export interface Region { name: string; sk?: number; vb0?: number; qb?: number; note?: string }
export interface ClimateSection { regionLabel: string; regions: Region[] }

/** qb from vb,0 as the engine works it out: ½ · 1.25 kg/m³ · vb,0², in kN/m² (four decimals). */
export const qbFromVb0 = (vb0: number): number => Math.round(0.5 * 1.25 * vb0 * vb0 / 1000 * 1e4) / 1e4;

/** What the engine would refuse in a region, in words; null when it reads. */
export function regionProblem(r: Region): string | null {
  const n = r.name.trim();
  if (!n || n.length > 80) return "a region's name is 1 to 80 characters";
  if (r.sk != null && !(r.sk >= 0 && r.sk <= 20)) return `${n}: sk is between 0 and 20 kN/m²`;
  if (r.vb0 != null && !(r.vb0 >= 0 && r.vb0 <= 60)) return `${n}: vb,0 is between 0 and 60 m/s`;
  if (r.qb != null && !(r.qb >= 0 && r.qb <= 10)) return `${n}: qb is between 0 and 10 kN/m²`;
  if (r.vb0 != null && r.qb != null) return `${n}: give vb,0 or qb, not both`;
  if (r.sk == null && r.vb0 == null && r.qb == null) return `${n}: a region gives sk, vb,0 or qb`;
  return null;
}

export function regionsProblem(c: ClimateSection): string | null {
  const seen = new Set<string>();
  for (const r of c.regions) {
    const p = regionProblem(r);
    if (p) return p;
    const k = r.name.trim().toLowerCase();
    if (seen.has(k)) return `the region ${r.name.trim()} is listed twice`;
    seen.add(k);
  }
  return c.regions.length > 500 ? 'a pack lists at most 500 regions' : null;
}

/** A region changed in place; a value set to null is taken off. */
export function setRegion(c: ClimateSection, at: number, patch: { name?: string; sk?: number | null; vb0?: number | null; qb?: number | null; note?: string | null }): ClimateSection {
  return {
    ...c,
    regions: c.regions.map((r, i) => {
      if (i !== at) return r;
      const next: Record<string, unknown> = { ...r, ...patch };
      for (const k of Object.keys(next)) if (next[k] == null || next[k] === '') delete next[k];
      return next as unknown as Region;
    }),
  };
}

/** A region added at the end (its values empty until typed); null when the name is empty, too long or taken. */
export function addRegion(c: ClimateSection, name: string): ClimateSection | null {
  const n = name.trim();
  if (!n || n.length > 80 || c.regions.some((r) => r.name.toLowerCase() === n.toLowerCase())) return null;
  return { ...c, regions: [...c.regions, { name: n, sk: 0 }] };
}

export function removeRegion(c: ClimateSection, at: number): ClimateSection {
  return { ...c, regions: c.regions.filter((_, i) => i !== at) };
}

const numberIn = (t: string | undefined): number | undefined => {
  const s = (t ?? '').trim().replace(',', '.');
  if (s === '') return undefined;
  const n = Number(s);
  return Number.isFinite(n) ? n : NaN;
};

/**
 * Regions pasted from a table, one a line: name, sk, vb,0 (and a note), the columns split by tabs or semicolons (a decimal comma
 * reads). A first line whose sk does not read is a header, left out. The regions, or the first line that does not read.
 */
export function parseRegions(text: string): { regions: Region[]; problem: string | null } {
  const lines = text.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.length > 0);
  const regions: Region[] = [];
  for (let i = 0; i < lines.length; i++) {
    const cells = lines[i]!.split(/\t|;/).map((c) => c.trim());
    const [name, skText, vbText, ...rest] = cells;
    const sk = numberIn(skText);
    const vb0 = numberIn(vbText);
    if (i === 0 && (Number.isNaN(sk) || Number.isNaN(vb0))) continue;
    if (Number.isNaN(sk) || Number.isNaN(vb0)) return { regions, problem: `line ${i + 1} does not read: name; sk; vb,0` };
    const r: Region = { name: (name ?? '').trim() };
    if (sk != null) r.sk = sk;
    if (vb0 != null) r.vb0 = vb0;
    const note = rest.join(' ').trim();
    if (note) r.note = note;
    const p = regionProblem(r);
    if (p) return { regions, problem: `line ${i + 1}: ${p}` };
    regions.push(r);
  }
  return { regions, problem: null };
}

/** Regions pasted into a list: one of a name already there replaced, a new one added at the end. */
export function mergeRegions(c: ClimateSection, regions: Region[]): ClimateSection {
  const next = [...c.regions];
  for (const r of regions) {
    const at = next.findIndex((x) => x.name.toLowerCase() === r.name.toLowerCase());
    if (at >= 0) next[at] = r; else next.push(r);
  }
  return { ...c, regions: next };
}

// ─── Accessibility (docs/plan-national-packs.md N3) ──────────────────────────

/** The limits a pack checks (null: not checked), whether ramps are checked against the stairs' set ramp, the rooms' functions. */
export interface AccessibilitySection {
  doorClearWidth: number | null; corridorWidth: number | null; turningDiameter: number | null; checkRamps: boolean;
  corridorFunctions: string[]; turningFunctions: string[];
}

/** Each limit, its label and its range in metres (as the engine checks it). */
export const ACCESS_LIMITS: { key: 'doorClearWidth' | 'corridorWidth' | 'turningDiameter'; label: string; min: number; max: number }[] = [
  { key: 'doorClearWidth', label: "A door's least clear width", min: 0.5, max: 2.5 },
  { key: 'corridorWidth', label: "A corridor's least width", min: 0.6, max: 5 },
  { key: 'turningDiameter', label: 'Turning circle, diameter', min: 1, max: 3 },
];

/** A limit set (null: not checked); null when it is out of its range. */
export function setAccessLimit(a: AccessibilitySection, key: typeof ACCESS_LIMITS[number]['key'], metres: number | null): AccessibilitySection | null {
  const f = ACCESS_LIMITS.find((x) => x.key === key)!;
  if (metres != null && !(metres >= f.min && metres <= f.max)) return null;
  return { ...a, [key]: metres };
}

/** Room functions typed as a list (Bath, WC; Bedroom): trimmed, each once (whatever its case), at most 30. */
export function functionsFrom(text: string): string[] {
  const out: string[] = [];
  for (const t of text.split(/[,;\n]+/).map((x) => x.trim()).filter((x) => x.length > 0 && x.length <= 60))
    if (!out.some((o) => o.toLowerCase() === t.toLowerCase())) out.push(t);
  return out.slice(0, 30);
}

/** Whether a pack checks anything of accessibility. */
export const checksAccessibility = (a: AccessibilitySection): boolean =>
  a.doorClearWidth != null || a.corridorWidth != null || a.turningDiameter != null || a.checkRamps;
