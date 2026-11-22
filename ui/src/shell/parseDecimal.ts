/** A number typed by a person: one parser for every plain numeric field (audit 22.11.2026, §4). */

export interface DecimalRules { min?: number; max?: number; integer?: boolean }

/**
 * A typed decimal: a comma or a point as the decimal mark, spaces ignored, a hyphen or a true minus (−) as the sign.
 * Null when the text is empty, is not one number (a sum, "1,2,3", "12abc", "Infinity") or breaks `rules`; a value
 * outside min/max is refused, not clamped (the caller clamps if it wants to).
 */
export function parseDecimal(text: string, rules: DecimalRules = {}): number | null {
  const t = text.replace(/[\s ]+/g, '').replace(/[−‒–]/g, '-').replace(',', '.');
  if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(t)) return null;
  const v = Number(t);
  if (!Number.isFinite(v)) return null;
  if (rules.integer && !Number.isInteger(v)) return null;
  if (rules.min !== undefined && v < rules.min) return null;
  if (rules.max !== undefined && v > rules.max) return null;
  return v === 0 ? 0 : v;
}

/** `value` held to [min, max]. */
export const clampDecimal = (value: number, rules: Pick<DecimalRules, 'min' | 'max'>): number =>
  Math.min(rules.max ?? Infinity, Math.max(rules.min ?? -Infinity, value));

/**
 * A number field leaving focus: a valid value other than the stored one goes to `apply`; a rejected one (not a number, or
 * outside the rules) puts the stored value back in the field, so the field never keeps text the model did not take.
 */
export function commitDecimalField(input: HTMLInputElement, stored: number, rules: DecimalRules, apply: (value: number) => void): void {
  const v = parseDecimal(input.value, rules);
  if (v === null) { input.value = String(stored); return; }
  if (v !== stored) apply(v);
}
