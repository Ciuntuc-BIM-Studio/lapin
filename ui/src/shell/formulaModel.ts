/**
 * A parameter worked out by a formula (docs/plan-user-extensibility.md §5.3), the pure side: which kinds may have one, the switch
 * between a value and a formula (what is written each way), what the live try says, and the names and functions the help lists.
 * The engine keeps the formula as written and works the value out on every ask.
 */

import type { Param } from './params';

/** A formula gives a number or a yes/no: text and choices stay values. */
export const FORMULA_KINDS = ['number', 'length', 'area', 'angle', 'bool'];
export const canFormula = (kind: string): boolean => FORMULA_KINDS.includes(kind);

export const FORMULA_FUNCTIONS = ['min', 'max', 'round', 'floor', 'ceil', 'sqrt', 'sin', 'cos', 'tan', 'atan2', 'if', 'clamp'];
/** Quantities a formula may name where the engine has them for the element. */
export const FORMULA_QUANTITIES = ['Length', 'Width', 'Height', 'GrossArea', 'NetArea', 'GrossVolume', 'NetVolume'];

export type Mode = 'value' | 'formula';
export interface FormulaState { mode: Mode; text: string }

/** A parameter's mode as saved: a formula one when it has a formula. */
export const modeOf = (p: Pick<Param, 'kind'> & { formula?: string | null }): Mode => (p.formula ? 'formula' : 'value');

/** The state a parameter starts with in its row. */
export const initialState = (p: Pick<Param, 'kind' | 'value'> & { formula?: string | null }): FormulaState => ({ mode: modeOf(p), text: p.formula ?? '' });

/** The state after the switch is pressed: to a formula, starting from the number the value was; back to a value, keeping the text for a return. */
export function toggled(state: FormulaState, p: Pick<Param, 'kind' | 'value'>): FormulaState {
  if (state.mode === 'formula') return { mode: 'value', text: state.text };
  const v = p.value;
  const text = state.text || (typeof v === 'number' ? String(v) : typeof v === 'boolean' ? (v ? '1' : '0') : '');
  return { mode: 'formula', text };
}

/** What `element.setParam` is given to make a parameter a formula (its kind kept; a set when it has one). */
export function formulaArgs(ids: string[], p: Pick<Param, 'name' | 'kind' | 'pset'>, formula: string): Record<string, unknown> {
  return { ids, name: p.name, formula: formula.trim(), kind: p.kind, ...(p.pset ? { pset: p.pset } : {}) };
}

/** What it is given to make it a plain value again: the value the formula gave (or the kind's empty one when it gave none). */
export function valueArgs(ids: string[], p: Pick<Param, 'name' | 'kind' | 'pset'>, computed: number | null | undefined): Record<string, unknown> {
  const value = computed != null ? (p.kind === 'bool' ? computed !== 0 : computed) : p.kind === 'bool' ? false : 0;
  return { ids, name: p.name, kind: p.kind, value, ...(p.pset ? { pset: p.pset } : {}) };
}

/** A result written for its kind: a yes/no, an angle in degrees, an area, a number; `length` formats the metres in the project's unit. */
export function resultText(value: number | null | undefined, kind: string, length: (m: number) => string): string {
  if (value == null) return '';
  switch (kind) {
    case 'bool': return value !== 0 ? 'yes' : 'no';
    case 'length': return length(value);
    case 'area': return `${Math.round(value * 1e4) / 1e4} m²`;
    case 'angle': return `${Math.round(value * 1e4) / 1e4}°`;
    default: return String(Math.round(value * 1e6) / 1e6);
  }
}

/** Names a formula may use that the element has: its parameters (the others named too) and its numeric attributes. */
export function usableNames(params: { name: string }[], attrs: Record<string, unknown>, self?: string): string[] {
  const numeric = Object.entries(attrs).filter(([, v]) => typeof v === 'number' && Number.isFinite(v)).map(([k]) => k);
  return [...new Set([...params.map((x) => x.name).filter((n) => n !== self), ...numeric])];
}

/** Whether a live try's answer is worth sending: not an empty formula, and not the one already saved. */
export const worthTrying = (text: string, saved: string | null | undefined): boolean => text.trim().length > 0 && text.trim() !== (saved ?? '').trim();
