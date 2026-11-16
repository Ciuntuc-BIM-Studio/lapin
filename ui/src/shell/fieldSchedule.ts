/**
 * Schedules of any field (docs/plan-user-extensibility.md §11): the options a field schedule keeps (`fields`), what the
 * engine says about the project's fields (`fields.model`, `fields.values`), the office's field names (aliases) and the
 * libraries of both (this computer's files; the firm folders are read by FirmFolderFiles).
 */

import { execute, query } from '../engine/session';
import { computerFiles, forgetComputerFile, computerFileId, keepComputerFile } from '../platform/libraries';

export type FieldKind = 'number' | 'text' | 'bool';
export interface FieldDef { token: string; label: string; kind: FieldKind; count: number; examples: string[]; set?: string | null; short?: string | null; description?: string | null }
export interface FieldGroupDef { id: string; label: string; set?: string | null; sampled?: boolean; fields: FieldDef[] }
export interface FieldModel { elements: number; sample: number; groups: FieldGroupDef[] }

/** The operators a condition takes (the engine's `FieldCondition.Ops`); the one editor offers them all (shell/conditionGroup). */
export const OPERATORS = ['=', '≠', 'contains', 'not contains', 'starts with', '<', '≤', '>', '≥', 'between', 'one of', 'none of', 'exists', 'missing'] as const;
export type Operator = typeof OPERATORS[number];
/** Operators that take no value. */
export const NO_VALUE: readonly string[] = ['exists', 'missing'];

export const AGGREGATES = ['sum', 'avg', 'min', 'max', 'count', 'countDistinct', 'first', 'list', 'rows'] as const;
export type Aggregate = typeof AGGREGATES[number];
export const AGGREGATE_LABELS: Record<Aggregate, string> = { sum: 'Sum', avg: 'Average', min: 'Smallest', max: 'Largest', count: 'Count', countDistinct: 'Count of different', first: 'First', list: 'List of values', rows: 'Number of items' };

export const UNITS = ['m', 'cm', 'mm', 'km', 'm²', 'cm²', 'mm²', 'ha', 'm³', 'dm³', 'l', 'cm³', 'kg', 't'];

/** A condition: a field (a field token, or a facet such as `@type`), an operator, a value, or a list (`one of`, `none of`, the two ends of `between`). */
export interface Condition { field: string; op: Operator | (string & {}); value?: string | number; values?: string[] }
/** A group of conditions inside a filter: all or any of them, and groups inside. */
export interface ConditionGroupDef { match?: 'all' | 'any'; conditions?: Condition[]; nested?: ConditionGroupDef[] }
export interface FieldColumn { field?: string; formula?: string; heading?: string; aggregate?: Aggregate; unit?: string; decimals?: number; format?: string }
export interface FieldFilter { classes?: string[]; types?: string[]; storeys?: string[]; materials?: string[]; groups?: string[]; phases?: string[]; conditions?: Condition[]; match?: 'all' | 'any'; nested?: ConditionGroupDef[] }
/** A field the rows are put in order by. */
export type FieldSort = string | { field: string; desc?: boolean };
/**
 * A field schedule's options. Besides the filter, grouping, columns and totals (audit S1): `source`, the parts it lists instead
 * of elements (`schedule.partSources`); `groupColumns: false`, the grouping fields not shown first; `sort`, the fields the rows
 * are put in order by; `subtotals`, a totals line per value of these fields; `totals: 'always'`, the totals under one row too.
 */
export interface FieldOptions {
  filter?: FieldFilter; groupBy?: string[]; columns?: FieldColumn[]; totals?: boolean | 'always'; count?: boolean;
  source?: string; groupColumns?: boolean; sort?: FieldSort[]; subtotals?: string[];
}

/** A sort's field and direction. */
export const sortOf = (s: FieldSort): { field: string; desc: boolean } => (typeof s === 'string' ? { field: s, desc: false } : { field: s.field, desc: !!s.desc });

/** A number format the engine takes (0, #, one point): `0.##`, `0.000`. */
export const isFormat = (f: string): boolean => /^[0#.,]{1,24}$/.test(f) && f.includes('0') && (f.match(/\./g) ?? []).length <= 1;

export const DIMENSIONS: { key: Exclude<keyof FieldFilter, 'conditions' | 'match' | 'nested'>; label: string; field: string }[] = [
  { key: 'classes', label: 'Kinds', field: 'class' },
  { key: 'types', label: 'Types', field: 'type' },
  { key: 'storeys', label: 'Storeys', field: 'storey.name' },
  { key: 'materials', label: 'Materials', field: 'material' },
  { key: 'groups', label: 'Groups', field: 'group.name' },
  { key: 'phases', label: 'Phases', field: 'phase' },
];

/** A condition's value as the engine compares it: a number when it reads as one. */
export function conditionValue(text: string): string | number {
  const t = text.trim().replace(',', '.');
  return t !== '' && Number.isFinite(Number(t)) ? Number(t) : text.trim();
}

/** Conditions as the engine takes them: a field named, a list or a value (none when the operator takes none). */
function cleanConditions(list: Condition[] | undefined): Condition[] {
  return (list ?? []).filter((c) => c.field.trim() && c.field.trim() !== '@text:').map((c): Condition => (NO_VALUE.includes(c.op) ? { field: c.field, op: c.op }
    : c.values ? { field: c.field, op: c.op, values: c.values } : { field: c.field, op: c.op, value: c.value ?? '' }));
}
function cleanGroups(list: ConditionGroupDef[] | undefined): ConditionGroupDef[] {
  return (list ?? []).map((g): ConditionGroupDef => {
    const o: ConditionGroupDef = { match: g.match === 'any' ? 'any' : 'all', conditions: cleanConditions(g.conditions) };
    const nested = cleanGroups(g.nested);
    if (nested.length) o.nested = nested;
    return o;
  }).filter((g) => g.conditions!.length || g.nested?.length);
}

/** The options as the engine takes them: nothing empty, a condition without its value when the operator takes none. */
export function cleanOptions(o: FieldOptions): FieldOptions {
  const filter: FieldFilter = {};
  for (const d of DIMENSIONS) { const v = o.filter?.[d.key]; if (v?.length) filter[d.key] = v; }
  const conditions = cleanConditions(o.filter?.conditions);
  if (conditions.length) filter.conditions = conditions;
  if (o.filter?.match === 'any') filter.match = 'any';
  const nested = cleanGroups(o.filter?.nested);
  if (nested.length) filter.nested = nested;
  const columns = (o.columns ?? []).filter((c) => (c.field ?? '').trim() || (c.formula ?? '').trim()).map((c) => {
    const k: FieldColumn = c.formula?.trim() ? { formula: c.formula.trim() } : { field: c.field };
    if (c.heading?.trim()) k.heading = c.heading.trim();
    if (c.aggregate) k.aggregate = c.aggregate;
    if (c.unit?.trim()) k.unit = c.unit.trim();
    if (typeof c.decimals === 'number' && Number.isFinite(c.decimals)) k.decimals = Math.max(0, Math.min(9, Math.round(c.decimals)));
    if (c.format?.trim() && isFormat(c.format.trim())) k.format = c.format.trim();
    return k;
  });
  const out: FieldOptions = { filter, groupBy: (o.groupBy ?? []).filter((g) => g.trim()), columns, totals: o.totals === 'always' ? 'always' : o.totals !== false, count: o.count !== false };
  if (o.source) out.source = o.source;
  if (o.groupColumns === false) out.groupColumns = false;
  const sort = (o.sort ?? []).map(sortOf).filter((x) => x.field.trim()).map((x) => (x.desc ? { field: x.field, desc: true } : x.field));
  if (sort.length) out.sort = sort;
  const subtotals = (o.subtotals ?? []).filter((g) => g.trim());
  if (subtotals.length) out.subtotals = subtotals;
  return out;
}

/** An item moved one place up (-1) or down (1); the list itself when it cannot. */
export function moved<T>(list: T[], i: number, by: -1 | 1): T[] {
  const j = i + by;
  if (j < 0 || j >= list.length) return list;
  const next = [...list];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}

/** The groups with the fields matching every word typed (token, label, set, description, examples); empty groups left out. */
export function searchGroups(groups: FieldGroupDef[], typed: string): FieldGroupDef[] {
  const words = typed.toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return groups;
  return groups.map((g) => ({ ...g, fields: g.fields.filter((f) => {
    const hay = `${f.token} ${f.label} ${f.short ?? ''} ${f.description ?? ''} ${(f.examples ?? []).join(' ')} ${g.label}`.toLowerCase();
    return words.every((w) => hay.includes(w));
  }) })).filter((g) => g.fields.length > 0);
}

// ── Shipped templates and parts (audit S1) ───────────────────────────────────────────────────────────

/** A field schedule a module ships: its id, name, the fixed schedule it stands for, the parts it lists. */
export interface ShippedTemplate { id: string; label: string; kind?: string | null; source?: string | null }
/** A kind of parts a field schedule can list, with its parts' fields. */
export interface PartSource { source: string; label: string; fields: { key: string; label: string }[] }

let shipped: Promise<ShippedTemplate[]> | null = null;
/** The templates this edition ships (the edition does not change while it runs: asked once). */
export function shippedTemplates(): Promise<ShippedTemplate[]> {
  shipped ??= query<ShippedTemplate[]>('schedule.shippedTemplates').then((r) => { if (!r) shipped = null; return r ?? []; });
  return shipped;
}

export async function partSources(): Promise<PartSource[]> {
  return (await query<PartSource[]>('schedule.partSources')) ?? [];
}

/**
 * A new schedule of a kind the ribbon or the browser offers: a field schedule from the template a module ships for it when
 * there is one (audit S1: the fixed schedule's kind is the template's alias), else the module's own table. One undo step.
 */
export async function createScheduleOf(kind: string, name?: string): Promise<{ id: string } | null> {
  const templates = await shippedTemplates();
  const t = templates.find((x) => x.kind === kind);
  const args: Record<string, unknown> = t ? { builtin: t.id } : { kind };
  if (name) args.name = name;
  const r = await execute(t ? 'architecture.importSchedule' : 'architecture.createSchedule', args) as { id?: string } | null;
  return r?.id ? { id: r.id } : null;
}

export async function fieldModel(): Promise<FieldModel> {
  return (await query<FieldModel>('fields.model', { examples: 3 })) ?? { elements: 0, sample: 0, groups: [] };
}

export async function fieldValues(field: string): Promise<string[]> {
  const r = await query<{ values: { value: unknown; count: number }[] }>('fields.values', { field, limit: 60 });
  return (r?.values ?? []).map((v) => String(v.value)).filter(Boolean);
}

// ── Field names (aliases) ──────────────────────────────────────────────────────────────────────

export interface FieldAlias { name: string; sources: string[]; description?: string }

export async function aliases(): Promise<FieldAlias[]> {
  return (await query<{ aliases: FieldAlias[] }>('fields.aliases'))?.aliases ?? [];
}

// ── Libraries on this computer: the files of the kinds, as the price lists' library keeps its own ──

export const TEMPLATE_EXT = 'lapinschedule';
export const ALIASES_EXT = 'lapinaliases';

export const keepTemplate = (name: string, text: string) => keepComputerFile(TEMPLATE_EXT, name, text);
export const forgetTemplate = (name: string) => forgetComputerFile(computerFileId(TEMPLATE_EXT, name));
export const keptTemplates = () => computerFiles(TEMPLATE_EXT);
export const keepAliasesFile = (name: string, text: string) => keepComputerFile(ALIASES_EXT, name, text);
export const forgetAliasesFile = (name: string) => forgetComputerFile(computerFileId(ALIASES_EXT, name));
export const keptAliasesFiles = () => computerFiles(ALIASES_EXT);

/** The first plausible name for a field's column: its token without the set. */
export const shortToken = (token: string): string => token.includes('.') ? token.slice(token.lastIndexOf('.') + 1) : token;
