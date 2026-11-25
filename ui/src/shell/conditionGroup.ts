/**
 * One condition model for the three places that choose elements by rules (a view filter, Quick select, a clash search
 * set), as the engine's one filter (`ElementFilter`) reads it: a condition is a field (a field token of `fields.model`, or
 * a facet: "@class", "@type", "@storey"… or "@text:<name>", a field read as a tag writes it), an operator, a value or a
 * list of values. Each place keeps its own stored shape; the pure functions here convert both ways. A condition keeps
 * the stored rule it came from (`raw`) until it is edited, so a rule that is opened and not changed goes back as it was.
 */

export type UOp = '=' | '≠' | 'contains' | 'not contains' | 'starts with' | '<' | '≤' | '>' | '≥' | 'between' | 'one of' | 'none of' | 'exists' | 'missing';
export interface UCondition { field: string; op: UOp; value?: string; values?: string[]; raw?: unknown }
export interface UGroup { matchAll: boolean; conditions: UCondition[]; groups: UGroup[] }

export const UNIFIED_OPS: UOp[] = ['=', '≠', 'contains', 'not contains', 'starts with', '<', '≤', '>', '≥', 'between', 'one of', 'none of', 'exists', 'missing'];
export const OP_LABELS: Record<UOp, string> = {
  '=': 'is', '≠': 'is not', 'contains': 'contains', 'not contains': 'does not contain', 'starts with': 'starts with',
  '<': 'is less than', '≤': 'is at most', '>': 'is greater than', '≥': 'is at least', 'between': 'is between',
  'one of': 'is one of', 'none of': 'is none of', 'exists': 'has a value', 'missing': 'has no value',
};

/** The facets of an element, before the project's fields. */
export const FACETS: [string, string][] = [
  ['@class', 'IFC class'], ['@category', 'Category'], ['@type', 'Type'], ['@name', 'Name'], ['@material', 'Material'], ['@storey', 'Storey'],
  ['@group', 'Group'], ['@link', 'Linked file'], ['@phase', 'Phase created'], ['@demolished', 'Phase demolished'], ['@predefinedType', 'Predefined type'],
  // Whether it bears load, yes or no (the Structural tick, else the rule; docs/plan-structural-usage.md).
  ['@structural', 'Structural'],
];
export const TEXT_FACET = '@text:';
export const isTextFacet = (field: string) => field.startsWith(TEXT_FACET);
/** A field's name for the picker's button: a facet by its label, a parameter by its kind; a field token as it is. */
export function fieldLabel(field: string): string {
  if (isTextFacet(field)) return 'Parameter (as text)';
  return FACETS.find(([k]) => k === field)?.[1] ?? field;
}

type Shape = 'none' | 'one' | 'two' | 'list';
export const shapeOf = (op: UOp): Shape => (op === 'exists' || op === 'missing' ? 'none' : op === 'between' ? 'two' : op === 'one of' || op === 'none of' ? 'list' : 'one');

/** Values typed in one box: one per line or ";" or comma, but a "1,200" keeps its thousands. */
export function splitList(text: string): string[] {
  return text.split(/[\n;]|,(?!\d{3}\b)/).map((s) => s.trim()).filter((s) => s.length > 0);
}

/** The condition with another operator, its value carried over to the shape the operator takes. */
export function withOp(c: UCondition, op: UOp): UCondition {
  const from = shapeOf(c.op), to = shapeOf(op);
  const list = from === 'one' ? (c.value ? [c.value] : []) : from === 'none' ? [] : c.values ?? [];
  const out: UCondition = { field: c.field, op };
  if (to === 'one') out.value = from === 'one' ? c.value ?? '' : list[0] ?? '';
  else if (to === 'two') out.values = [list[0] ?? '', list[1] ?? ''];
  else if (to === 'list') out.values = from === 'two' ? list.filter((x) => x !== '') : list;
  return out;
}

/** Whether a condition can be asked: a field, a parameter's name, and a value when the operator takes one. */
export function isComplete(c: UCondition): boolean {
  if (!c.field.trim() || (isTextFacet(c.field) && !c.field.slice(TEXT_FACET.length).trim())) return false;
  switch (shapeOf(c.op)) {
    case 'none': return true;
    case 'one': return (c.value ?? '').trim() !== '';
    case 'two': return (c.values ?? []).some((v) => v.trim() !== '');
    case 'list': return (c.values ?? []).some((v) => v.trim() !== '');
  }
}

/** The condition without the stored rule it came from (after an edit). */
export const edited = (c: UCondition, patch: Partial<UCondition>): UCondition => ({ ...c, ...patch, raw: undefined });

// ── The field token ────────────────────────────────────────────────────────────────────────────

const FACET_NAMES = new Set(FACETS.map(([k]) => k.slice(1)));

/** The stored (field, name) pair of a view filter's rule or a clash rule's (field, parameter) for a unified field. */
function splitField(field: string, textKey: string, tokenKey: string): { field: string; name?: string } {
  if (isTextFacet(field)) return { field: textKey, name: field.slice(TEXT_FACET.length) };
  if (field.startsWith('@') && FACET_NAMES.has(field.slice(1))) return { field: field.slice(1) };
  return { field: tokenKey, name: field };
}
function joinField(field: string, name: string | undefined, textKey: string, tokenKey: string): string {
  if (field === textKey) return TEXT_FACET + (name ?? '');
  if (field === tokenKey) return name ?? '';
  return '@' + field;
}

// ── View filters and Quick select ──────────────────────────────────────────────────────────────

export interface ViewRule { field: string; op: string; value?: string; values?: string[]; name?: string }

const VIEW_OPS: [string, UOp][] = [
  ['equals', '='], ['notEquals', '≠'], ['contains', 'contains'], ['notContains', 'not contains'], ['beginsWith', 'starts with'], ['greater', '>'], ['less', '<'],
  ['atLeast', '≥'], ['atMost', '≤'], ['between', 'between'], ['oneOf', 'one of'], ['noneOf', 'none of'], ['hasValue', 'exists'], ['hasNoValue', 'missing'],
];

export function fromViewRule(r: ViewRule): UCondition {
  const op = VIEW_OPS.find(([k]) => k === r.op)?.[1] ?? (r.op as UOp);
  const c: UCondition = { field: joinField(r.field, r.name, 'param', 'field'), op, raw: r };
  switch (shapeOf(op)) {
    case 'one': c.value = r.value ?? ''; break;
    case 'two': { const v = r.value ?? ''; const k = v.indexOf('..'); c.values = k < 0 ? [v, ''] : [v.slice(0, k), v.slice(k + 2)]; break; }
    case 'list': c.values = r.values ?? (r.value ? [r.value] : []); break;
  }
  return c;
}

export function toViewRule(c: UCondition): ViewRule {
  if (c.raw) return c.raw as ViewRule;
  const op = VIEW_OPS.find(([, u]) => u === c.op)?.[0] ?? c.op;
  const { field, name } = splitField(c.field, 'param', 'field');
  const r: ViewRule = { field, op };
  switch (shapeOf(c.op)) {
    case 'one': r.value = c.value ?? ''; break;
    case 'two': r.value = `${(c.values?.[0] ?? '').trim()}..${(c.values?.[1] ?? '').trim()}`; break;
    case 'list': r.values = c.values ?? []; break;
  }
  if (name !== undefined) r.name = name;
  return r;
}

export function fromViewFilter(match: 'all' | 'any', rules: ViewRule[]): UGroup {
  return { matchAll: match === 'all', conditions: rules.map(fromViewRule), groups: [] };
}
export const toViewFilter = (g: UGroup): { rules: ViewRule[]; match: 'all' | 'any' } => ({ rules: g.conditions.map(toViewRule), match: g.matchAll ? 'all' : 'any' });

/** Quick select's `select.query` arguments: the finished conditions, values trimmed, a parameter by its name. */
export function queryArgs(g: UGroup): { rules: ViewRule[]; match: 'all' | 'any' } {
  const rules = g.conditions.filter(isComplete).map((c) => {
    const clean: UCondition = { field: c.field.trim(), op: c.op };
    if (c.value !== undefined) clean.value = c.value.trim();
    if (c.values) clean.values = c.values.map((v) => v.trim());
    if (isTextFacet(clean.field)) clean.field = TEXT_FACET + clean.field.slice(TEXT_FACET.length).trim();
    return toViewRule(clean);
  });
  return { rules, match: g.matchAll ? 'all' : 'any' };
}

// ── Clash search sets ──────────────────────────────────────────────────────────────────────────

export interface ClashRule { field: string; op: string; values: string[]; parameter?: string }
export interface ClashGroup { matchAll: boolean; rules: ClashRule[]; groups: ClashGroup[] }

const CLASH_OPS: [string, UOp][] = [
  ['is', '='], ['isNot', '≠'], ['contains', 'contains'], ['notContains', 'not contains'], ['startsWith', 'starts with'], ['greater', '>'], ['less', '<'],
  ['atLeast', '≥'], ['atMost', '≤'], ['between', 'between'], ['isOneOf', 'one of'], ['isNotAnyOf', 'none of'], ['exists', 'exists'], ['missing', 'missing'],
];

export function fromClashRule(r: ClashRule): UCondition {
  const op = CLASH_OPS.find(([k]) => k === r.op)?.[1] ?? (r.op as UOp);
  const c: UCondition = { field: joinField(r.field, r.parameter, 'parameter', 'field'), op, raw: r };
  switch (shapeOf(op)) {
    case 'one': c.value = r.values?.[0] ?? ''; break;
    case 'two': c.values = [r.values?.[0] ?? '', r.values?.[1] ?? '']; break;
    case 'list': c.values = r.values ?? []; break;
  }
  return c;
}

export function toClashRule(c: UCondition): ClashRule {
  if (c.raw) return c.raw as ClashRule;
  const op = CLASH_OPS.find(([, u]) => u === c.op)?.[0] ?? c.op;
  const { field, name } = splitField(c.field, 'parameter', 'field');
  const shape = shapeOf(c.op);
  const r: ClashRule = { field, op, values: shape === 'none' ? [] : shape === 'one' ? [c.value ?? ''] : c.values ?? [] };
  if (name !== undefined) r.parameter = name;
  return r;
}

export const fromClashGroup = (g: ClashGroup): UGroup => ({ matchAll: g.matchAll, conditions: g.rules.map(fromClashRule), groups: (g.groups ?? []).map(fromClashGroup) });
export const toClashGroup = (g: UGroup): ClashGroup => ({ matchAll: g.matchAll, rules: g.conditions.map(toClashRule), groups: g.groups.map(toClashGroup) });

// ── Conditions as the engine keeps them (a field schedule's filter, a task's rule, a generator's step) ─────────────────

/** A condition in the engine's own words (`FieldCondition`): the field (a field token or a facet), the operator, a value or a list. */
export interface NativeCondition { field: string; op: string; value?: string | number; values?: string[] }
/** A group of them (a field schedule's filter): all or any, and groups inside. */
export interface NativeGroup { match?: 'all' | 'any'; conditions?: NativeCondition[]; nested?: NativeGroup[] }

/** The operators as the engine also reads them (a script's `!=`, `<=`…), by the editor's names. */
const NATIVE_OPS: Record<string, UOp> = {
  '==': '=', 'is': '=', 'equals': '=', '!=': '≠', '<>': '≠', 'is not': '≠', 'not': '≠', '<=': '≤', '>=': '≥',
  'startswith': 'starts with', 'notcontains': 'not contains', 'between': 'between', 'in': 'one of', 'not in': 'none of', 'has': 'exists', 'empty': 'missing',
};
const normalOp = (op: string): UOp => (UNIFIED_OPS.includes(op as UOp) ? (op as UOp) : NATIVE_OPS[op.trim().toLowerCase()] ?? (op as UOp));

/** A condition's value as the engine compares it: a number when it reads as one (as the field schedules have always sent it). */
function nativeValue(text: string): string | number {
  const t = text.trim().replace(',', '.');
  return t !== '' && Number.isFinite(Number(t)) ? Number(t) : text.trim();
}

export function fromCondition(r: NativeCondition): UCondition {
  const op = normalOp(r.op);
  const c: UCondition = { field: r.field, op, raw: r };
  switch (shapeOf(op)) {
    case 'one': c.value = r.value == null ? '' : String(r.value); break;
    case 'two': c.values = [r.values?.[0] ?? (r.value == null ? '' : String(r.value).split('..')[0] ?? ''), r.values?.[1] ?? (r.value == null ? '' : String(r.value).split('..')[1] ?? '')]; break;
    case 'list': c.values = r.values ?? (r.value == null || r.value === '' ? [] : [String(r.value)]); break;
  }
  return c;
}

export function toCondition(c: UCondition): NativeCondition {
  if (c.raw) return c.raw as NativeCondition;
  const field = isTextFacet(c.field) ? TEXT_FACET + c.field.slice(TEXT_FACET.length).trim() : c.field.trim();
  switch (shapeOf(c.op)) {
    case 'none': return { field, op: c.op };
    case 'one': return { field, op: c.op, value: nativeValue(c.value ?? '') };
    case 'two': return { field, op: c.op, values: [(c.values?.[0] ?? '').trim(), (c.values?.[1] ?? '').trim()] };
    case 'list': return { field, op: c.op, values: (c.values ?? []).map((v) => v.trim()).filter((v) => v !== '') };
  }
}

export const fromNativeGroup = (g: NativeGroup): UGroup => ({ matchAll: g.match !== 'any', conditions: (g.conditions ?? []).map(fromCondition), groups: (g.nested ?? []).map(fromNativeGroup) });
export function toNativeGroup(g: UGroup): NativeGroup {
  const o: NativeGroup = { match: g.matchAll ? 'all' : 'any', conditions: g.conditions.map(toCondition) };
  if (g.groups.length) o.nested = g.groups.map(toNativeGroup);
  return o;
}

/** A flat list of conditions, all of which must hold (a task's rule, a generator's step). */
export const fromConditions = (list: NativeCondition[] | undefined): UGroup => ({ matchAll: true, conditions: (list ?? []).map(fromCondition), groups: [] });
export const toConditions = (g: UGroup): NativeCondition[] => g.conditions.map(toCondition);

/**
 * The group as it can be sent while the rest is being typed: its finished conditions, and those read from storage and not
 * changed (a rule saved without a value keeps its place); a group left with nothing goes.
 */
export function finishedGroup(g: UGroup): UGroup {
  const groups = g.groups.map(finishedGroup).filter((x) => x.conditions.length || x.groups.length);
  return { matchAll: g.matchAll, conditions: g.conditions.filter((c) => c.raw !== undefined || isComplete(c)), groups };
}
