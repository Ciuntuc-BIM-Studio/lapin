/**
 * The part's parameter table, flex and the published parameters in Properties, apart from React (docs/plan-studio-parametric.md §3.2
 * and §3.3, stage P1): the rows grouped, which cell of a row is editable and with what, a typed cell read as a value or a formula,
 * what flex has changed and what Keep sends. Tested in partParamModel.test.ts.
 */

export type ParamKind = 'length' | 'number' | 'angle' | 'bool' | 'text' | 'integer';
export const PARAM_KINDS: ParamKind[] = ['length', 'number', 'angle', 'bool', 'text', 'integer'];

/** One parameter as `studio.partTree` lists it. */
export interface ParamDto {
  key: string; label: string; kind: ParamKind; default: number | string; value: number | string; formula?: string; derived?: boolean;
  instance: boolean; published: boolean; unit?: string; group?: string; description?: string; min?: number; max?: number;
}
/** One entry of `studio.partValues`: where the object's value comes from. */
export interface ValueDto { key: string; value: number | string; source: 'default' | 'instance' | 'formula' | 'type'; problem?: string }

/** Derived: a formula gives its value (an engine's `derived` flag or a formula alone). */
export const isDerived = (p: Pick<ParamDto, 'formula' | 'derived'>): boolean => !!p.derived || (typeof p.formula === 'string' && p.formula !== '');

export interface ParamGroup { group: string; rows: ParamDto[] }

/** The rows grouped by `group`: the ungrouped first, then the groups in the order they first appear; each keeps the engine's order. */
export function groupParams(params: readonly ParamDto[]): ParamGroup[] {
  const groups: ParamGroup[] = [];
  for (const p of params) {
    const g = p.group ?? '';
    let at = groups.find((x) => x.group === g);
    if (!at) { at = { group: g, rows: [] }; groups.push(at); }
    at.rows.push(p);
  }
  return groups.sort((a, b) => (a.group === '' ? -1 : b.group === '' ? 1 : 0));
}

/** The unit a parameter is shown with: its own, or degrees for an angle; a length is shown in the project's unit by its field. */
export const unitOf = (p: Pick<ParamDto, 'kind' | 'unit'>): string => p.unit ?? (p.kind === 'angle' ? '°' : '');

/** What the two value cells of a row can do. */
export interface Cells {
  /** The default column: a value, or the formula for a derived parameter. */
  defaultCell: 'value' | 'formula';
  /** This object's own column: edited for an instance parameter, nothing for a type or a derived one (it follows). */
  ownCell: 'edit' | 'none';
  /** The row's own value comes from the object (a type parameter shows only the default; a derived one is worked out). */
  showsOwn: boolean;
}
export function cellsOf(p: ParamDto): Cells {
  const derived = isDerived(p);
  return { defaultCell: derived ? 'formula' : 'value', ownCell: !derived && p.instance ? 'edit' : 'none', showsOwn: !derived && p.instance };
}

/** What a typed cell means. */
export type CellInput = { formula: string } | { value: number | string } | { clear: true } | 'bad';

/**
 * A default's text: `=…` is a formula of the other parameters, else a value of the kind (a length through `length`, a number or an
 * angle read with decimals, a bool as yes/no/1/0/true/false, a text as typed). An empty text clears a formula (kind-less).
 */
export function parseCell(text: string, kind: ParamKind, length: (t: string) => number | null): CellInput {
  const t = text.trim();
  if (t.startsWith('=')) { const f = t.slice(1).trim(); return f ? { formula: f } : 'bad'; }
  if (kind === 'text') return { value: text };
  if (!t) return { clear: true };
  if (kind === 'bool') {
    if (/^(yes|true|1|da)$/i.test(t)) return { value: 1 };
    if (/^(no|false|0|nu)$/i.test(t)) return { value: 0 };
    return 'bad';
  }
  if (kind === 'length') { const n = length(t); return n != null && Number.isFinite(n) ? { value: n } : 'bad'; }
  const n = Number(t.replace(',', '.'));
  if (!Number.isFinite(n) || t === '') return 'bad';
  return { value: kind === 'integer' ? Math.round(n) : n };
}

/** The `changes` of `studio.updatePartParam` for a default's text: a value sets the default and drops a formula, a formula sets it. */
export function defaultChanges(input: CellInput, derived: boolean): Record<string, unknown> | null {
  if (input === 'bad' || 'clear' in input) return null;
  if ('formula' in input) return { formula: input.formula };
  return derived ? { default: input.value, formula: null } : { default: input.value };
}

/** A value as the table shows it (a length in the project's unit is left to the caller). */
export function plainValue(v: number | string | undefined, kind: ParamKind): string {
  if (v == null) return '';
  if (kind === 'bool') return Number(v) ? 'yes' : 'no';
  if (typeof v === 'number') return String(Math.round(v * 1e6) / 1e6);
  return v;
}

/** The part's position moved by a row's arrows: the target index, or null at the ends. */
export function moveTarget(params: readonly ParamDto[], key: string, dir: -1 | 1): number | null {
  const i = params.findIndex((p) => p.key === key), to = i + dir;
  return i < 0 || to < 0 || to >= params.length ? null : to;
}

/** A free key for a new parameter: P1, P2…; or the base with a number. */
export function freeKey(params: readonly ParamDto[], base = 'P'): string {
  const taken = new Set(params.map((p) => p.key));
  for (let i = 1; ; i++) if (!taken.has(`${base}${i}`)) return `${base}${i}`;
}

/** A new parameter's definition for a kind. */
export function newParam(params: readonly ParamDto[], kind: ParamKind): Record<string, unknown> {
  const key = freeKey(params, kind === 'length' ? 'L' : kind === 'bool' ? 'B' : kind === 'angle' ? 'A' : kind === 'text' ? 'T' : 'N');
  return { key, label: key, kind, default: kind === 'text' ? '' : kind === 'length' ? 0.1 : kind === 'bool' ? 0 : 0, instance: true, published: true };
}

// ---------------------------------------------------------------- flex

/** Flex: values changed for a preview only, until Keep or Put back. `values` holds what was typed, by key. */
export interface FlexState { on: boolean; values: Record<string, number | string> }
export const FLEX_OFF: FlexState = { on: false, values: {} };

/** The value a parameter shows while flexing: the flexed one, else the object's (instance) or the default (type). */
export function flexBase(p: ParamDto): number | string { return p.instance && !isDerived(p) ? p.value : p.default; }
export function flexShown(p: ParamDto, s: FlexState): number | string { return p.key in s.values ? s.values[p.key]! : flexBase(p); }

/** The keys flexed to something other than what they are. */
export function flexChanged(params: readonly ParamDto[], s: FlexState): string[] {
  return params.filter((p) => !isDerived(p) && p.key in s.values && String(s.values[p.key]) !== String(flexBase(p))).map((p) => p.key);
}
/** What a preview is asked with: the changed values (or null when nothing changed, so there is nothing to ask). */
export function flexPreviewValues(params: readonly ParamDto[], s: FlexState): Record<string, number | string> | null {
  const keys = flexChanged(params, s);
  return keys.length ? Object.fromEntries(keys.map((k) => [k, s.values[k]!])) : null;
}
/** Flexing a value back to what it was drops it from the changes. */
export function flexSet(params: readonly ParamDto[], s: FlexState, key: string, value: number | string): FlexState {
  const values = { ...s.values, [key]: value };
  const p = params.find((x) => x.key === key);
  if (p && String(flexBase(p)) === String(value)) delete values[key];
  return { ...s, values };
}
/** Keep: the instance values (one `setPartValues`) and the defaults of the type parameters (one `updatePartParam` each). */
export function flexKeepPlan(params: readonly ParamDto[], s: FlexState): { instance: Record<string, number | string>; defaults: { key: string; value: number | string }[] } {
  const instance: Record<string, number | string> = {}, defaults: { key: string; value: number | string }[] = [];
  for (const k of flexChanged(params, s)) {
    const p = params.find((x) => x.key === k)!;
    if (p.instance) instance[k] = s.values[k]!; else defaults.push({ key: k, value: s.values[k]! });
  }
  return { instance, defaults };
}

/** The preview as `studio.partPreview` answers it. */
export interface PreviewDto { ok: boolean; reason?: string; bodies?: { name: string; edges: number[][]; volume: number }[] }
export const previewVolume = (p: PreviewDto | null): number | null => (p?.ok && p.bodies ? p.bodies.reduce((a, b) => a + (b.volume || 0), 0) : null);

// ---------------------------------------------------------------- Properties in model mode

export interface PublishedRow {
  key: string; label: string; kind: ParamKind; value: number | string; unit: string; description: string;
  /** `own`: editable for this object; `type`: read-only, the part's; `derived`: read-only, a formula. */
  mode: 'own' | 'type' | 'derived'; formula?: string; problem?: string; min?: number; max?: number;
}
/** The published parameters only, in the part's order, each with how it may be edited. */
export function publishedRows(params: readonly ParamDto[], values: readonly ValueDto[]): PublishedRow[] {
  return params.filter((p) => p.published).map((p) => {
    const v = values.find((x) => x.key === p.key);
    const derived = isDerived(p) || v?.source === 'formula';
    return {
      key: p.key, label: p.label, kind: p.kind, value: v?.value ?? p.value, unit: unitOf(p), description: p.description ?? '',
      mode: derived ? 'derived' : p.instance ? 'own' : 'type', formula: p.formula, problem: v?.problem, min: p.min, max: p.max,
    };
  });
}
