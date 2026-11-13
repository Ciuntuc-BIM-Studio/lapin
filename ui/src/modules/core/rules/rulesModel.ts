/**
 * Rule packs, the pure side (docs/plan-user-extensibility.md §5.4): the sections a pack holds as the engine gives them — the
 * stairs' sets, the reinforcement's steels, diameters, mandrel and hooks, the roofs' least pitches — and how each is edited as a
 * table: a value set, a step added or removed (the last step of a mandrel or a hook has no "up to"), a set started from another.
 * Every function returns a new section; the dialog sends the whole section to `rules.update`.
 */

export type Section = Record<string, unknown>;
export interface PackItem { id: string; name: string; builtin: boolean; active: boolean; description?: string | null }
export interface RulesList { items: PackItem[]; active: string; sections: { key: string; label: string }[] }
export interface RulePack { id: string; name: string; builtin: boolean; indicative: boolean; sections: Record<string, Section>; defaults: Record<string, Section> }
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
