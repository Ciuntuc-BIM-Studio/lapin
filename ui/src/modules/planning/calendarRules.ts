/** The work calendar's input rules, apart from the dialog so they are tested. */

/** A year typed: whole, 1900 to 2099; null for anything else. */
export function parseYear(text: string): number | null {
  const t = text.trim();
  if (!/^\d{4}$/.test(t)) return null;
  const n = Number(t);
  return n >= 1900 && n <= 2099 ? n : null;
}

/** The working days after a weekday is ticked or unticked; null when that would leave none (a calendar needs a working day). */
export function toggleDay(days: readonly number[], day: number, on: boolean): number[] | null {
  const next = on ? [...new Set([...days, day])].sort() : days.filter((d) => d !== day);
  return next.length ? next : null;
}

/** An exception's last day, never before its first (ISO dates compare as text). */
export const lastDay = (from: string, to: string): string => (to < from ? from : to);
