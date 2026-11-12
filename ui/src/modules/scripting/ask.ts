/**
 * `lapin.ui.ask`'s dialog, its logic (docs/plan-python-scripting.md §6, stage 4; decision 489): a script's question
 * (the host's `script.ask`, come as a `script.output` event of type `ask`) read into fields the window draws itself —
 * a number with its unit, a text, a choice, a checkbox, an element of the model (the selection, or one of a list), a
 * storey, a type of a class — never anything of the script's to run; their first values; and what the user typed checked
 * and turned into the answer (`script.answer {ask, values}`). Kept apart from what draws it (AskDialog.tsx), for the tests.
 */

export type FieldKind = 'number' | 'text' | 'choice' | 'bool' | 'element' | 'storey' | 'type';

export interface AskField {
  key: string;
  kind: FieldKind;
  label: string;
  value?: unknown;
  unit?: string;
  min?: number;
  max?: number;
  step?: number;
  integer?: boolean;
  multiline?: boolean;
  choices?: { value: string | number | boolean; label: string }[];
  /** element: the IFC classes it may be (any when none). */
  classes?: string[];
  multiple?: boolean;
  /** element: start from the window's selection. */
  selection?: boolean;
  required?: boolean;
  /** type: its class. */
  cls?: string;
}

export interface AskQuestion { ask: string; title: string; text?: string; ok?: string; fields: AskField[] }

const KINDS = new Set<FieldKind>(['number', 'text', 'choice', 'bool', 'element', 'storey', 'type']);
const str = (v: unknown, max: number): string | undefined => (typeof v === 'string' && v.trim() ? v.trim().slice(0, max) : undefined);
const num = (v: unknown): number | undefined => (typeof v === 'number' && Number.isFinite(v) ? v : undefined);

/** A question as the event brought it, read so that only what the dialog knows how to draw is kept; null when it is not one. */
export function readQuestion(e: unknown): AskQuestion | null {
  if (!e || typeof e !== 'object') return null;
  const o = e as Record<string, unknown>;
  const ask = str(o.ask, 64);
  if (!ask || !Array.isArray(o.fields)) return null;
  const fields: AskField[] = [];
  const seen = new Set<string>();
  for (const raw of o.fields.slice(0, 64)) {
    if (!raw || typeof raw !== 'object') continue;
    const f = raw as Record<string, unknown>;
    const key = str(f.key, 64);
    const kind = f.kind as FieldKind;
    if (!key || seen.has(key) || !KINDS.has(kind)) continue;
    seen.add(key);
    const field: AskField = { key, kind, label: str(f.label, 120) ?? key };
    if (f.value !== undefined) field.value = f.value;
    const unit = str(f.unit, 20); if (unit) field.unit = unit;
    for (const k of ['min', 'max', 'step'] as const) { const n = num(f[k]); if (n !== undefined) field[k] = n; }
    if (f.integer === true) field.integer = true;
    if (f.multiline === true) field.multiline = true;
    if (f.multiple === true) field.multiple = true;
    if (f.selection === false) field.selection = false; else if (kind === 'element') field.selection = true;
    if (f.required === false) field.required = false;
    const cls = str(f.cls, 80); if (cls) field.cls = cls;
    if (Array.isArray(f.classes)) field.classes = f.classes.filter((c): c is string => typeof c === 'string' && !!c).slice(0, 20);
    if (Array.isArray(f.choices)) {
      field.choices = f.choices.flatMap((c) => {
        if (!c || typeof c !== 'object') return [];
        const v = (c as { value?: unknown }).value;
        if (!['string', 'number', 'boolean'].includes(typeof v)) return [];
        return [{ value: v as string | number | boolean, label: str((c as { label?: unknown }).label, 120) ?? String(v) }];
      }).slice(0, 500);
      if (!field.choices.length) continue;
    } else if (kind === 'choice') continue;
    fields.push(field);
  }
  if (!fields.length) return null;
  return { ask, title: str(o.title, 200) ?? 'Script', text: str(o.text, 2000), ok: str(o.ok, 40), fields };
}

/** What the dialog knows of the model: its entities (id → class, name, attributes), the selection, the active storey. */
export interface ModelFacts {
  nodes: ReadonlyMap<string, { id: string; type: string; name: string; props: Record<string, unknown> }>;
  selection: readonly string[];
  storey: string | null;
}

/** What a field holds while the dialog is open: a number or a text as typed, a checkbox, a choice's index, ids. */
export type FieldState = string | boolean | number | string[];

export const STOREY = 'IfcBuildingStorey';

/** The storeys, lowest first. */
export function storeysOf(m: ModelFacts): { id: string; name: string }[] {
  return [...m.nodes.values()].filter((n) => n.type === STOREY)
    .sort((a, b) => (Number(a.props.elevation) || 0) - (Number(b.props.elevation) || 0)).map((n) => ({ id: n.id, name: n.name }));
}

/** The types of a class, by name. */
export function typesOf(cls: string | undefined, m: ModelFacts): { id: string; name: string }[] {
  if (!cls) return [];
  return [...m.nodes.values()].filter((n) => n.type === cls).map((n) => ({ id: n.id, name: n.name })).sort((a, b) => a.name.localeCompare(b.name));
}

/** Whether an element may be the field's: of one of its classes (any element when none is named). */
export const fits = (f: AskField, type: string | undefined): boolean => !!type && (!f.classes?.length || f.classes.includes(type));

/** The elements a field offers in its list: of its classes, by name (at most `limit`). */
export function elementsOf(f: AskField, m: ModelFacts, limit = 500): { id: string; name: string; type: string }[] {
  if (!f.classes?.length) return [];
  return [...m.nodes.values()].filter((n) => fits(f, n.type)).slice(0, limit).map((n) => ({ id: n.id, name: n.name || n.id, type: n.type })).sort((a, b) => a.name.localeCompare(b.name));
}

/** The window's selection as a field takes it: the elements of its classes (one, unless it is multiple). */
export function fromSelection(f: AskField, m: ModelFacts): string[] {
  const ids = m.selection.filter((id) => fits(f, m.nodes.get(id)?.type));
  return f.multiple ? ids : ids.slice(0, 1);
}

const idOf = (v: unknown): string | null => (typeof v === 'string' && v ? v : null);

/** Each field's first state: the script's value, else (an element) the selection, (a storey) the active one, (a type, a choice) the first. */
export function initialState(fields: readonly AskField[], m: ModelFacts): Record<string, FieldState> {
  const s: Record<string, FieldState> = {};
  for (const f of fields) {
    switch (f.kind) {
      case 'number': s[f.key] = typeof f.value === 'number' && Number.isFinite(f.value) ? String(f.value) : ''; break;
      case 'text': s[f.key] = typeof f.value === 'string' ? f.value : ''; break;
      case 'bool': s[f.key] = f.value === true; break;
      case 'choice': s[f.key] = Math.max(0, f.choices!.findIndex((c) => c.value === f.value)); break;
      case 'element': {
        const given = (Array.isArray(f.value) ? f.value : [f.value]).map(idOf).filter((x): x is string => !!x && m.nodes.has(x));
        s[f.key] = given.length ? given : f.selection !== false ? fromSelection(f, m) : [];
        break;
      }
      case 'storey': {
        const list = storeysOf(m);
        const given = idOf(f.value);
        // The script's, else the window's active storey, else the one at ±0.00 (as lapin.model.storey() finds it), else the lowest.
        const ground = [...m.nodes.values()].find((n) => n.type === STOREY && Math.abs(Number(n.props.elevation) || 0) < 1e-3)?.id;
        s[f.key] = (given && list.some((x) => x.id === given) ? given : m.storey && list.some((x) => x.id === m.storey) ? m.storey : ground ?? list[0]?.id) ?? '';
        break;
      }
      case 'type': {
        const list = typesOf(f.cls, m);
        const given = idOf(f.value);
        s[f.key] = (given && list.some((x) => x.id === given) ? given : f.required === false ? '' : list[0]?.id) ?? '';
        break;
      }
    }
  }
  return s;
}

/** A number as typed: a comma or a point for decimals, spaces between thousands; null when it is not one. */
export function readNumber(text: string): number | null {
  const t = text.trim().replace(/\s+/g, '').replace(',', '.');
  if (!/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(t)) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** What the user chose, checked: the values to answer with, or the first field that is wrong and why. */
export function collect(fields: readonly AskField[], state: Record<string, FieldState>): { values: Record<string, unknown> } | { key: string; error: string } {
  const values: Record<string, unknown> = {};
  for (const f of fields) {
    const v = state[f.key];
    switch (f.kind) {
      case 'number': {
        const n = readNumber(String(v ?? ''));
        if (n === null) return { key: f.key, error: 'Type a number.' };
        if (f.integer && !Number.isInteger(n)) return { key: f.key, error: 'Type a whole number.' };
        if (f.min !== undefined && n < f.min) return { key: f.key, error: `At least ${f.min}${f.unit ? ` ${f.unit}` : ''}.` };
        if (f.max !== undefined && n > f.max) return { key: f.key, error: `At most ${f.max}${f.unit ? ` ${f.unit}` : ''}.` };
        values[f.key] = n;
        break;
      }
      case 'text': values[f.key] = String(v ?? ''); break;
      case 'bool': values[f.key] = v === true; break;
      case 'choice': values[f.key] = f.choices![typeof v === 'number' && v >= 0 && v < f.choices!.length ? v : 0].value; break;
      case 'element': {
        const ids = Array.isArray(v) ? v : [];
        if (!ids.length && f.required !== false) return { key: f.key, error: f.multiple ? 'Select the elements in the model, then Use selection.' : 'Choose an element.' };
        values[f.key] = f.multiple ? ids : ids[0] ?? null;
        break;
      }
      case 'storey':
      case 'type': {
        const id = typeof v === 'string' && v ? v : null;
        if (!id && (f.kind === 'storey' || f.required !== false)) return { key: f.key, error: f.kind === 'storey' ? 'Choose a storey.' : 'Choose a type.' };
        values[f.key] = id;
        break;
      }
    }
  }
  return { values };
}
