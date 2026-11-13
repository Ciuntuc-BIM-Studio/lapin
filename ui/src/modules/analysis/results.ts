/** Structural analysis, stage S1b (docs/plan-fem.md §9.6): the pure parts of the results interface: the engine's reports, the picker, the diagrams' geometry, the end releases. */

import type { AnalysisOptionsDto, ShellResultDto, ShellsRunDto } from './shells';

export interface RunCaseDto { id: string; code: string; sumLoads: number[]; sumReactions: number[] | null; balance: number | null; balanced: boolean | null; unmapped: number }
export interface RunComboDto { key: string; kind: string; name: string }
export interface MechanismDto {
  node: string; at: number[]; dof: string; elements: string[]; sources: string[]; names: string[];
  moving: { node: string; at: number[] }[]; movingElements?: string[]; movingNames?: string[]; message: string;
}
export interface RefusedDto { id: string; sourceId: string; name: string; reason: string }
export interface RunDto {
  ok: boolean; dofs: number; elements: number; nodes: number; solveMs: number; totalMs: number; profile: number;
  cases: RunCaseDto[]; combinations: RunComboDto[]; mechanism?: MechanismDto; refused?: RefusedDto[]; notes: string[];
  /** The slabs and walls analysed as shells (S2b), and the options the run took. */
  shells?: ShellsRunDto; settings?: AnalysisOptionsDto;
}

export interface Extreme { frame: string; x: number; value: number }
export interface MaximaDto {
  displacement?: { node: string; value: number }; deflection?: Extreme;
  [q: string]: { max: Extreme; min: Extreme } | { node: string; value: number } | Extreme | undefined;
}
export interface ReactionDto { node: string; at: number[]; f?: number[]; min?: number[]; max?: number[] }
export interface FrameResultDto {
  id: string; sourceId: string; name: string; length: number;
  extremes: Record<string, { max: number; xMax: number; min: number; xMin: number }>;
  deflection?: { max: number; x: number };
}
export interface ResultsDto {
  kind: string; key: string; name: string; units?: Record<string, string>;
  reactions: ReactionDto[]; frames: FrameResultDto[]; maxima: MaximaDto;
  shells?: ShellResultDto[];
}
export interface PolylineDto { frame: string; sourceId: string; base: number[][]; points: number[][]; values: number[]; bound?: 'min' | 'max'; combination?: string }
export interface DiagramDto { kind: string; key: string; quantity: string; unit: string; scale: number; max: number; polylines: PolylineDto[] }

// ── What is shown: the case, combination or envelope, and the quantity ──

export type Pick = { kind: 'case' | 'combination'; key: string } | { kind: 'envelope'; key: 'uls' | 'sls' };
export const QUANTITIES = ['N', 'Vy', 'Vz', 'T', 'My', 'Mz', 'deformed'] as const;
export type Quantity = typeof QUANTITIES[number];
export const QUANTITY_LABEL: Record<Quantity, string> = {
  N: 'N · axial force', Vy: 'Vy · shear', Vz: 'Vz · shear', T: 'T · torsion', My: 'My · moment', Mz: 'Mz · moment (strong axis)', deformed: 'Deformed shape',
};
/** The quantity as the bars' table headings write it. */
export const FORCE_QUANTITIES = ['N', 'Vy', 'Vz', 'T', 'My', 'Mz'] as const;

/** The query arguments naming what is shown. */
export const pickArgs = (p: Pick): Record<string, string> => (p.kind === 'case' ? { case: p.key } : p.kind === 'combination' ? { combination: p.key } : { envelope: p.key });
export const samePick = (a: Pick | null, b: Pick | null): boolean => (a === null || b === null ? a === b : a.kind === b.kind && a.key === b.key);
/** The picker's value as one string ("case:id", "envelope:uls"), and back. */
export const pickValue = (p: Pick): string => `${p.kind}:${p.key}`;
export function pickFrom(v: string): Pick | null {
  const i = v.indexOf(':');
  if (i < 0) return null;
  const kind = v.slice(0, i), key = v.slice(i + 1);
  if (kind === 'case' || kind === 'combination') return { kind, key };
  if (kind === 'envelope' && (key === 'uls' || key === 'sls')) return { kind, key };
  return null;
}

/** What to show first after a run: the ultimate envelope when there are combinations of it, else the first case that carries a load. */
export function defaultPick(run: RunDto): Pick | null {
  if (run.combinations.some((c) => c.kind === 'uls')) return { kind: 'envelope', key: 'uls' };
  const loaded = run.cases.find((c) => c.sumLoads.some((x) => Math.abs(x) > 1e-9)) ?? run.cases[0];
  return loaded ? { kind: 'case', key: loaded.id } : null;
}
/** The picker's entries in groups: the cases, then the combinations, then the envelopes. */
export function pickChoices(run: RunDto): { group: 'Load cases' | 'Combinations' | 'Envelopes'; pick: Pick; label: string }[] {
  const out: { group: 'Load cases' | 'Combinations' | 'Envelopes'; pick: Pick; label: string }[] = [];
  for (const c of run.cases) out.push({ group: 'Load cases', pick: { kind: 'case', key: c.id }, label: c.code });
  for (const c of run.combinations) out.push({ group: 'Combinations', pick: { kind: 'combination', key: c.key }, label: c.name || c.key });
  if (run.combinations.some((c) => c.kind === 'uls')) out.push({ group: 'Envelopes', pick: { kind: 'envelope', key: 'uls' }, label: 'Ultimate limit state (envelope)' });
  if (run.combinations.some((c) => c.kind !== 'uls')) out.push({ group: 'Envelopes', pick: { kind: 'envelope', key: 'sls' }, label: 'Serviceability limit state (envelope)' });
  return out;
}
/** A pick that still exists in this run (a case or combination can go between two runs); else the default. */
export function validPick(run: RunDto, p: Pick | null): Pick | null {
  if (p && pickChoices(run).some((c) => samePick(c.pick, p))) return p;
  return defaultPick(run);
}

// ── Numbers ──

/** A force or moment with its sign, up to two decimals (3 significant when small): 12.35, -0.012. */
export function formatValue(v: number): string {
  if (!Number.isFinite(v)) return '–';
  const a = Math.abs(v);
  const r = a === 0 ? 0 : a < 0.1 ? Number(v.toPrecision(2)) : Math.round(v * 100) / 100;
  return String(Object.is(r, -0) ? 0 : r);
}
/** A displacement in metres written in millimetres, one decimal (two under 1 mm). */
export function formatMm(m: number): string {
  const mm = m * 1000;
  return `${Math.abs(mm) < 1 ? Math.round(mm * 100) / 100 : Math.round(mm * 10) / 10} mm`;
}
/** Span over deflection: L/300. */
export const spanRatio = (length: number, deflection: number): string | null => (deflection > 1e-9 && length > 0 ? `L/${Math.round(length / deflection)}` : null);
/** The balance of a case as a short text: "balanced" with its residual, or "does not balance". */
export const balanceText = (c: RunCaseDto): string => (c.balanced == null ? '–' : c.balanced ? 'balanced' : 'does not balance');
export const formatVec = (v: number[] | null | undefined): string => (v ? v.map(formatValue).join(' · ') : '–');

// ── Diagrams ──

export const SIGN_COLOUR = { positive: '#d9480f', negative: '#1971c2', neutral: '#868e96' } as const;
export const signColour = (v: number, eps = 1e-9): string => (v > eps ? SIGN_COLOUR.positive : v < -eps ? SIGN_COLOUR.negative : SIGN_COLOUR.neutral);

/** Where a point of the curve stands on the bar: its foot on the base line, by projection. */
export function footOnBase(base: number[][], p: number[]): number[] {
  const [a, b] = base as [number[], number[]];
  const d = [b[0]! - a[0]!, b[1]! - a[1]!, b[2]! - a[2]!];
  const l2 = d[0]! * d[0]! + d[1]! * d[1]! + d[2]! * d[2]!;
  const t = l2 < 1e-18 ? 0 : Math.min(1, Math.max(0, ((p[0]! - a[0]!) * d[0]! + (p[1]! - a[1]!) * d[1]! + (p[2]! - a[2]!) * d[2]!) / l2));
  return [a[0]! + d[0]! * t, a[1]! + d[1]! * t, a[2]! + d[2]! * t];
}

/** The indices of the hatch ticks: about `count` between the ends, none where the value is nothing. */
export function hatchIndices(values: number[], count = 6): number[] {
  const n = values.length;
  if (n < 3) return [];
  const step = Math.max(1, Math.round((n - 1) / count));
  const out: number[] = [];
  for (let i = 0; i < n; i += step) out.push(i);
  if (out[out.length - 1] !== n - 1) out.push(n - 1);
  return out;
}

/** Where a diagram is labelled: its greatest value, and its least when that is the other sign and not nothing. Indices into `values`. */
export function labelIndices(values: number[], rel = 0.02): number[] {
  if (values.length === 0) return [];
  let hi = 0, lo = 0;
  values.forEach((v, i) => { if (v > values[hi]!) hi = i; if (v < values[lo]!) lo = i; });
  const big = Math.max(Math.abs(values[hi]!), Math.abs(values[lo]!));
  if (big < 1e-9) return [];
  const out: number[] = [];
  const keep = (i: number) => { if (Math.abs(values[i]!) >= rel * big && !out.includes(i)) out.push(i); };
  const first = Math.abs(values[hi]!) >= Math.abs(values[lo]!) ? [hi, lo] : [lo, hi];
  first.forEach(keep);
  return out;
}

/** The arrows of the reactions: the vector shown (an envelope's larger of min and max) and which labels it takes (components above 5 % of the greatest). */
export function reactionVector(r: ReactionDto): number[] | null {
  if (r.f) return r.f;
  if (!r.min || !r.max) return null;
  const mag = (v: number[]) => Math.hypot(v[0]!, v[1]!, v[2]!);
  return mag(r.max) >= mag(r.min) ? r.max : r.min;
}
export function reactionLabel(f: number[], biggest: number): string {
  const parts = (['Fx', 'Fy', 'Fz'] as const).flatMap((n, i) => (Math.abs(f[i]!) >= 0.05 * biggest && Math.abs(f[i]!) > 1e-9 ? [`${n} ${formatValue(f[i]!)}`] : []));
  return parts.join(', ');
}

// ── End releases ──

export const DOFS = ['ux', 'uy', 'uz', 'rx', 'ry', 'rz'] as const;
export const DOF_TITLE: Record<typeof DOFS[number], string> = {
  ux: 'Axial displacement', uy: 'Shear displacement, local axis 2', uz: 'Shear displacement, local axis 3', rx: 'Torsion', ry: 'Rotation about local axis 2', rz: 'Rotation about local axis 3 (strong axis)',
};
export type Release = boolean[];
export const FIXED: Release = [false, false, false, false, false, false];
export const PINNED: Release = [false, false, false, false, true, true];
export type ReleasePreset = 'default' | 'fixed' | 'pinned' | 'custom';

/** The end's preset from what the element carries (null: none, the default). */
export function releasePreset(own: Release | null | undefined): ReleasePreset {
  if (!own) return 'default';
  if (own.every((x, i) => x === FIXED[i])) return 'fixed';
  if (own.every((x, i) => x === PINNED[i])) return 'pinned';
  return 'custom';
}
/** What setReleases takes for a preset (null: the default again); `from` is the toggles Custom starts from. */
export function releaseValue(preset: ReleasePreset, from: Release | null | undefined): Release | 'fixed' | 'pinned' | null {
  if (preset === 'default') return null;
  if (preset === 'fixed' || preset === 'pinned') return preset;
  return from && from.length === 6 ? [...from] : [false, false, false, false, false, true];
}
export const toggleRelease = (r: Release, i: number): Release => r.map((x, k) => (k === i ? !x : x));

/** The release an element carries, as the engine stored it: {i: [6], j: [6]}; either end absent. */
export function ownReleases(attr: unknown): { i: Release | null; j: Release | null } {
  const o = attr && typeof attr === 'object' ? (attr as Record<string, unknown>) : {};
  const end = (v: unknown): Release | null => (Array.isArray(v) && v.length === 6 && v.every((x) => typeof x === 'boolean') ? (v as boolean[]) : null);
  return { i: end(o.i), j: end(o.j) };
}

/** The classes an analytical bar can be (what the structure, steel and architecture modules contribute as frames). */
export const BAR_CLASSES = ['IfcColumn', 'IfcBeam', 'IfcMember', 'LapinSteelMember'] as const;

/** A value's label on the screen: where it would go, its text and how much it matters (its absolute value). */
export interface LabelCandidate { x: number; y: number; text: string; weight: number }

/**
 * The labels that fit (§9.6): the most important first, each kept only when its box (its text's length at about
 * `charWidth` pixels a character, `height` high, from (x, y−height) to the right) overlaps none kept before, so the values where
 * several bars meet do not print over one another. Returns the indices kept, in the order given.
 */
export function placeLabels(labels: readonly LabelCandidate[], charWidth = 7, height = 14): number[] {
  const kept: { x0: number; y0: number; x1: number; y1: number }[] = [];
  const order = labels.map((_, i) => i).sort((a, b) => labels[b]!.weight - labels[a]!.weight);
  const keep = new Set<number>();
  for (const i of order) {
    const l = labels[i]!;
    const box = { x0: l.x, y0: l.y - height, x1: l.x + l.text.length * charWidth, y1: l.y };
    if (kept.some((k) => box.x0 < k.x1 && k.x0 < box.x1 && box.y0 < k.y1 && k.y0 < box.y1)) continue;
    kept.push(box);
    keep.add(i);
  }
  return labels.map((_, i) => i).filter((i) => keep.has(i));
}
