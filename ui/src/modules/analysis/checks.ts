/**
 * Structural analysis, stage S4 (docs/plan-fem.md §12.7): the pure parts of the design checks' interface: the engine's reports
 * (analysis.checks, .check, .walls, .designOptions), the members grouped by the element they belong to, the utilisation's colour
 * and text, the masonry walls' table, and the design values typed in the properties. Indicative, as everything here.
 */

export interface GoverningDto { key: string; name: string; clause: string; combination: string; combinationName: string; utilisation: number }
/** One axis' buckling length (docs/plan-fem.md §14.24): the one used (m), where it came from, and both candidates. */
export interface LengthAxisDto { used: number; source: 'given' | 'k' | 'alpha' | string; how: string; kL: number; alpha?: number | null; alphaCr?: number | null; mode?: number | null }
export interface LengthsDto { fromAlpha: boolean; informative: boolean; combination?: string; combinationName?: string; y: LengthAxisDto; z: LengthAxisDto }
export interface CheckedDto {
  id: string; sourceId: string; name: string; material: string; grade?: string; kind: string; storeyId?: string | null;
  utilisation: number | null; ok: boolean; done?: boolean; governing?: GoverningDto | null; lengths?: LengthsDto | null;
}
export interface ReinforcementLayersDto { asxBottom: number; asyBottom: number; asxTop: number; asyTop: number }
export interface SurfaceCheckedDto extends CheckedDto { reinforcement?: ReinforcementLayersDto | null }
export interface ChecksDto {
  ok: boolean; indicative: boolean; ms?: number; members: CheckedDto[]; surfaces: SurfaceCheckedDto[]; walls: CheckedDto[]; notes: string[];
}

export interface CheckValueDto { name: string; value: number; unit: string }
export interface CheckLineDto {
  key: string; name: string; clause: string; utilisation: number | null; ok: boolean; done?: boolean;
  combination?: string; combinationName?: string; x?: number; values?: CheckValueDto[]; note?: string;
}
export interface BarReinforcementDto { bottom?: number; top?: number; total?: number; stirrups?: number; side?: number; unit?: string }
export interface ElementCheckDto extends CheckedDto {
  checks: CheckLineDto[]; notes?: string[]; reinforcement?: BarReinforcementDto | ReinforcementLayersDto | null;
}
export interface ElementChecksDto { id: string; indicative: boolean; members: ElementCheckDto[]; surfaces: ElementCheckDto[]; walls: ElementCheckDto[] }

export interface WallForceDto { value: number; combination?: string; combinationName?: string }
export interface WallDto {
  id: string; sourceId: string; name: string; direction: 'X' | 'Y' | '–' | string; length: number; thickness: number; height: number;
  utilisation: number | null; governing?: GoverningDto | null;
  N: WallForceDto; V: WallForceDto; M: WallForceDto; mOut?: number; NRd: number; NRdMiddle?: number; VRd: number; MRd: number;
  utilisations?: Record<string, number>;
}
export interface WallSumDto { walls: number; V: number; VRd: number; area: number }
export interface WallStoreyDto { id: string; name: string; walls: WallDto[]; sums: Record<string, WallSumDto> }
export interface WallsDto { ok: boolean; indicative: boolean; storeys: WallStoreyDto[]; notes: string[] }

export interface DesignOptionsDto {
  project: Record<string, number | string> | null;
  defaults: { serviceClass: number; cover: number; bar: number; stirrup: number; fyk: number; fvk0: number; gammaM: number; steelInteraction?: string; snowDuration?: string; bucklingLengths?: string };
  systems: string[];
  bucklingLengths?: string[];
}

// ── The buckling lengths (k·L or from αcr) ──

/** A length in metres, two decimals; nothing → "–". */
export function formatLength(v: number | null | undefined): string {
  return v == null || !Number.isFinite(v) ? '–' : v.toFixed(2);
}

/** The small marker of where a used length came from: "αcr", "k" or "given". */
export function lengthSource(a: Pick<LengthAxisDto, 'source'>): string {
  return a.source === 'alpha' ? 'αcr' : a.source === 'given' ? 'given' : 'k';
}

/** One axis as the tooltip tells it: k·L, the αcr one (with αcr and the mode), the one used and why. */
export function lengthAxisText(name: string, a: LengthAxisDto): string {
  const alpha = a.alpha != null && Number.isFinite(a.alpha)
    ? `${formatLength(a.alpha)} m (αcr = ${a.alphaCr != null ? +a.alphaCr.toFixed(2) : '–'}${a.mode != null ? `, mode ${a.mode}` : ''})` : '–';
  return `${name}: k·L = ${formatLength(a.kL)} m · αcr ${alpha} · used ${formatLength(a.used)} m (${lengthSource(a)})${a.how ? `\n${a.how}` : ''}`;
}

/** The tooltip of a lengths cell: both axes. */
export const lengthsTitle = (l: LengthsDto): string => `${lengthAxisText('y-y', l.y)}\n${lengthAxisText('z-z', l.z)}`;

// ── The utilisation: its text and its colour ──

/** 0.847 → "0.85"; nothing checked → "–". */
export function formatUtilisation(u: number | null | undefined): string {
  if (u == null || !Number.isFinite(u)) return '–';
  return u >= 10 ? '>10' : u.toFixed(2);
}

/** The colour of a utilisation: 0 green to 1 orange (through yellow), above 1 red, not checked grey. */
export function utilisationColour(u: number | null | undefined): string {
  if (u == null || !Number.isFinite(u)) return '#9aa0a6';
  if (u > 1) return '#d9202c';
  const hue = 120 - 85 * Math.max(0, u); // 120° green → 35° orange
  return `hsl(${hue.toFixed(0)}, 70%, 42%)`;
}

/** The legend's steps, from 0 to 1 and then over 1. */
export const UTILISATION_STEPS = [0, 0.25, 0.5, 0.75, 1] as const;

/** A checked one with nothing it could check is grey; what was not done is not "ok". */
export const isChecked = (c: Pick<CheckedDto, 'utilisation' | 'done'>): boolean => c.utilisation != null && c.done !== false;

// ── The members, by the element they belong to ──

export interface ElementRow {
  sourceId: string; name: string; material: string; grade: string; kind: string; storeyId: string | null; pieces: number;
  utilisation: number | null; ok: boolean; governing: GoverningDto | null; lengths: LengthsDto | null;
}

/** Pieces of the analytical model share their element's id (sourceId): one row an element, its worst piece's utilisation, the worst first, the unchecked last. */
export function groupBySource(members: readonly CheckedDto[]): ElementRow[] {
  const by = new Map<string, ElementRow>();
  for (const m of members) {
    const u = isChecked(m) ? m.utilisation : null;
    const row = by.get(m.sourceId);
    if (!row) {
      by.set(m.sourceId, { sourceId: m.sourceId, name: m.name, material: m.material, grade: m.grade ?? '', kind: m.kind, storeyId: m.storeyId ?? null, pieces: 1, utilisation: u, ok: m.ok, governing: m.governing ?? null, lengths: m.lengths ?? null });
      continue;
    }
    row.pieces++;
    row.ok = row.ok && m.ok;
    if (u !== null && (row.utilisation === null || u > row.utilisation)) { row.utilisation = u; row.governing = m.governing ?? row.governing; row.lengths = m.lengths ?? row.lengths; }
    else if (!row.lengths && m.lengths) row.lengths = m.lengths;
  }
  return [...by.values()].sort((a, b) => (b.utilisation ?? -1) - (a.utilisation ?? -1) || a.name.localeCompare(b.name));
}

export const MATERIALS = ['steel', 'concrete', 'timber', 'masonry'] as const;
export const MATERIAL_LABEL: Record<string, string> = { steel: 'Steel', concrete: 'Concrete', timber: 'Timber', masonry: 'Masonry' };

/** The rows kept by a material and a storey ('' keeps all). */
export function filterRows<T extends { material: string; storeyId?: string | null }>(rows: readonly T[], material: string, storeyId: string): T[] {
  return rows.filter((r) => (!material || r.material === material) && (!storeyId || r.storeyId === storeyId));
}

/** The storeys the checked ones stand on, in the order met, with their names from `names`. */
export function storeysOf(rows: readonly { storeyId?: string | null }[], names: ReadonlyMap<string, string>): { id: string; name: string }[] {
  const ids = [...new Set(rows.map((r) => r.storeyId).filter((x): x is string => !!x))];
  return ids.map((id) => ({ id, name: names.get(id) ?? id }));
}

/** How many of the rows are over 1, and the worst. */
export function summary(rows: readonly { utilisation: number | null }[]): { checked: number; over: number; worst: number | null } {
  let checked = 0, over = 0, worst: number | null = null;
  for (const r of rows) {
    if (r.utilisation == null) continue;
    checked++;
    if (r.utilisation > 1) over++;
    if (worst === null || r.utilisation > worst) worst = r.utilisation;
  }
  return { checked, over, worst };
}

/** An element's utilisation for colouring (the worst of its pieces), by its id; slabs, walls and bars alike. */
export function utilisationBySource(c: ChecksDto): Map<string, number | null> {
  const out = new Map<string, number | null>();
  for (const m of [...c.members, ...c.surfaces, ...c.walls]) {
    const u = isChecked(m) ? m.utilisation : null;
    const had = out.get(m.sourceId);
    if (had === undefined || (u !== null && (had === null || u > had))) out.set(m.sourceId, u);
  }
  return out;
}

// ── The slabs' and walls' reinforcement ──

export const LAYERS = ['asxBottom', 'asyBottom', 'asxTop', 'asyTop'] as const;
export type Layer = typeof LAYERS[number];
export const LAYER_LABEL: Record<Layer, string> = { asxBottom: 'Bottom x', asyBottom: 'Bottom y', asxTop: 'Top x', asyTop: 'Top y' };

export interface SurfaceRow extends ElementRow { surfaceKind: string; layers: ReinforcementLayersDto | null }

/** The slabs and walls by element: the largest required area of each layer over their pieces (cm²/m). */
export function surfaceRows(surfaces: readonly SurfaceCheckedDto[]): SurfaceRow[] {
  const rows = groupBySource(surfaces) as SurfaceRow[];
  const by = new Map<string, SurfaceCheckedDto[]>();
  for (const s of surfaces) by.set(s.sourceId, [...(by.get(s.sourceId) ?? []), s]);
  for (const r of rows) {
    const mine = by.get(r.sourceId) ?? [];
    r.surfaceKind = mine[0]?.kind ?? '';
    const withRebar = mine.filter((s) => s.reinforcement);
    r.layers = withRebar.length === 0 ? null : {
      asxBottom: Math.max(...withRebar.map((s) => s.reinforcement!.asxBottom)), asyBottom: Math.max(...withRebar.map((s) => s.reinforcement!.asyBottom)),
      asxTop: Math.max(...withRebar.map((s) => s.reinforcement!.asxTop)), asyTop: Math.max(...withRebar.map((s) => s.reinforcement!.asyTop)),
    };
  }
  return rows;
}

/** An area in cm² (or cm²/m), two decimals up to 10, then one; nothing → "–". */
export function formatArea(a: number | null | undefined): string {
  if (a == null || !Number.isFinite(a)) return '–';
  return a < 10 ? a.toFixed(2) : a.toFixed(1);
}

// ── The masonry walls' table, per storey ──

export interface WallRow {
  id: string; sourceId: string; name: string; direction: string; length: string; thickness: string;
  N: string; V: string; M: string; nTitle: string; vTitle: string; mTitle: string; NRd: string; VRd: string; MRd: string;
  utilisation: number | null; governing: string;
}
export interface WallSumRow { direction: string; walls: number; V: string; VRd: string; area: string }

const num = (v: number | null | undefined, digits = 1): string => (v == null || !Number.isFinite(v) ? '–' : v.toFixed(digits));
const combo = (f: WallForceDto | undefined): string => f?.combinationName || f?.combination || '';

/** A storey's walls as the table writes them (kN, kN·m, m), the worst first. */
export function wallRows(storey: WallStoreyDto): WallRow[] {
  return [...storey.walls].sort((a, b) => (b.utilisation ?? -1) - (a.utilisation ?? -1) || a.name.localeCompare(b.name)).map((w) => ({
    id: w.id, sourceId: w.sourceId, name: w.name, direction: w.direction, length: num(w.length, 2), thickness: num(w.thickness, 2),
    N: num(w.N?.value), V: num(w.V?.value), M: num(w.M?.value), nTitle: combo(w.N), vTitle: combo(w.V), mTitle: combo(w.M),
    NRd: num(w.NRd), VRd: num(w.VRd), MRd: num(w.MRd), utilisation: w.utilisation ?? null,
    governing: w.governing ? `${w.governing.name}${w.governing.clause ? ` (${w.governing.clause})` : ''}` : '',
  }));
}

/** The sums per direction of a storey: ΣV against ΣVRd, only the directions that have walls. */
export function wallSums(storey: WallStoreyDto): WallSumRow[] {
  return (['X', 'Y'] as const).flatMap((d) => {
    const s = storey.sums?.[d];
    return s && s.walls > 0 ? [{ direction: d, walls: s.walls, V: num(s.V), VRd: num(s.VRd), area: num(s.area, 2) }] : [];
  });
}

// ── An element's checks, in its properties ──

/** The values of a check as one line: "NEd = 120.5 kN · NRd = 340 kN". */
export function valuesText(values: readonly CheckValueDto[] | undefined): string {
  return (values ?? []).map((v) => `${v.name} = ${Number.isFinite(v.value) ? +v.value.toPrecision(4) : '–'}${v.unit ? ` ${v.unit}` : ''}`).join(' · ');
}

/** The required reinforcement of a bar as lines of text, only what the engine gave. */
export function barReinforcementText(r: BarReinforcementDto): { label: string; value: string }[] {
  const u = r.unit ?? 'cm²';
  const out: { label: string; value: string }[] = [];
  const add = (label: string, v: number | undefined, unit = u) => { if (v != null && Number.isFinite(v)) out.push({ label, value: `${formatArea(v)} ${unit}` }); };
  add('Bottom', r.bottom); add('Top', r.top); add('Total', r.total); add('Side', r.side);
  add('Stirrups', r.stirrups, `${u}/m`);
  return out;
}

// ── The design values ──

export interface OwnDesign { ky?: number; kz?: number; lcrY?: number; lcrZ?: number; llt?: number; serviceClass?: number; system?: string; bucklingLengths?: string }

/** The element's own design values as the attribute holds them (anything else dropped). */
export function ownDesign(attr: unknown): OwnDesign {
  if (!attr || typeof attr !== 'object') return {};
  const a = attr as Record<string, unknown>;
  const n = (k: string): number | undefined => (typeof a[k] === 'number' && Number.isFinite(a[k]) ? (a[k] as number) : undefined);
  return { ky: n('ky'), kz: n('kz'), lcrY: n('lcrY'), lcrZ: n('lcrZ'), llt: n('llt'), serviceClass: n('serviceClass'), system: typeof a.system === 'string' ? a.system : undefined, bucklingLengths: typeof a.bucklingLengths === 'string' ? a.bucklingLengths : undefined };
}

/** A number typed: empty is null (the default again), a number as typed (a comma too), anything else undefined (not applied). */
export function parseDesignValue(text: string): number | null | undefined {
  const s = text.trim().replace(',', '.');
  if (s === '') return null;
  const v = Number(s);
  return Number.isFinite(v) ? v : undefined;
}

export const SYSTEM_LABEL: Record<string, string> = {
  simple: 'Simply supported', end: 'End span', interior: 'Interior span', cantilever: 'Cantilever', flat: 'Flat slab',
};
export const DESIGN_CLASSES = ['IfcSlab', 'IfcWall', 'IfcWallStandardCase'] as const;

/** The project's design values as the fields write them (mm, MPa) and back (m, Pa). */
export const PROJECT_FIELDS = [
  { key: 'cover', label: 'Cover (mm)', scale: 1000, title: 'The concrete cover to the stirrups' },
  { key: 'bar', label: 'Main bars (mm)', scale: 1000, title: 'The main bars\' diameter' },
  { key: 'stirrup', label: 'Stirrups (mm)', scale: 1000, title: 'The stirrups\' diameter' },
  { key: 'fyk', label: 'fyk (MPa)', scale: 1e-6, title: 'The reinforcement\'s characteristic yield strength' },
  { key: 'fvk0', label: 'fvk0 (MPa)', scale: 1e-6, title: 'The masonry\'s initial shear strength (CR 6 Table 4.5, by the mortar)' },
  { key: 'gammaM', label: 'γM (masonry)', scale: 1, title: 'The masonry\'s partial factor' },
] as const;
export type ProjectKey = typeof PROJECT_FIELDS[number]['key'];

/** A project value in the field's unit, as text (3 significant digits at most). */
export const projectText = (v: number | undefined, scale: number): string => (v === undefined ? '' : String(+(v * scale).toPrecision(4)));
/** What a typed project value sends: null (default), a value in the engine's unit, or undefined (not a number). */
export function projectValue(text: string, scale: number): number | null | undefined {
  const v = parseDesignValue(text);
  return v == null ? v : scale < 1 ? Math.round(v / scale) : v / scale;
}
