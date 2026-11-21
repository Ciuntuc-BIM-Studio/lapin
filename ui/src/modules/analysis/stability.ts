/**
 * Structural analysis, stage S5 (docs/plan-fem.md §14): the pure parts of the stability section (second order, linear buckling,
 * the one-way members and the supports that lift) and of the supports' properties (springs, Winkler's ks, compression only).
 * Indicative: the engineer's own to check.
 */

/** One combination of the nonlinear solve (analysis.run → nonlinear). */
export type Variant = '+X' | '-X' | '+Y' | '-Y';
/** One of the four imperfection variants of an ultimate combination (§14.14). */
export interface VariantDto {
  variant: Variant; key: string; name: string; ok: boolean; iterations: number; converged: boolean; amplification: number | null;
  phi: number; m: number; force: number; neglectable: boolean; message: string | null;
}
export interface NonlinearComboDto {
  key: string; name: string; ok: boolean; iterations: number; converged: boolean; amplification: number | null; unstable?: boolean;
  message: string | null; slack: { id: string; sourceId: string; name: string }[]; lifted: { node: string; at: number[] }[];
  /** With the imperfections on: the variant of the largest amplification, and the four. */
  governing?: Variant; variants?: VariantDto[];
}
/** The global sway imperfections a run took (EN 1993-1-1 5.3.2): φ0, the height h, αh, φ and m along X and Y, and the combinations where 5.3.2(4) would let them go. */
export interface ImperfectionsDto {
  phi0: number; h: number; alphaH: number; x: { phi: number; m: number }; y: { phi: number; m: number }; timber?: boolean;
  neglectable: { key: string; name: string; direction: 'X' | 'Y' }[];
}
export interface NonlinearDto { secondOrder: boolean; imperfections?: ImperfectionsDto | null; combinations: NonlinearComboDto[] }
/** The members braced by their sheeting in a run (analysis.run → sheeting). */
export interface SheetingRunDto { members: number; nodes: number; spans?: number; skipped: string[] }
export interface SupportsRunDto { supports: number; nodes: number; winkler: number; slabs: number; slabNodes: number; compressionOnly: number }

export type BucklingClass = 'first-order' | 'amplify' | 'second-order' | 'unstable' | 'none';
export interface BucklingModeDto { alpha: number; local: { sourceId: string; name: string; share: number; purlin?: boolean } | null }
export interface BucklingComboDto {
  key: string; name: string; ok: boolean; alpha: number[]; modes: BucklingModeDto[]; global: number | null; globalAbove: number | null;
  class: BucklingClass; amplifier: number | null; compressed: boolean; converged: boolean; message: string | null;
}
export interface BucklingDto {
  ok: boolean; combinations: BucklingComboDto[]; lowest: { key: string; alpha: number; class: BucklingClass } | null; notes: string[];
}

export type Tone = 'ok' | 'warn' | 'bad' | 'none';
/** The class's colour (EN 1993-1-1 5.2.1(3)): green first order will do, amber second order or the amplified sway, red second order required or unstable. */
export function classTone(c: BucklingClass | string | null | undefined): Tone {
  switch (c) {
    case 'first-order': return 'ok';
    case 'amplify': return 'warn';
    case 'second-order': case 'unstable': return 'bad';
    default: return 'none';
  }
}
export const TONE_COLOUR: Record<Tone, string> = { ok: '#2b8a3e', warn: '#e67700', bad: '#c92a2a', none: '#868e96' };

export const CLASS_LABEL: Record<string, string> = {
  'first-order': 'First order will do', amplify: 'Amplify the sway effects, or second order', 'second-order': 'Second order required', unstable: 'Unstable', none: 'No compression',
};
export const CLASS_TITLE: Record<string, string> = {
  'first-order': 'αcr of 10 or more (EN 1993-1-1 5.2.1(3)): the first-order analysis is enough',
  amplify: 'αcr from 3 to 10: second-order effects must be taken, by second order or by the sway effects × 1/(1 − 1/αcr)',
  'second-order': 'αcr from 1 to 3: a second-order analysis is required',
  unstable: 'αcr under 1: the loads are past the critical ones',
  none: 'No bar is compressed: no buckling',
};

/** A critical load factor: three significant digits, at most two decimals. */
export function formatAlpha(a: number | null | undefined): string {
  if (a == null || !Number.isFinite(a)) return '–';
  return a >= 100 ? String(Math.round(a)) : a >= 10 ? String(Math.round(a * 10) / 10) : String(Math.round(a * 100) / 100);
}
/** The frame's αcr: the first mode no single member holds, or a lower bound ("≥ x") when every mode found is one member's own. */
export function globalText(c: Pick<BucklingComboDto, 'global' | 'globalAbove'>): string {
  if (c.global != null) return formatAlpha(c.global);
  if (c.globalAbove != null) return `≥ ${formatAlpha(c.globalAbove)}`;
  return '–';
}
/** The amplifier 1/(1 − 1/αcr), as ×1.23; none when the engine gave none. */
export const formatAmplifier = (a: number | null | undefined): string => (a == null || !Number.isFinite(a) ? '–' : `× ${formatAlpha(a)}`);
/** The P-Δ amplification (largest sway second order over first), as × 1.23. */
export const formatAmplification = formatAmplifier;

/** The first modes' α, each with the member that holds it when one does (a local mode: its member's own buckling). */
export function modeCells(c: BucklingComboDto, count = 3): { alpha: string; local: string | null; share: number | null }[] {
  const out: { alpha: string; local: string | null; share: number | null }[] = [];
  for (let i = 0; i < count; i++) {
    const m = c.modes?.[i];
    const a = c.alpha?.[i];
    if (a == null && !m) { out.push({ alpha: '–', local: null, share: null }); continue; }
    out.push({ alpha: formatAlpha(m?.alpha ?? a), local: m?.local?.name ?? null, share: m?.local?.share ?? null });
  }
  return out;
}

/** φ as the engineer writes it: 1/231 (N = 1/φ rounded); a dash when there is no column. */
export const formatPhi = (phi: number | null | undefined): string => (phi != null && phi > 0 && Number.isFinite(phi) ? `1/${Math.round(1 / phi)}` : '–');
/** φ and m along X and Y: "X 1/231 (m = 4) · Y 1/200 (m = 2)"; no m with timber (it has none). */
export function imperfectionsText(i: Pick<ImperfectionsDto, 'x' | 'y' | 'h' | 'alphaH'>): string {
  const along = (d: string, v: { phi: number; m: number }) => `${d} ${formatPhi(v.phi)}${v.phi > 0 && v.m > 0 ? ` (m = ${v.m})` : ''}`;
  return `φ ${along('X', i.x)} · ${along('Y', i.y)} · h = ${Math.round(i.h * 100) / 100} m · αh = ${Math.round(i.alphaH * 1000) / 1000}`;
}
/** A variant's name as the engine writes the combination's: φ(+X), φ(−X) with the typographic minus. */
export const variantLabel = (v: string | null | undefined): string => (v ? `φ(${v.replace('-', '−')})` : '–');
/** The four variants' amplifications, for a row's tooltip: "φ(+X) × 1.2 · φ(−X) × 1.1 …" (the names alone when first order). */
export const variantsTitle = (c: Pick<NonlinearComboDto, 'variants'>): string | undefined =>
  c.variants?.length ? c.variants.map((v) => (v.amplification != null ? `${variantLabel(v.variant)} ${formatAmplifier(v.amplification)}` : variantLabel(v.variant))).join(' · ') : undefined;
/** Whether a buckling result shows a local mode of a roof purlin: the sheeting could brace it. */
export const hasPurlinMode = (rows: Pick<BucklingComboDto, 'modes'>[]): boolean => rows.some((c) => c.modes?.some((m) => m.local?.purlin === true));

/** The slack members' names (a combination's one-way members out of work), each once. */
export const slackNames = (c: Pick<NonlinearComboDto, 'slack'>): string[] => [...new Set(c.slack.map((s) => s.name || s.sourceId))];

/** The combinations that came out worth a look: not ok, not converged, or amplified. */
export const needsLook = (c: NonlinearComboDto): boolean => !c.ok || !c.converged;

/** The combinations' modes asked for in the shape picker: 1..n of the chosen combination. */
export function modeChoices(c: BucklingComboDto | undefined): number[] {
  const n = c?.alpha?.length ?? 0;
  return Array.from({ length: n }, (_, i) => i + 1);
}

// ── The supports' properties ──

export const SPRING_LABELS = ['kx', 'ky', 'kz', 'krx', 'kry', 'krz'] as const;
export const SPRING_UNITS = ['kN/m', 'kN/m', 'kN/m', 'kN·m/rad', 'kN·m/rad', 'kN·m/rad'] as const;
export type SpringValues = (number | null)[];

export interface SupportOptionsDto {
  id: string; name: string; kind: 'footing' | 'slab' | 'base';
  own: { springs?: SpringValues | null; winkler?: number | null; compressionOnly?: boolean | null } | null;
  typical: { soil: string; min: number; max: number }[]; indicative: boolean;
  nodes: { node: string; at: number[]; k: number[]; held: boolean[]; compressionOnly: boolean }[];
}

/** The six springs a support holds in its attribute (null where it holds as it was). */
export function ownSprings(support: unknown): SpringValues {
  const s = (support as { springs?: unknown } | null | undefined)?.springs;
  return Array.isArray(s) && s.length === 6 ? s.map((x) => (typeof x === 'number' && Number.isFinite(x) ? x : null)) : [null, null, null, null, null, null];
}
export const ownWinkler = (support: unknown): number | null => {
  const w = (support as { winkler?: unknown } | null | undefined)?.winkler;
  return typeof w === 'number' && Number.isFinite(w) ? w : null;
};
export const ownCompressionOnly = (support: unknown): boolean => (support as { compressionOnly?: unknown } | null | undefined)?.compressionOnly === true;

/** A stiffness typed: empty = as the support holds it (null), 0 = free, else a number from 0 up; undefined when it does not read. */
export function parseSpring(text: string): number | null | undefined {
  const t = text.trim().replace(',', '.');
  if (t === '') return null;
  const v = Number(t);
  return Number.isFinite(v) && v >= 0 ? v : undefined;
}
/** The modulus of subgrade reaction typed (kN/m³, above 0); empty removes it (null); undefined when it does not read. */
export function parseWinkler(text: string): number | null | undefined {
  const t = text.trim().replace(',', '.');
  if (t === '') return null;
  const v = Number(t);
  return Number.isFinite(v) && v > 0 ? v : undefined;
}
/** Springs with one position changed; all empty gives null (the springs removed). */
export function withSpring(springs: SpringValues, index: number, value: number | null): SpringValues | null {
  const next = springs.slice();
  next[index] = value;
  return next.every((x) => x === null) ? null : next;
}
/** A stiffness as shown: a plain number, or a power of ten for a very large one. */
export function formatStiffness(k: number | null | undefined): string {
  if (k == null || !Number.isFinite(k)) return '–';
  if (k === 0) return '0';
  const a = Math.abs(k);
  return a >= 1e6 || a < 0.01 ? k.toExponential(2).replace('e+', 'e') : String(Math.round(k * 100) / 100);
}
/** A typical ks range in MN/m³ (the engine gives kN/m³): 5–15. */
export const formatSoilRange = (min: number, max: number): string => `${Math.round(min / 100) / 10}–${Math.round(max / 100) / 10} MN/m³`;

export type ActsIn = 'both' | 'tension' | 'compression';
export const ACTS_IN: [ActsIn, string][] = [['both', 'Both ways'], ['tension', 'Tension only'], ['compression', 'Compression only']];
export const actsIn = (only: unknown): ActsIn => (only === 'tension' || only === 'compression' ? only : 'both');
/** The analysis.setOnly argument for a choice. */
export const onlyArg = (a: ActsIn): 'tension' | 'compression' | null => (a === 'both' ? null : a);

/** The classes whose supports can take springs: footings, slabs on the ground and the steel module's column base node. */
export const SUPPORT_CLASSES = ['IfcFooting', 'IfcSlab', 'LapinSteelNode'] as const;
