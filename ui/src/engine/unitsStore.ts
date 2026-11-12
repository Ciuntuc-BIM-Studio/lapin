/**
 * The project's units for the interface (the drafting standards' `units`):
 * lengths are shown and typed in millimetres, centimetres, metres, feet and
 * inches (12'-3 1/2") or inches. The engine keeps metres throughout; this is
 * only how the panels, the live input and the command line read and write.
 * A typed length may carry its own unit (240cm, 2.4m, 3'6", 42") and sums
 * and differences (3000+250).
 */

import { create } from 'zustand';

export type Unit = 'mm' | 'cm' | 'm' | 'ft-in' | 'in';
export const UNITS: Unit[] = ['mm', 'cm', 'm', 'ft-in', 'in'];

/** The steps a project may round the areas of its spaces to (decision 471); null: as measured. */
export type AreaStep = 0.1 | 0.5 | 1 | null;
export const AREA_STEPS: Exclude<AreaStep, null>[] = [0.1, 0.5, 1];

/** The scales a view's picker offers when the project's standards are not read yet (the engine's usual ones, decision 492). */
export const DEFAULT_VIEW_SCALES = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];

interface Standards { units: Unit; lineCap: 'round' | 'square'; areaRounding: AreaStep; scales: number[] }
export const useStandards = create<Standards>(() => ({ units: 'mm', lineCap: 'round', areaRounding: null, scales: DEFAULT_VIEW_SCALES }));

export const unit = (): Unit => useStandards.getState().units;
/** The unit's label after a shown length ('' for imperial, which carries its own marks). */
export const unitLabel = (u: Unit = unit()): string => (u === 'ft-in' || u === 'in' ? '' : u);

/** The unit's name in a prompt: "Depth (mm)", "Depth (ft-in)". */
export const unitName = (u: Unit = unit()): string => u;

const METRIC: Record<string, number> = { mm: 0.001, cm: 0.01, dm: 0.1, m: 1 };
const INCH = 0.0254;

/** Inches with their fraction to the nearest 1/16: 3 1/2", 1/32", 0". */
function inches(totalInches: number, denominator = 16): { whole: number; frac: string } {
  const n = Math.round(totalInches * denominator);
  let num = n % denominator, den = denominator;
  const whole = (n - num) / denominator;
  while (num > 0 && num % 2 === 0) { num /= 2; den /= 2; }
  return { whole, frac: num > 0 ? `${num}/${den}` : '' };
}

/** Tenths of a millimetre in a metre: the step a metric length is kept and shown to (report #94: a 12.5 mm gypsum board). */
const TENTHS = 10_000;

/** Metres rounded to a tenth of a millimetre, then in `perMetre` units: whole values without decimals (12, not 12.0), never "-0". */
const metric = (m: number, perMetre: number) => String(Math.round(m * TENTHS) / (TENTHS / perMetre) || 0);

/**
 * A length in metres as the project shows it (no label): metric to a tenth of
 * a millimetre (12.5 mm, 1.25 cm, 0.0125 m; whole values without decimals),
 * imperial to the nearest 1/16".
 */
export function formatLength(m: number, u: Unit = unit()): string {
  if (!Number.isFinite(m)) return '';
  switch (u) {
    case 'cm': return metric(m, 100);
    case 'm': return metric(m, 1);
    case 'in': {
      const { whole, frac } = inches(Math.abs(m) / INCH);
      return `${m < 0 ? '-' : ''}${whole === 0 && frac ? '' : whole}${whole && frac ? ' ' : ''}${frac}"`;
    }
    case 'ft-in': {
      const { whole, frac } = inches(Math.abs(m) / INCH);
      const ft = Math.floor(whole / 12), inch = whole % 12;
      const inchText = `${inch === 0 && frac ? '' : inch}${inch && frac ? ' ' : ''}${frac}"`;
      return `${m < 0 ? '-' : ''}${ft > 0 ? `${ft}'-` : ''}${inchText}`;
    }
    default: return metric(m, 1000);
  }
}

/**
 * A level (metres) in the project's units (report #68): metric projects write levels in metres, as drawings do (+3.00,
 * −0.50; ±0.00 with `zero`), imperial ones in feet and inches or inches (+10'-0", ±0"), as the engine draws them.
 */
export function formatLevel(z: number, u: Unit = unit(), zero = false): string {
  if (u === 'ft-in' || u === 'in') {
    const text = formatLength(Math.abs(z), u);
    return text === '0"' ? '±0"' : `${z > 0 ? '+' : '−'}${text}`;
  }
  if (zero && Math.abs(z) < 0.005) return '±0.00';
  return `${z >= 0 ? '+' : '−'}${Math.abs(z).toFixed(2)}`;
}

/** A length with its label: "2400 mm", "12'-3 1/2"". */
export const lengthText = (m: number, u: Unit = unit()): string => `${formatLength(m, u)}${unitLabel(u) ? ` ${unitLabel(u)}` : ''}`;

/** A feet-and-inches term: 12'-3 1/2", 3'6", 3', 42", 6 1/2", 3.5'. Metres, or null when it is not one. */
export function parseImperial(text: string): number | null {
  const t = text.replace(/\s+/g, ' ').trim().replace(/″/g, '"').replace(/′/g, "'");
  if (!/['"]/.test(t)) return null;
  const m = /^([+-]?)(?:(\d+(?:\.\d+)?)')?\s*-?\s*(?:(\d+(?:\.\d+)?)?(?:\s+(\d+)\/(\d+)|(?:(\d+)\/(\d+)))?"?)?$/.exec(t);
  if (!m || (m[2] == null && m[3] == null && m[4] == null && m[6] == null)) return null;
  const feet = m[2] ? Number(m[2]) : 0;
  const whole = m[3] ? Number(m[3]) : 0;
  const frac = m[4] ? Number(m[4]) / Number(m[5]) : m[6] ? Number(m[6]) / Number(m[7]) : 0;
  const total = feet * 12 + whole + frac;
  if (!Number.isFinite(total)) return null;
  return (m[1] === '-' ? -1 : 1) * total * INCH;
}

/** A typed length in the project's unit (unless it says its own), sums and differences allowed; metres, or null. */
export function parseLength(text: string, dflt: Unit = unit()): number | null {
  const raw = text.trim();
  if (!raw) return null;
  if (/['"″′]/.test(raw)) return parseImperial(raw);
  const t = raw.replace(/\s+/g, '').replace(/,/g, '.');
  const terms = t.match(/[+-]?[^+-]+/g);
  if (!terms) return null;
  let sum = 0;
  for (const term of terms) {
    const m = /^([+-]?)(\d+\.?\d*|\.\d+)(mm|cm|dm|m|in|ft)?$/.exec(term);
    if (!m) return null;
    const u = m[3] ?? (dflt === 'ft-in' ? 'ft' : dflt);
    const factor = u === 'in' ? INCH : u === 'ft' ? 12 * INCH : METRIC[u];
    sum += (m[1] === '-' ? -1 : 1) * Number(m[2]) * factor;
  }
  return sum;
}

const SQUARE_FEET = 10.763910417;

/** The project's area step, when it is one Lapin knows (a malformed one: none). */
export const areaStep = (): AreaStep => {
  const s = useStandards.getState().areaRounding;
  return AREA_STEPS.includes(s as Exclude<AreaStep, null>) ? s : null;
};

/** A value to the nearest `step`, half up (12.25 at 0.5 is 12.5; 12.5 at 1 is 13), as the engine's AreaRounding.ToStep. */
export function toStep(v: number, step: number): number {
  const n = Math.round((Math.round(v * 1e4) / 1e4 / step) * 1e9) / 1e9;
  return Math.round(Math.floor(n + 0.5) * step * 1e9) / 1e9;
}

/**
 * The area of a space (m²) as the project shows it (report #76, decision 471): to the hundredth of a square metre (the
 * tenth of a square foot in an imperial project), or rounded half up to the project's step: 12.5 m² and 12.0 m² at 0.5,
 * 12 m² at 1. Presentation only: the engine keeps and exports the exact area.
 */
export function areaText(m2: number, step: AreaStep = areaStep(), u: Unit = unit()): string {
  if (!Number.isFinite(m2)) return '';
  const imperial = u === 'ft-in' || u === 'in';
  const v = imperial ? m2 * SQUARE_FEET : m2;
  const shown = step ? toStep(v, step) : v;
  const decimals = step ? (step >= 1 ? 0 : 1) : imperial ? 1 : 2;
  return `${shown.toFixed(decimals)} ${imperial ? 'ft²' : 'm²'}`;
}

/** The unit levels are written in (report #68): metres in a metric project, the project's own unit in an imperial one. */
export const levelUnit = (u: Unit = unit()): Unit => (u === 'ft-in' || u === 'in' ? u : 'm');

/** A typed level (an elevation): metres in a metric project unless it says its own unit, feet and inches in an imperial one; metres, or null. */
export const parseLevel = (text: string, u: Unit = unit()): number | null => parseLength(text, levelUnit(u));
