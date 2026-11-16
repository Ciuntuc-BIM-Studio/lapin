/**
 * Schedules of any field (docs/plan-user-extensibility.md §11): the options a field schedule keeps (`fields`), what the
 * engine says about the project's fields (`fields.model`, `fields.values`), the office's field names (aliases) and the
 * libraries of both (this computer's files; the firm folders are read by FirmFolderFiles).
 */

import { query } from '../engine/session';
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

export const AGGREGATES = ['sum', 'avg', 'min', 'max', 'count', 'countDistinct', 'first', 'list'] as const;
export type Aggregate = typeof AGGREGATES[number];
export const AGGREGATE_LABELS: Record<Aggregate, string> = { sum: 'Sum', avg: 'Average', min: 'Smallest', max: 'Largest', count: 'Count', countDistinct: 'Count of different', first: 'First', list: 'List of values' };

export const UNITS = ['m', 'cm', 'mm', 'km', 'm²', 'cm²', 'mm²', 'ha', 'm³', 'dm³', 'l', 'cm³', 'kg', 't'];

/** A condition: a field (a field token, or a facet such as `@type`), an operator, a value, or a list (`one of`, `none of`, the two ends of `between`). */
export interface Condition { field: string; op: Operator | (string & {}); value?: string | number; values?: string[] }
/** A group of conditions inside a filter: all or any of them, and groups inside. */
export interface ConditionGroupDef { match?: 'all' | 'any'; conditions?: Condition[]; nested?: ConditionGroupDef[] }
export interface FieldColumn { field?: string; formula?: string; heading?: string; aggregate?: Aggregate; unit?: string; decimals?: number }
export interface FieldFilter { classes?: string[]; types?: string[]; storeys?: string[]; materials?: string[]; groups?: string[]; phases?: string[]; conditions?: Condition[]; match?: 'all' | 'any'; nested?: ConditionGroupDef[] }
export interface FieldOptions { filter?: FieldFilter; groupBy?: string[]; columns?: FieldColumn[]; totals?: boolean; count?: boolean }

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
    return k;
  });
  return { filter, groupBy: (o.groupBy ?? []).filter((g) => g.trim()), columns, totals: o.totals !== false, count: o.count !== false };
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
