/**
 * The assistant's tools (docs/plan-assistant.md): the only way the local model
 * learns about the project or acts on it. They read the model as the session
 * holds it and the engine's quantities and schedules; every count, sum and
 * average is computed here, so the model quotes numbers and never works them
 * out. The one tool that changes anything, `propose_properties`, only puts
 * values in front of the user (changeset.ts). No geometry, no deleting, no
 * other command.
 *
 * Results are kept small for the model's context: a page of rows (offset,
 * limit) and at most MAX_CHARS of JSON; the whole list goes to the chat's
 * table, for the user to read and export.
 */

import type { NodeDto } from '../engine/protocol';
import { storeys, typeOf, type ModelState } from '../engine/model';
import { importedSets } from '../shell/ifcProperties';
import { appliesTo, checkProposal, type ChangeRow, type ProposedValue, type StandardTable } from './changeset';
import { parseCsv } from './csv';

export interface QuantitySet { name: string; quantities: { name: string; value: number; kind?: string; unit?: string }[] }

/** What the tools read through. */
export interface ToolContext {
  model: ModelState;
  /** The elements selected in the views. */
  selection: string[];
  /** The engine's table of standard property sets. */
  standard: () => Promise<StandardTable>;
  /** Several elements' quantity sets (the engine's quantities.many). */
  quantities: (ids: string[]) => Promise<Record<string, QuantitySet[]>>;
  /** A schedule as CSV (the engine's schedule.csv). */
  scheduleCsv: (id: string) => Promise<string | null>;
}

export type Cell = string | number | boolean | null;
export interface Table { title: string; columns: string[]; rows: Cell[][] }

/** What a tool gives back: the JSON for the model, a short line for the chat, and a table or proposals for the user. */
export interface ToolOutcome { content: string; summary: string; table?: Table; proposals?: ChangeRow[]; error?: boolean }

/** The most JSON a tool result may hand the model. */
export const MAX_CHARS = 6000;
export const DEFAULT_LIMIT = 25;
export const MAX_LIMIT = 50;
/** The most rows a table in the chat keeps. */
export const MAX_TABLE_ROWS = 5000;

class ToolError extends Error {}

// ─── The tools as the model sees them (Ollama's function schema) ────────────

const filterProps = {
  class: { type: 'string', description: 'IFC class, e.g. IfcWall, IfcDoor, IfcWindow, IfcSlab, IfcSpace (a plural like "doors" works too). Several: comma-separated.' },
  storey: { type: 'string', description: 'Storey name or id.' },
  type: { type: 'string', description: 'Part of the element type name, or the type id.' },
  name_contains: { type: 'string', description: 'Part of the element name.' },
  selected_only: { type: 'boolean', description: 'Only the elements the user has selected.' },
  where: {
    type: 'array',
    description: 'Conditions, all must hold. Field: a quantity (Width, Height, Length, Area, NetSideArea, GrossVolume…), an attribute, a property as Set.Name (Pset_DoorCommon.FireRating), or class/name/storey/type. Lengths in metres, areas m², volumes m³.',
    items: {
      type: 'object',
      properties: {
        field: { type: 'string' },
        op: { type: 'string', enum: ['=', '!=', '>', '>=', '<', '<=', 'contains', 'exists', 'missing'] },
        value: { type: ['string', 'number', 'boolean'] },
      },
      required: ['field', 'op'],
    },
  },
} as const;

export const TOOL_DEFS = [
  {
    type: 'function',
    function: {
      name: 'model_overview',
      description: 'The project at a glance: storeys with their element counts, element counts by class, types with how many elements use them, materials, schedules. Call it first.',
      parameters: { type: 'object', properties: {} },
    },
  },
  {
    type: 'function',
    function: {
      name: 'list_elements',
      description: 'List elements (id, class, name, storey, type and key quantities), filtered. Paged: use offset for more.',
      parameters: {
        type: 'object',
        properties: {
          ...filterProps,
          fields: { type: 'array', items: { type: 'string' }, description: 'Extra columns (quantities, attributes or Set.Name properties).' },
          sort_by: { type: 'string', description: 'A field to sort by.' },
          descending: { type: 'boolean' },
          offset: { type: 'integer' },
          limit: { type: 'integer', description: `At most ${MAX_LIMIT}.` },
        },
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'get_element',
      description: 'One element in full: attributes, property sets (its own, its type\'s, those from an IFC file) and quantities.',
      parameters: { type: 'object', properties: { id: { type: 'string' } }, required: ['id'] },
    },
  },
  {
    type: 'function',
    function: {
      name: 'aggregate',
      description: 'Count, sum, average, minimum or maximum over filtered elements, optionally grouped (by storey, type, class, name or any field). Use it for every number in a report; do not compute yourself.',
      parameters: {
        type: 'object',
        properties: {
          ...filterProps,
          group_by: { type: 'array', items: { type: 'string' }, description: 'Up to 3 fields, e.g. ["storey"] or ["storey", "type"].' },
          metrics: {
            type: 'array',
            description: 'Default: count. E.g. [{"op":"count"},{"op":"sum","field":"Length"}].',
            items: { type: 'object', properties: { op: { type: 'string', enum: ['count', 'sum', 'avg', 'min', 'max'] }, field: { type: 'string' } }, required: ['op'] },
          },
        },
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'property_names',
      description: 'The standard IFC property sets (Pset_…Common) an element class may take, with their property names and kinds, and the parameters already used on that class. Check it before proposing values.',
      parameters: { type: 'object', properties: { class: { type: 'string' } }, required: ['class'] },
    },
  },
  {
    type: 'function',
    function: { name: 'list_schedules', description: 'The project\'s schedules (doors, windows, lintels, rooms…).', parameters: { type: 'object', properties: {} } },
  },
  {
    type: 'function',
    function: {
      name: 'read_schedule',
      description: 'A schedule\'s table, paged.',
      parameters: { type: 'object', properties: { id: { type: 'string' }, offset: { type: 'integer' }, limit: { type: 'integer' } }, required: ['id'] },
    },
  },
  {
    type: 'function',
    function: {
      name: 'propose_properties',
      description: 'Propose property values for elements. They are NOT applied: the user reviews and applies them. Standard sets (Pset_…) only with their own property names (see property_names); a set of the user\'s own must not start with Pset_; no set: Lapin_Parameters.',
      parameters: {
        type: 'object',
        properties: {
          changes: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                id: { type: 'string', description: 'Element id.' },
                pset: { type: 'string', description: 'Property set, e.g. Pset_DoorCommon.' },
                name: { type: 'string', description: 'Property name, e.g. FireRating.' },
                value: { type: ['string', 'number', 'boolean'] },
              },
              required: ['id', 'name', 'value'],
            },
          },
        },
        required: ['changes'],
      },
    },
  },
] as const;

export type ToolName = (typeof TOOL_DEFS)[number]['function']['name'];

// ─── Reading the model ──────────────────────────────────────────────────────

const round = (v: number) => Math.round(v * 1000) / 1000;
const lc = (s: string) => s.toLowerCase();

/** An element: contained in a storey (walls, doors, slabs, rooms…), not a view, a type or a material. */
const isElement = (n: NodeDto) => n.storeyId != null && n.type !== 'IfcBuildingStorey';

/** The classes asked for (several comma-separated, a plural, without Ifc), as the model has them; null: all elements. */
export function resolveClasses(model: ModelState, q: unknown): string[] | null {
  if (q == null || q === '') return null;
  const asked = (Array.isArray(q) ? q.map(String) : String(q).split(',')).map((s) => s.trim()).filter(Boolean);
  if (asked.length === 0) return null;
  const present = [...new Set([...model.nodes.values()].map((n) => n.type))];
  const bare = (c: string) => lc(c).replace(/^(ifc|lapin)/, '');
  const out: string[] = [];
  for (const a of asked) {
    const want = [lc(a), bare(a), bare(a).replace(/ies$/, 'y'), bare(a).replace(/e?s$/, '')];
    const hit = present.filter((c) => want.includes(lc(c)) || want.includes(bare(c)));
    if (hit.length === 0) throw new ToolError(`No class „${a}” in the model. Classes present: ${classesPresent(model).join(', ')}.`);
    out.push(...hit);
  }
  return [...new Set(out)];
}

const classesPresent = (model: ModelState) => [...new Set([...model.nodes.values()].filter(isElement).map((n) => n.type))].sort();

function resolveStorey(model: ModelState, q: unknown): NodeDto | null {
  if (q == null || q === '') return null;
  const all = storeys(model);
  const s = lc(String(q).trim());
  const hit = all.find((n) => n.id === q) ?? all.find((n) => lc(n.name) === s) ?? all.find((n) => lc(n.name).includes(s));
  if (!hit) throw new ToolError(`No storey „${String(q)}”. Storeys: ${all.map((n) => n.name).join(', ')}.`);
  return hit;
}

const storeyName = (model: ModelState, n: NodeDto) => (n.storeyId ? model.nodes.get(n.storeyId)?.name ?? null : null);
const typeName = (model: ModelState, n: NodeDto) => typeOf(model, n.id)?.name ?? null;

interface StoredParam { name: string; value: unknown; pset?: string }
const paramsOf = (n: NodeDto | null | undefined): StoredParam[] => (n && Array.isArray(n.props.params) ? n.props.params : []) as unknown as StoredParam[];

const BUILTIN = ['id', 'class', 'name', 'storey', 'type'];
const isBuiltin = (f: string) => BUILTIN.includes(lc(f));

/** A field of an element: a built-in (class, storey…), a Set.Name property, a quantity, an attribute or a parameter of its own or its type's. */
export function fieldValue(model: ModelState, n: NodeDto, field: string, q?: QuantitySet[]): Cell | undefined {
  const f = field.trim();
  switch (lc(f)) {
    case 'id': return n.id;
    case 'class': return n.type;
    case 'name': return n.name;
    case 'storey': return storeyName(model, n);
    case 'type': return typeName(model, n);
  }
  const type = typeOf(model, n.id);
  const dot = f.indexOf('.');
  if (dot > 0) {
    const [set, name] = [f.slice(0, dot), f.slice(dot + 1)];
    const own = set === 'Lapin_Parameters' ? null : set;
    for (const holder of [n, type]) {
      const p = paramsOf(holder).find((x) => lc(x.pset ?? '') === lc(own ?? '') && lc(x.name) === lc(name));
      if (p && p.value != null && typeof p.value !== 'object') return p.value as Cell;
    }
    const imported = importedSets(n).find((s) => lc(s.name) === lc(set))?.props.find(([k]) => lc(k) === lc(name));
    if (imported) return imported[1];
    const qty = q?.find((s) => lc(s.name) === lc(set))?.quantities.find((x) => lc(x.name) === lc(name));
    if (qty) return round(qty.value);
    return undefined;
  }
  // A quantity: the base set's first (Qto_…BaseQuantities), then any.
  if (q) {
    const ordered = [...q].sort((a, b) => rank(a.name) - rank(b.name));
    for (const s of ordered) { const x = s.quantities.find((y) => lc(y.name) === lc(f)); if (x) return round(x.value); }
  }
  const key = Object.keys(n.props).find((k) => lc(k) === lc(f));
  if (key) { const v = n.props[key]; if (v === null || typeof v !== 'object') return typeof v === 'number' ? round(v) : v; }
  for (const holder of [n, type]) {
    const p = paramsOf(holder).find((x) => lc(x.name) === lc(f));
    if (p && p.value != null && typeof p.value !== 'object') return p.value as Cell;
  }
  return undefined;
}
const rank = (set: string) => (/^Qto_.*BaseQuantities$/.test(set) ? 0 : /^Qto_/.test(set) ? 1 : 2);

interface Where { field: string; op: string; value?: unknown }

function holds(v: Cell | undefined, w: Where): boolean {
  if (w.op === 'exists') return v !== undefined && v !== null && v !== '';
  if (w.op === 'missing') return v === undefined || v === null || v === '';
  if (v === undefined || v === null) return w.op === '!=';
  const [a, b] = [measure(v), measure(w.value)];
  if (a !== null && b !== null && w.op !== 'contains') {
    switch (w.op) {
      case '=': return Math.abs(a - b) < 1e-9;
      case '!=': return Math.abs(a - b) >= 1e-9;
      case '>': return a > b;
      case '>=': return a >= b - 1e-9;
      case '<': return a < b;
      case '<=': return a <= b + 1e-9;
    }
  }
  const [s, t] = [lc(String(v)), lc(String(w.value ?? ''))];
  switch (w.op) {
    case '=': return s === t;
    case '!=': return s !== t;
    case 'contains': return s.includes(t);
    case '>': return s > t;
    case '>=': return s >= t;
    case '<': return s < t;
    case '<=': return s <= t;
    default: throw new ToolError(`„${w.op}” is not a comparison (=, !=, >, >=, <, <=, contains, exists, missing).`);
  }
}

/** A number, or a measure as a model may write it ("1 m", "1,2", "900 mm", "120 cm"), in metres; null for anything else. */
export function measure(x: unknown): number | null {
  if (typeof x === 'number') return Number.isFinite(x) ? x : null;
  if (typeof x !== 'string') return null;
  const m = /^\s*(-?\d+(?:[.,]\d+)?)\s*(mm|cm|m|m2|m²|m3|m³)?\s*$/i.exec(x);
  if (!m) return null;
  const n = Number(m[1].replace(',', '.'));
  return m[2]?.toLowerCase() === 'mm' ? n / 1000 : m[2]?.toLowerCase() === 'cm' ? n / 100 : n;
}

/** A comparison as models write it (gt, >=, eq, ==, not_equals…), as the tools know it. */
const OPS: Record<string, string> = {
  gt: '>', greater_than: '>', gte: '>=', ge: '>=', greater_or_equal: '>=', lt: '<', less_than: '<', lte: '<=', le: '<=', less_or_equal: '<=',
  eq: '=', '==': '=', equals: '=', is: '=', ne: '!=', '<>': '!=', not_equals: '!=', neq: '!=', has: 'exists', not_null: 'exists', is_null: 'missing', not_exists: 'missing', none: 'missing', like: 'contains',
};
const normOp = (op: string) => OPS[op.trim().toLowerCase()] ?? op.trim();

interface Selected { nodes: NodeDto[]; q: Record<string, QuantitySet[]>; hint?: string }

/** The elements a filter picks, with their quantities when a field needs them. */
async function select(ctx: ToolContext, a: Record<string, unknown>, fields: string[] = []): Promise<Selected> {
  const { model } = ctx;
  const classes = resolveClasses(model, a.class);
  const storey = resolveStorey(model, a.storey);
  const type = typeof a.type === 'string' && a.type.trim() ? lc(a.type.trim()) : null;
  const name = typeof a.name_contains === 'string' && a.name_contains.trim() ? lc(a.name_contains.trim()) : null;
  const raw = (Array.isArray(a.where) ? a.where : a.where && typeof a.where === 'object' ? [a.where] : []) as Where[];
  for (const w of raw) if (!w || typeof w.field !== 'string' || typeof w.op !== 'string') throw new ToolError('Each condition needs a field and an op.');
  const where = raw.map((w) => ({ ...w, op: normOp(w.op) }));
  const sel = a.selected_only === true ? new Set(ctx.selection) : null;
  let nodes = [...model.nodes.values()].filter((n) => (classes ? classes.includes(n.type) : isElement(n)));
  if (storey) nodes = nodes.filter((n) => n.storeyId === storey.id);
  if (sel) nodes = nodes.filter((n) => sel.has(n.id));
  if (name) nodes = nodes.filter((n) => lc(n.name).includes(name));
  if (type) nodes = nodes.filter((n) => { const t = typeOf(model, n.id); return !!t && (t.id === a.type || lc(t.name).includes(type)); });
  const needQ = [...fields, ...where.map((w) => w.field)].some((f) => !isBuiltin(f));
  const q = needQ && nodes.length ? await quantitiesOf(ctx, nodes.map((n) => n.id)) : {};
  // A field no candidate has is said, with the fields they do have, so the model can ask again rightly.
  const unknown = where.filter((w) => w.op !== 'missing' && nodes.length > 0 && nodes.every((n) => fieldValue(model, n, w.field, q[n.id]) === undefined)).map((w) => w.field);
  const hint = unknown.length ? `No element here has ${unknown.join(', ')}. Fields they have: ${knownFields(nodes[0], q[nodes[0].id]).join(', ')}.` : undefined;
  if (where.length) nodes = nodes.filter((n) => where.every((w) => holds(fieldValue(model, n, w.field, q[n.id]), w)));
  return { nodes, q, hint };
}

/** What an element can be filtered by: its quantities, attributes and parameters. */
function knownFields(n: NodeDto, q: QuantitySet[] | undefined): string[] {
  const names = new Set<string>();
  for (const s of q ?? []) for (const x of s.quantities) names.add(x.name);
  for (const [k, v] of Object.entries(n.props)) if (v === null || typeof v !== 'object') names.add(k);
  for (const p of paramsOf(n)) names.add(p.pset ? `${p.pset}.${p.name}` : p.name);
  return [...names].slice(0, 30);
}

async function quantitiesOf(ctx: ToolContext, ids: string[]): Promise<Record<string, QuantitySet[]>> {
  const out: Record<string, QuantitySet[]> = {};
  for (let i = 0; i < ids.length; i += 2000) Object.assign(out, await ctx.quantities(ids.slice(i, i + 2000)));
  return out;
}

/** Storeys by their elevation, then by name. */
function byStorey(model: ModelState) {
  const order = new Map(storeys(model).map((s, i) => [s.id, i]));
  return (a: NodeDto, b: NodeDto) => (order.get(a.storeyId ?? '') ?? 1e9) - (order.get(b.storeyId ?? '') ?? 1e9) || a.name.localeCompare(b.name, undefined, { numeric: true });
}

/** JSON for the model within MAX_CHARS: rows dropped from the end until it fits, said with `truncated`. */
export function capped(result: Record<string, unknown>, rowsKey = 'rows'): { text: string; kept: number } {
  const rows = Array.isArray(result[rowsKey]) ? [...(result[rowsKey] as unknown[])] : null;
  let text = JSON.stringify(result);
  if (text.length <= MAX_CHARS || !rows) return { text: text.length <= MAX_CHARS ? text : text.slice(0, MAX_CHARS), kept: rows?.length ?? 0 };
  let keep = rows.length;
  while (keep > 0) {
    keep = Math.floor(keep * 0.75);
    text = JSON.stringify({ ...result, [rowsKey]: rows.slice(0, keep), truncated: true, rows_shown: keep });
    if (text.length <= MAX_CHARS) break;
  }
  return { text, kept: keep };
}

/** A page of rows for the model within MAX_CHARS: fewer rows when it would not fit, and where the next page starts. */
export function paged(head: Record<string, unknown>, rows: unknown[], offset: number, total: number): { text: string; shown: number } {
  let keep = rows.length;
  for (;;) {
    const next = offset + keep < total ? offset + keep : null;
    const text = JSON.stringify({ ...head, total, offset, rows: rows.slice(0, keep), next_offset: next, ...(keep < rows.length ? { truncated: true } : {}) });
    if (text.length <= MAX_CHARS || keep === 0) return { text: text.slice(0, MAX_CHARS), shown: keep };
    keep = Math.floor(keep * 0.75);
  }
}

const pageArgs = (a: Record<string, unknown>) => {
  const offset = Math.max(0, Math.floor(Number(a.offset) || 0));
  const limit = Math.min(MAX_LIMIT, Math.max(1, Math.floor(Number(a.limit) || DEFAULT_LIMIT)));
  return { offset, limit };
};

/** What a list was of, for the chat's line: the IFC classes, or all elements. */
const what = (cls: string[] | null) => (cls?.length ? cls.join(', ') : 'all');

// ─── The tools ──────────────────────────────────────────────────────────────

async function modelOverview(ctx: ToolContext): Promise<ToolOutcome> {
  const { model } = ctx;
  const elements = [...model.nodes.values()].filter(isElement);
  const project = [...model.nodes.values()].find((n) => n.type === 'IfcProject');
  const byClass: Record<string, number> = {};
  for (const n of elements) byClass[n.type] = (byClass[n.type] ?? 0) + 1;
  const typeUse = new Map<string, number>();
  for (const r of model.relations.values()) if (r.class === 'IfcRelDefinesByType') typeUse.set(r.relating, (typeUse.get(r.relating) ?? 0) + r.related.filter((id) => model.nodes.has(id)).length);
  const types = [...model.nodes.values()].filter((n) => /Type$/.test(n.type) && n.type.startsWith('Ifc'))
    .map((t) => ({ id: t.id, class: t.type, name: t.name, elements: typeUse.get(t.id) ?? 0 }))
    .sort((a, b) => a.class.localeCompare(b.class) || b.elements - a.elements).slice(0, 60);
  const result = {
    project: project?.name ?? null,
    storeys: storeys(model).map((s) => ({ id: s.id, name: s.name, elevation: round(Number(s.props.elevation ?? 0)), elements: elements.filter((n) => n.storeyId === s.id).length })),
    elements_by_class: byClass,
    total_elements: elements.length,
    types,
    materials: [...model.nodes.values()].filter((n) => n.type === 'IfcMaterial').map((m) => m.name).slice(0, 40),
    schedules: [...model.nodes.values()].filter((n) => n.type === 'LapinSchedule').map((s) => ({ id: s.id, name: s.name })),
    selected: ctx.selection.length,
    units: 'lengths m, areas m², volumes m³',
  };
  return { content: capped(result, 'types').text, summary: 'Read the model overview' };
}

async function listElements(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const { model } = ctx;
  const classes = resolveClasses(model, a.class);
  const asked = (Array.isArray(a.fields) ? a.fields.map(String) : []).filter((f) => !isBuiltin(f)).slice(0, 8);
  const sortBy = typeof a.sort_by === 'string' && a.sort_by.trim() ? a.sort_by.trim() : null;
  const { nodes, q: q0, hint } = await select(ctx, a, sortBy ? [...asked, sortBy] : asked);
  // The key quantities: one class, its base set's numbers (Width, Height, Length, Area…).
  let q = q0;
  if (!asked.length && classes?.length === 1 && nodes.length && !Object.keys(q).length) q = await quantitiesOf(ctx, nodes.slice(0, MAX_TABLE_ROWS).map((n) => n.id));
  const fields = asked.length ? asked : keyQuantities(nodes, q);
  const sorted = [...nodes].sort(byStorey(model));
  if (sortBy) {
    const val = (n: NodeDto) => fieldValue(model, n, sortBy, q[n.id]);
    sorted.sort((x, y) => compare(val(x), val(y)) * (a.descending === true ? -1 : 1));
  }
  const columns = ['id', 'class', 'name', 'storey', 'type', ...fields];
  const row = (n: NodeDto): Cell[] => [n.id, n.type, n.name, storeyName(model, n), typeName(model, n), ...fields.map((f) => fieldValue(model, n, f, q[n.id]) ?? null)];
  const { offset, limit } = pageArgs(a);
  const page = sorted.slice(offset, offset + limit);
  const obj = (r: Cell[]) => Object.fromEntries(columns.map((c, i) => [c, r[i]]).filter(([, v]) => v !== null));
  const { text, shown } = paged(hint ? { note: hint } : {}, page.map((n) => obj(row(n))), offset, sorted.length);
  return {
    content: text,
    summary: `Listed ${shown} of ${sorted.length} · ${what(classes)}`,
    table: { title: classes?.join(', ') ?? 'Elements', columns, rows: sorted.slice(0, MAX_TABLE_ROWS).map(row) },
  };
}

function keyQuantities(nodes: NodeDto[], q: Record<string, QuantitySet[]>): string[] {
  const first = nodes.find((n) => q[n.id]?.length);
  const base = first ? [...q[first.id]].sort((a, b) => rank(a.name) - rank(b.name))[0] : undefined;
  return base && rank(base.name) === 0 ? base.quantities.slice(0, 4).map((x) => x.name) : [];
}

function compare(x: Cell | undefined, y: Cell | undefined): number {
  if (x == null && y == null) return 0;
  if (x == null) return 1;
  if (y == null) return -1;
  if (typeof x === 'number' && typeof y === 'number') return x - y;
  return String(x).localeCompare(String(y), undefined, { numeric: true });
}

async function getElement(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const { model } = ctx;
  const n = typeof a.id === 'string' ? model.nodes.get(a.id) : undefined;
  if (!n) throw new ToolError(`No element with id „${String(a.id)}”: list_elements gives the ids.`);
  const type = typeOf(model, n.id);
  const attributes: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(n.props)) {
    if (k === 'params' || k === 'properties' || k === 'imported') continue;
    if (v === null || typeof v !== 'object') attributes[k] = typeof v === 'number' ? round(v) : v;
  }
  const params = (holder: NodeDto | null) => paramsOf(holder).map((p) => ({ set: p.pset ?? 'Lapin_Parameters', name: p.name, value: p.value }));
  const q = (await ctx.quantities([n.id]))[n.id] ?? [];
  const result = {
    id: n.id, class: n.type, name: n.name, storey: storeyName(model, n), type: type ? { id: type.id, name: type.name } : null,
    attributes,
    properties: params(n),
    type_properties: params(type),
    ifc_property_sets: importedSets(n).map((s) => ({ set: s.name, values: Object.fromEntries(s.props.slice(0, 40)) })).slice(0, 12),
    quantities: q.map((s) => ({ set: s.name, values: Object.fromEntries(s.quantities.map((x) => [x.name, round(x.value)])) })),
  };
  let text = JSON.stringify(result);
  if (text.length > MAX_CHARS) text = JSON.stringify({ ...result, ifc_property_sets: result.ifc_property_sets.slice(0, 3), truncated: true }).slice(0, MAX_CHARS);
  return { content: text, summary: `Read element ${n.name}` };
}

type Metric = { op: 'count' | 'sum' | 'avg' | 'min' | 'max'; field?: string };

async function aggregate(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const { model } = ctx;
  const groupBy = (Array.isArray(a.group_by) ? a.group_by.map(String) : typeof a.group_by === 'string' ? [a.group_by] : []).slice(0, 3);
  const metrics = ((Array.isArray(a.metrics) && a.metrics.length ? a.metrics : [{ op: 'count' }]) as Metric[]).slice(0, 6);
  for (const m of metrics) {
    if (!['count', 'sum', 'avg', 'min', 'max'].includes(m?.op)) throw new ToolError(`„${String(m?.op)}” is not count, sum, avg, min or max.`);
    if (m.op !== 'count' && !m.field) throw new ToolError(`${m.op} needs a field.`);
  }
  const { nodes, q, hint } = await select(ctx, a, [...groupBy, ...metrics.flatMap((m) => (m.field ? [m.field] : []))]);
  const groups = new Map<string, { keys: Cell[]; members: NodeDto[] }>();
  for (const n of nodes) {
    const keys = groupBy.map((g) => fieldValue(model, n, g, q[n.id]) ?? null);
    const k = JSON.stringify(keys);
    (groups.get(k) ?? groups.set(k, { keys, members: [] }).get(k)!).members.push(n);
  }
  const order = new Map(storeys(model).map((s, i) => [s.name, i]));
  const rankOf = (g: string, v: Cell) => (lc(g) === 'storey' ? order.get(String(v)) ?? 1e9 : 0);
  const list = [...groups.values()].sort((x, y) => {
    for (let i = 0; i < groupBy.length; i++) {
      const d = rankOf(groupBy[i], x.keys[i]) - rankOf(groupBy[i], y.keys[i]) || compare(x.keys[i], y.keys[i]);
      if (d) return d;
    }
    return 0;
  });
  const label = (m: Metric) => (m.op === 'count' ? 'count' : `${m.op}(${m.field})`);
  const compute = (members: NodeDto[], m: Metric): { value: number | null; missing: number } => {
    if (m.op === 'count') return { value: members.length, missing: 0 };
    const vals: number[] = [];
    let missing = 0;
    for (const n of members) {
      const v = fieldValue(model, n, m.field!, q[n.id]);
      const x = typeof v === 'number' ? v : typeof v === 'string' && v.trim() !== '' && Number.isFinite(Number(v)) ? Number(v) : null;
      if (x === null) missing++; else vals.push(x);
    }
    if (!vals.length) return { value: null, missing };
    const sum = vals.reduce((s, x) => s + x, 0);
    const value = m.op === 'sum' ? sum : m.op === 'avg' ? sum / vals.length : m.op === 'min' ? Math.min(...vals) : Math.max(...vals);
    return { value: round(value), missing };
  };
  const columns = [...groupBy, ...metrics.map(label)];
  const rows: Cell[][] = list.map((g) => [...g.keys, ...metrics.map((m) => compute(g.members, m).value)]);
  const totals = metrics.map((m) => compute(nodes, m));
  const missing = Object.fromEntries(metrics.map((m, i) => [label(m), totals[i].missing]).filter(([, v]) => v));
  const result: Record<string, unknown> = {
    elements: nodes.length,
    ...(hint ? { note: hint } : {}),
    groups: rows.slice(0, 200).map((r) => Object.fromEntries(columns.map((c, i) => [c, r[i]]))),
    total: Object.fromEntries(metrics.map((m, i) => [label(m), totals[i].value])),
    ...(Object.keys(missing).length ? { elements_without_the_field: missing } : {}),
    units: 'lengths m, areas m², volumes m³',
  };
  const { text } = capped(result, 'groups');
  const tableRows = groupBy.length ? [...rows, [...groupBy.map((_, i) => (i === 0 ? 'Total' : null)), ...totals.map((t) => t.value)]] : rows;
  return {
    content: text,
    summary: `Computed ${groupBy.length ? rows.length : 1} group(s) over ${nodes.length} element(s)`,
    table: { title: groupBy.length ? `Grouped by ${groupBy.join(', ')}` : 'Totals', columns, rows: tableRows.slice(0, MAX_TABLE_ROWS) },
  };
}

async function propertyNames(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const classes = resolveClasses(ctx.model, a.class) ?? [];
  const table = await ctx.standard();
  const used = new Map<string, Set<string>>();
  for (const n of ctx.model.nodes.values()) {
    if (!classes.includes(n.type)) continue;
    for (const p of paramsOf(n)) { const set = p.pset ?? table.own; (used.get(set) ?? used.set(set, new Set()).get(set)!).add(p.name); }
  }
  const result = {
    classes,
    standard_sets: table.sets.filter((s) => classes.some((c) => appliesTo(s.classes, c))).map((s) => ({ set: s.name, properties: s.properties })),
    used_in_model: [...used].map(([set, names]) => ({ set, names: [...names].slice(0, 30) })),
    rule: `Standard sets (Pset_…) take only the names listed. Anything else goes in a set of the user's own (not starting with Pset_ or Qto_) or in ${table.own}.`,
  };
  return { content: capped(result).text, summary: 'Read the standard property sets' };
}

async function listSchedules(ctx: ToolContext): Promise<ToolOutcome> {
  const list = [...ctx.model.nodes.values()].filter((n) => n.type === 'LapinSchedule').map((s) => ({ id: s.id, name: s.name, kind: s.props.kind ?? null }));
  return { content: JSON.stringify({ schedules: list }).slice(0, MAX_CHARS), summary: 'Listed the schedules' };
}

async function readSchedule(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const s = typeof a.id === 'string' ? ctx.model.nodes.get(a.id) : undefined;
  if (!s || s.type !== 'LapinSchedule') throw new ToolError(`No schedule with id „${String(a.id)}”: list_schedules gives them.`);
  const csv = await ctx.scheduleCsv(s.id);
  if (csv == null) throw new ToolError('The schedule could not be read.');
  const [columns = [], ...rows] = parseCsv(csv);
  const { offset, limit } = pageArgs(a);
  const page = rows.slice(offset, offset + limit);
  const { text } = paged({ schedule: s.name, columns }, page, offset, rows.length);
  return { content: text, summary: `Read schedule ${s.name}`, table: { title: s.name, columns, rows: rows.slice(0, MAX_TABLE_ROWS) } };
}

async function proposeProperties(ctx: ToolContext, a: Record<string, unknown>): Promise<ToolOutcome> {
  const changes = (Array.isArray(a.changes) ? a.changes : []) as ProposedValue[];
  if (!changes.length) throw new ToolError('No changes given: changes is a list of {id, pset, name, value}.');
  if (changes.length > 500) throw new ToolError('At most 500 changes at a time.');
  const table = await ctx.standard();
  const rows: ChangeRow[] = [];
  const refused: { id: unknown; name: unknown; why: string }[] = [];
  for (const c of changes) {
    const r = checkProposal(c ?? ({} as ProposedValue), ctx.model, table);
    if (r.ok) rows.push(r.row); else refused.push({ id: c?.id, name: c?.name, why: r.why });
  }
  const result = {
    proposed: rows.length,
    refused: refused.slice(0, 20),
    note: rows.length ? 'Shown to the user for approval. Nothing is applied until the user applies it; do not say it is done.' : 'Nothing to show.',
  };
  return {
    content: JSON.stringify(result).slice(0, MAX_CHARS),
    summary: refused.length ? `Proposed ${rows.length} change(s), ${refused.length} refused` : `Proposed ${rows.length} change(s)`,
    proposals: rows,
    error: rows.length === 0,
  };
}

/** Run one tool by name, its arguments as the model sent them (an object, or JSON text). Never throws: a failure is a result the model reads. */
export async function runTool(ctx: ToolContext, name: string, rawArgs: unknown): Promise<ToolOutcome> {
  let args: Record<string, unknown>;
  try {
    args = typeof rawArgs === 'string' ? (rawArgs.trim() ? JSON.parse(rawArgs) as Record<string, unknown> : {}) : (rawArgs as Record<string, unknown> | null) ?? {};
    if (typeof args !== 'object' || Array.isArray(args)) throw new ToolError('The arguments must be an object.');
  } catch (e) {
    return { content: JSON.stringify({ error: `Arguments not readable: ${e instanceof Error ? e.message : String(e)}` }), summary: `Tool failed: ${name}`, error: true };
  }
  try {
    switch (name as ToolName) {
      case 'model_overview': return await modelOverview(ctx);
      case 'list_elements': return await listElements(ctx, args);
      case 'get_element': return await getElement(ctx, args);
      case 'aggregate': return await aggregate(ctx, args);
      case 'property_names': return await propertyNames(ctx, args);
      case 'list_schedules': return await listSchedules(ctx);
      case 'read_schedule': return await readSchedule(ctx, args);
      case 'propose_properties': return await proposeProperties(ctx, args);
      default: return { content: JSON.stringify({ error: `No tool „${name}”. Tools: ${TOOL_DEFS.map((t) => t.function.name).join(', ')}.` }), summary: `Tool failed: ${name}`, error: true };
    }
  } catch (e) {
    const why = e instanceof Error ? e.message : String(e);
    return { content: JSON.stringify({ error: why }), summary: `Tool failed: ${name}`, error: true };
  }
}

/** A table as CSV (comma-separated, quoted where needed), for the user to save. */
export function tableCsv(t: Table): string {
  const cell = (v: Cell) => { const s = v === null ? '' : String(v); return /[",\n;]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; };
  return [t.columns, ...t.rows].map((r) => r.map(cell).join(',')).join('\n') + '\n';
}
