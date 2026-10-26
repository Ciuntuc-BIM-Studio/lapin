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

interface Standards { units: Unit; lineCap: 'round' | 'square' }
export const useStandards = create<Standards>(() => ({ units: 'mm', lineCap: 'round' }));

export const unit = (): Unit => useStandards.getState().units;
/** The unit's label after a shown length ('' for imperial, which carries its own marks). */
export const unitLabel = (u: Unit = unit()): string => (u === 'ft-in' || u === 'in' ? '' : u);

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

/** A length in metres as the project shows it (no label). */
export function formatLength(m: number, u: Unit = unit()): string {
  if (!Number.isFinite(m)) return '';
  switch (u) {
    case 'cm': return String(Math.round(m * 1000) / 10);
    case 'm': return String(Math.round(m * 1000) / 1000);
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
    default: return String(Math.round(m * 1000));
  }
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
  const t = raw.replace(/\s+/g, '').replace(',', '.');
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
