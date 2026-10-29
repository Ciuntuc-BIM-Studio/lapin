/**
 * The assistant's proposals (docs/plan-assistant.md): property values it
 * suggests, checked here against the model and IFC's rules before the user
 * sees them, gathered into a changeset the user ticks through, and written,
 * once accepted, as one engine edit (`edit.batch` of `element.setParam`: one
 * undo step). The model never writes anything itself.
 *
 * The rule for sets (the engine's StandardPsets, which sends its table): a set
 * that starts with „Pset_” is IFC's own, and only one Lapin knows, on its
 * classes, with one of its property names; any other set is the user's own
 * and must not start with „Pset_” or „Qto_”, nor be one of Lapin's own.
 */

import type { NodeDto } from '../engine/protocol';
import type { ModelState } from '../engine/model';
import { importedSets } from '../shell/ifcProperties';

/** The engine's table of standard sets (query params.standardSets). */
export interface StandardTable {
  own: string;
  reserved: string[];
  sets: { name: string; classes: string[]; properties: Record<string, string> }[];
}

/** What the model proposes: one value. */
export interface ProposedValue { id: string; pset?: string | null; name: string; value: unknown }

export type ParamKind = 'text' | 'number' | 'length' | 'area' | 'angle' | 'bool' | 'choice';
export type Scalar = string | number | boolean;

/** One line of the changeset, as the user reviews it. */
export interface ChangeRow {
  /** element/set/name: one line per value. */
  key: string;
  id: string;
  element: string;
  ifcClass: string;
  /** null: the user's own Lapin_Parameters. */
  pset: string | null;
  name: string;
  kind: ParamKind;
  old: Scalar | null;
  value: Scalar;
  accepted: boolean;
}

interface StoredParam { name: string; kind: string; value: unknown; pset?: string; options?: string[] }

const isStandard = (set: string) => /^pset_/i.test(set);
/** A set as stored: null for the user's own. */
const setOf = (pset: string | null | undefined, own: string): string | null => {
  const s = (pset ?? '').trim();
  return !s || s === own ? null : s;
};
export const rowKey = (id: string, pset: string | null, name: string) => `${id}/${(pset ?? '').toLowerCase()}/${name.toLowerCase()}`;

/** Whether a standard set is for an entity of this class (or its type). */
export const appliesTo = (classes: string[], ifcClass: string) => classes.some((c) => c === ifcClass || `${c}Type` === ifcClass);

/** The value as the kind holds it, or null when it cannot be. */
export function coerce(kind: ParamKind, v: unknown): Scalar | null {
  if (v === null || v === undefined) return null;
  switch (kind) {
    case 'bool':
      if (typeof v === 'boolean') return v;
      if (typeof v === 'string' && /^(true|yes|da|sí|si|1)$/i.test(v.trim())) return true;
      if (typeof v === 'string' && /^(false|no|nu|0)$/i.test(v.trim())) return false;
      return null;
    case 'text': case 'choice':
      return typeof v === 'object' ? null : String(v);
    default: {
      const n = typeof v === 'number' ? v : typeof v === 'string' && v.trim() !== '' ? Number(v.trim().replace(',', '.')) : NaN;
      if (!Number.isFinite(n)) return null;
      if ((kind === 'length' || kind === 'area') && n < 0) return null;
      return n;
    }
  }
}

const kindOfValue = (v: unknown): ParamKind => (typeof v === 'boolean' ? 'bool' : typeof v === 'number' ? 'number' : 'text');

/** The element's current value of a property: its own parameter, else what its IFC file said. */
export function currentValue(n: NodeDto, pset: string | null, name: string): Scalar | null {
  const params = (Array.isArray(n.props.params) ? n.props.params : []) as unknown as StoredParam[];
  const own = params.find((p) => (p.pset ?? null) === pset && p.name.toLowerCase() === name.toLowerCase());
  if (own && own.value != null && typeof own.value !== 'object') return own.value as Scalar;
  if (pset) {
    const set = importedSets(n).find((s) => s.name === pset);
    const v = set?.props.find(([k]) => k.toLowerCase() === name.toLowerCase())?.[1];
    if (v != null) return v;
  }
  return null;
}

export type Checked = { ok: true; row: ChangeRow } | { ok: false; why: string };

/** One proposed value checked: the element exists, the set and name are allowed, the value fits the kind. */
export function checkProposal(p: ProposedValue, model: ModelState, table: StandardTable): Checked {
  const n = typeof p.id === 'string' ? model.nodes.get(p.id) : undefined;
  if (!n) return { ok: false, why: `no element with id ${String(p.id)}` };
  let name = typeof p.name === 'string' ? p.name.trim() : '';
  if (!name) return { ok: false, why: 'a property needs a name' };
  const pset = setOf(p.pset, table.own);
  let kind: ParamKind;
  if (pset && isStandard(pset)) {
    const set = table.sets.find((s) => s.name.toLowerCase() === pset.toLowerCase());
    if (!set) return { ok: false, why: `${pset} is not a standard property set Lapin knows; a set of your own must not start with Pset_` };
    if (!appliesTo(set.classes, n.type)) return { ok: false, why: `${set.name} is not for ${n.type}` };
    const prop = Object.keys(set.properties).find((k) => k.toLowerCase() === name.toLowerCase());
    if (!prop) return { ok: false, why: `${set.name} has no property ${name} (it has ${Object.keys(set.properties).join(', ')})` };
    name = prop;
    kind = set.properties[prop] as ParamKind;
    return finish(set.name);
  }
  if (pset && /^qto_/i.test(pset)) return { ok: false, why: 'Qto_ sets are IFC quantities, computed by Lapin' };
  if (pset && (table.reserved.some((r) => r.toLowerCase() === pset.toLowerCase()) || /^pset_lapin/i.test(pset))) return { ok: false, why: `${pset} is Lapin's own set` };
  // The user's own: the kind the parameter already has, else the value's.
  const params = (Array.isArray(n.props.params) ? n.props.params : []) as unknown as StoredParam[];
  const existing = params.find((x) => (x.pset ?? null) === pset && x.name.toLowerCase() === name.toLowerCase());
  kind = (existing?.kind as ParamKind | undefined) ?? kindOfValue(p.value);
  if (existing) name = existing.name;
  if (kind === 'choice' && existing?.options?.length && !existing.options.includes(String(p.value)))
    return { ok: false, why: `${String(p.value)} is not one of the choices of ${name} (${existing.options.join(', ')})` };
  return finish(pset);

  function finish(set: string | null): Checked {
    const value = coerce(kind, p.value);
    if (value === null) return { ok: false, why: `${String(p.value)} is not a ${kind} value` };
    return {
      ok: true,
      row: { key: rowKey(n!.id, set, name), id: n!.id, element: n!.name, ifcClass: n!.type, pset: set, name, kind, old: currentValue(n!, set, name), value, accepted: true },
    };
  }
}

/** New rows into the changeset: a later value for the same element, set and name replaces the earlier one; a value already there is left out. */
export function mergeRows(existing: ChangeRow[], rows: ChangeRow[]): ChangeRow[] {
  const out = [...existing];
  for (const r of rows) {
    if (r.old !== null && String(r.old) === String(r.value)) continue;
    const i = out.findIndex((x) => x.key === r.key);
    if (i >= 0) out[i] = r; else out.push(r);
  }
  return out;
}

/** The accepted rows as the steps of one `edit.batch` (one undo step). */
export function toSteps(rows: ChangeRow[]): { command: string; args: Record<string, unknown> }[] {
  return rows.filter((r) => r.accepted).map((r) => ({
    command: 'element.setParam',
    args: { ids: [r.id], name: r.name, value: r.value, ...(r.pset ? { pset: r.pset } : { kind: r.kind }) },
  }));
}
