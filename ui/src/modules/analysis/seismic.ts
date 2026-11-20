/**
 * Structural analysis, stage S3c (docs/plan-fem.md §11.5): the pure parts of the seismic section of the results: the engine's
 * reports (analysis.seismicOptions, .modal, .spectrum, .seismic, .mode), the modes' and storeys' tables, the spectrum chart's
 * scaling and the mode shape's interpolation for its animation.
 */

export interface SeismicValuesDto {
  code: string; ag: number | null; tc: number; ground: string; spectrum: number; importance: string; gamma: number; q: number; damping: number;
  modal: string; directions: string; torsion: number; modes: number; cracked: { concrete: number; masonry: number }; diaphragm: string;
  drift: { sls: number; uls: number; nu: number }; snowPsi2: number; given: boolean;
}
export interface SeismicOptionsDto extends SeismicValuesDto {
  defaults: SeismicValuesDto;
  choices: { code: string[]; tc: number[]; ground: string[]; importance: string[]; modal: string[]; directions: string[]; diaphragm: string[] };
  masonryQ: { kind: string; regular: number; irregular: number }[];
  indicative: string;
}

export interface ModeRowDto {
  n: number; omega: number; f: number; T: number; gamma: number[]; mass: number[]; ratio: number[]; cumulative: number[]; rayleigh: number; residual: number;
}
export interface ModalDto {
  ok: boolean; ms: number; modes: ModeRowDto[]; totalMass: number[]; supportMass: number; reached: boolean; dofs: number; notes: string[];
}
export interface SpectrumDto {
  code: string; ag: number; S: number; TB: number; TC: number; TD: number; q: number; gamma: number; given: boolean; unit: string;
  points: { T: number; Sd: number; Se: number }[]; modes: { n: number; T: number; Sd: number }[];
}
export interface StoreyDto {
  id: string; name: string; z: number; h: number; mass: number; shear: number[]; force: number[]; drift: number[]; driftMax: number[];
  sls: number[]; uls: number[]; slsLimit: number; ulsLimit: number | null; slsOk: boolean; ulsOk: boolean | null;
}
export interface SeismicDto {
  ok: boolean; combination: string | null; base?: number[]; equivalent?: number[]; T1?: number[]; storeys?: StoreyDto[]; closeModes?: number; notes: string[];
}
export interface ModeShapeDto {
  mode: number; T: number; f: number; ratio: number[]; scale: number; max: number;
  polylines: { frame: string; sourceId: string; base: number[][]; points: number[][] }[];
  shells: { id: string; sourceId: string; positions: number[]; displaced: number[]; faces: number[] }[];
}

// ── The parameters' labels ──

export const CODE_LABEL: Record<string, string> = { 'P100-1/2013': 'P100-1/2013 (Romania)', 'EN 1998-1': 'EN 1998-1 (Eurocode 8)' };
export const MODAL_LABEL: Record<string, string> = { cqc: 'CQC (complete quadratic)', srss: 'SRSS (square root of the sum of squares)' };
export const DIRECTIONS_LABEL: Record<string, string> = { '30%': '30 % rule (Ex + 0.3 Ey, 0.3 Ex + Ey)', srss: 'SRSS (√(Ex² + Ey²))' };
export const DIAPHRAGM_LABEL: Record<string, string> = { none: 'None (shell floors give it)', rigid: 'Rigid, on levels without a shell floor' };
export const MASONRY_KIND_TITLE: Record<string, string> = {
  ZNA: 'Unreinforced masonry', ZC: 'Confined masonry', 'ZC+AR': 'Confined and reinforced masonry', ZIA: 'Reinforced masonry',
};

/** A number typed in a field (a comma or a dot for the decimals), within a range; null when it does not read. */
export function readNumber(text: string, min: number, max: number): number | null {
  const v = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(v) && v >= min && v <= max ? v : null;
}

// ── The tables ──

/** A fraction of the mass as a percentage, one decimal: 0.6234 → "62.3 %". */
export const formatPercent = (v: number): string => (Number.isFinite(v) ? `${(v * 100).toFixed(1)} %` : '–');
/** A period in seconds: three decimals under 1 s, two above. */
export const formatPeriod = (t: number): string => (!Number.isFinite(t) ? '–' : t < 1 ? t.toFixed(3) : t.toFixed(2));
/** A frequency in hertz, two decimals. */
export const formatHz = (f: number): string => (Number.isFinite(f) ? f.toFixed(2) : '–');
/** A length in metres written in millimetres, one decimal (two under 1 mm). */
export const formatDriftMm = (m: number): string => {
  const mm = m * 1000;
  return !Number.isFinite(mm) ? '–' : Math.abs(mm) < 1 ? mm.toFixed(2) : mm.toFixed(1);
};
/** A force (kN) or a mass (t): no decimals from 100 up, one under. */
export const formatQty = (v: number): string => (!Number.isFinite(v) ? '–' : Math.abs(v) >= 100 ? v.toFixed(0) : v.toFixed(1));

export interface ModeRow { n: number; T: string; f: string; x: string; y: string; cumX: string; cumY: string }
/** The modes table's rows as it writes them (every mode, in order). */
export const modeRows = (m: ModalDto): ModeRow[] => m.modes.map((x) => ({
  n: x.n, T: formatPeriod(x.T), f: formatHz(x.f), x: formatPercent(x.ratio[0]!), y: formatPercent(x.ratio[1]!), cumX: formatPercent(x.cumulative[0]!), cumY: formatPercent(x.cumulative[1]!),
}));
/** Where the modes stand at the end: the cumulative effective masses along X and Y (nothing: both 0). */
export const reachedMass = (m: ModalDto): [number, number] => {
  const last = m.modes[m.modes.length - 1];
  return last ? [last.cumulative[0]!, last.cumulative[1]!] : [0, 0];
};

export interface StoreyRow {
  id: string; name: string; h: string; mass: string; shearX: string; shearY: string; driftX: string; driftY: string;
  sls: string; slsOk: boolean; slsTitle: string; uls: string; ulsOk: boolean | null; ulsTitle: string;
}
/** The storeys table's rows: the elastic drift and the checks' values (mm) against their limits; the ULS check only with P100 (its limit is null with EN). */
export const storeyRows = (storeys: StoreyDto[]): StoreyRow[] => storeys.map((s) => ({
  id: s.id, name: s.name, h: s.h.toFixed(2), mass: formatQty(s.mass), shearX: formatQty(s.shear[0]!), shearY: formatQty(s.shear[1]!),
  driftX: formatDriftMm(s.drift[0]!), driftY: formatDriftMm(s.drift[1]!),
  sls: `${formatDriftMm(s.sls[0]!)} · ${formatDriftMm(s.sls[1]!)}`, slsOk: s.slsOk, slsTitle: `${formatDriftMm(s.slsLimit)} mm`,
  uls: s.ulsLimit == null ? '–' : `${formatDriftMm(s.uls[0]!)} · ${formatDriftMm(s.uls[1]!)}`, ulsOk: s.ulsOk, ulsTitle: s.ulsLimit == null ? '' : `${formatDriftMm(s.ulsLimit)} mm`,
}));

// ── The spectrum chart ──

/** A step for an axis that reaches `max` in about `count` ticks: 1, 2, 2.5 or 5 times a power of ten. */
export function niceStep(max: number, count = 5): number {
  if (!(max > 0)) return 1;
  const raw = max / count;
  const p = 10 ** Math.floor(Math.log10(raw));
  const f = raw / p;
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p;
}
/** The ticks of an axis from 0 up to the first one at or past `max`. */
export function ticks(max: number, count = 5): number[] {
  const step = niceStep(max, count);
  const out: number[] = [];
  for (let i = 0; i < 1000; i++) {
    const v = Number((i * step).toPrecision(10));
    out.push(v);
    if (v >= max) break;
  }
  return out;
}

export interface SpectrumChart {
  width: number; height: number; left: number; top: number; right: number; bottom: number;
  xMax: number; yMax: number;
  x: (t: number) => number; y: (v: number) => number;
  sd: string; se: string;
  xTicks: { v: number; at: number }[]; yTicks: { v: number; at: number }[];
  modes: { n: number; cx: number; cy: number }[];
  corners: { name: 'TB' | 'TC' | 'TD'; at: number }[];
}
/** Scales the spectrum into an SVG box: T along the bottom (to the last point), Sd and Se up the side (m/s²), the modes on the design spectrum. */
export function spectrumChart(s: SpectrumDto, width = 520, height = 240): SpectrumChart {
  const left = 46, right = 12, top = 12, bottom = 30;
  const xMax = Math.max(0.5, ...s.points.map((p) => p.T), ...s.modes.map((m) => m.T));
  const top0 = Math.max(1e-6, ...s.points.map((p) => Math.max(p.Sd, p.Se)));
  const yTicks = ticks(top0);
  const yMax = yTicks[yTicks.length - 1] ?? top0;
  const xTicks = ticks(xMax);
  const xEnd = xTicks[xTicks.length - 1] ?? xMax;
  const x = (t: number) => left + (t / xEnd) * (width - left - right);
  const y = (v: number) => height - bottom - (v / yMax) * (height - top - bottom);
  const path = (key: 'Sd' | 'Se') => s.points.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(p.T).toFixed(1)} ${y(p[key]).toFixed(1)}`).join('');
  return {
    width, height, left, top, right, bottom, xMax: xEnd, yMax, x, y, sd: path('Sd'), se: path('Se'),
    xTicks: xTicks.map((v) => ({ v, at: x(v) })), yTicks: yTicks.map((v) => ({ v, at: y(v) })),
    modes: s.modes.map((m) => ({ n: m.n, cx: x(m.T), cy: y(m.Sd) })),
    corners: [{ name: 'TB' as const, at: x(s.TB) }, { name: 'TC' as const, at: x(s.TC) }, { name: 'TD' as const, at: x(s.TD) }].filter((c) => c.at <= width - right),
  };
}

// ── The mode shape's animation ──

/** The bar's points at rest: its ends' line cut into `count` equal pieces (the engine's points are the displaced ones). */
export function restPoints(base: number[][], count: number): number[] {
  const [a, b] = base as [number[], number[]];
  const out: number[] = [];
  for (let i = 0; i < count; i++) {
    const t = count > 1 ? i / (count - 1) : 0;
    out.push(a[0]! + (b[0]! - a[0]!) * t, a[1]! + (b[1]! - a[1]!) * t, a[2]! + (b[2]! - a[2]!) * t);
  }
  return out;
}
/** Rest plus k times the way to the displaced shape (k in −1…1: the oscillation), flat x, y, z. */
export function blend(rest: ArrayLike<number>, displaced: ArrayLike<number>, k: number, out?: Float32Array): Float32Array {
  const r = out ?? new Float32Array(rest.length);
  for (let i = 0; i < rest.length; i++) r[i] = rest[i]! + (displaced[i]! - rest[i]!) * k;
  return r;
}
/** The oscillation at a moment: sin(2πt / period), t and period in milliseconds. */
export const oscillation = (ms: number, period = 1600): number => Math.sin((2 * Math.PI * ms) / period);
