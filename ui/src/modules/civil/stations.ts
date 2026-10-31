/**
 * Stations along an axis (a road's, a module's): the station text, the
 * kilometrage written and read as people type it, and what a corridor keeps
 * of its axis. Metres.
 */

import type { NodeDto } from '../../engine/protocol';

/** What the corridor keeps on a path element: its first station and the curve at each vertex ([radius, spiral in, spiral out]). */
export function corridorOf(n: NodeDto): { stationStart: number; curves: [number, number, number][] } {
  const c = n.props.corridor;
  const o = c && typeof c === 'object' && !Array.isArray(c) ? (c as Record<string, unknown>) : {};
  const stationStart = typeof o.stationStart === 'number' ? o.stationStart : 0;
  const curves = Array.isArray(o.curves)
    ? o.curves.map((v) => (Array.isArray(v) ? [Number(v[0]) || 0, Number(v[1]) || 0, Number(v[2]) || 0] as [number, number, number] : [0, 0, 0] as [number, number, number]))
    : [];
  return { stationStart, curves };
}

/** Station text: 1234.5 → 1+234.50. */
export const stationLabel = (s: number): string => { const km = Math.floor(Math.abs(s) / 1000 + 1e-9); return `${s < 0 ? '-' : ''}${km}+${(Math.abs(s) - km * 1000).toFixed(2).padStart(6, '0')}`; };

/** A station as a kilometrage (Stationing.FormatKm): 12345.6 → "12+345,600"; rounded first, so 999.9996 reads "1+000,000"; a negative station keeps its sign in front ("-0+012,500"). */
export function formatKm(station: number, decimals = 3, sep = ','): string {
  if (!Number.isFinite(station)) return '—';
  const f = 10 ** decimals;
  const units = Math.round(Math.abs(station) * f);
  const neg = station < 0 && units > 0;
  const km = Math.floor(units / (1000 * f));
  const rest = units - km * 1000 * f;
  const whole = Math.floor(rest / f), frac = rest - whole * f;
  const m = String(whole).padStart(3, '0') + (decimals > 0 ? sep + String(frac).padStart(decimals, '0') : '');
  return `${neg ? '-' : ''}${km}+${m}`;
}

const KM_PATTERN = /^(-?)(\d+)\+(\d+(?:[.,]\d*)?)$/;
const PLAIN_PATTERN = /^-?\d+(?:[.,]\d*)?$/;

/** What a person types for a station → metres (Stationing.ParseKm): "12+345,6", "km 12+345.600", "12345,6", "-0+012,5"; null when it is none of these (the metres part must be under 1000). */
export function parseKm(text: string): number | null {
  const t = text.trim().replace(/^km\s*/i, '').replace(/ /g, '');
  if (t.length === 0) return null;
  const m = KM_PATTERN.exec(t);
  if (m) {
    const metres = Number(m[3].replace(',', '.'));
    if (!Number.isFinite(metres) || metres >= 1000) return null;
    const v = Number(m[2]) * 1000 + metres;
    return m[1] ? -v : v;
  }
  if (!PLAIN_PATTERN.test(t)) return null;
  const p = Number(t.replace(',', '.'));
  return Number.isFinite(p) ? p : null;
}
