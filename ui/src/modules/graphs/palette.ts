/**
 * A graph's palette (docs/plan-visual-graphs.md §8 stage 2, decision 536), its logic: the graph's input nodes as the
 * fields of a small floating panel (the same fields as a script's palette and `lapin.ui.ask`, shell/askFields.ts), its
 * Watch nodes and the nodes marked "Show in palette" as read-only values, and the graph as a tool of My tools. Pure
 * functions over the graph's JSON, kept apart from what draws them (PaletteWindow.tsx), for the tests.
 *
 * A field's value belongs to the graph: while a field changes the graph is evaluated with it (graphs.evaluate's
 * `values`, no undo step), and once it is let go of it is written into the input node (graphs.set, one undo step), so
 * the canvas, Bake and a colleague opening the project see the same values.
 */

import { readNumber, type AskField, type FieldState } from '../../shell/askFields';
import { showValue, type GraphJson, type GraphNode, type NodeSpec } from './graph';

/** What a node shows in the palette (format 2): an input's field label, hidden, order, least, most, step; an output shown. */
export interface NodePalette { label?: string; hide?: boolean; order?: number; min?: number; max?: number; step?: number; show?: boolean }

/** The graph as a tool of My tools (format 2): shown, its button's name, icon and tooltip, its group. */
export interface GraphTool { show?: boolean; name?: string; icon?: string; tooltip?: string; group?: string }

/** The group of My tools a graph's tool goes in when it names none. */
export const GRAPHS_GROUP = 'Graphs';
/** A graph's tool's id in My tools. */
export const toolIdOf = (graphId: string) => `graph:${graphId}`;

/** The input nodes' kinds, and the setting each keeps its value in (as the engine's Builtins.InputPort). */
export const INPUT_PORT: Record<string, string> = { number: 'value', integer: 'value', text: 'value', bool: 'value', choice: 'value', storey: 'value', type: 'value', pick: 'ids' };

export const isInput = (kind: string): boolean => kind in INPUT_PORT;

/**
 * Every node of a graph with its path (a group's nodes as group/node), groups' nodes after their group, and where it
 * stands for the palette's order: on the canvas, or (inside a group) where its group stands, then within the group.
 */
export function allNodes(graph: { nodes: GraphNode[] }, prefix = '', at?: number[]): { path: string; node: GraphNode; place: number[] }[] {
  const out: { path: string; node: GraphNode; place: number[] }[] = [];
  for (const n of graph.nodes) {
    const place = [...(at ?? []), n.y, n.x];
    out.push({ path: prefix + n.id, node: n, place });
    if (n.graph) out.push(...allNodes(n.graph, `${prefix}${n.id}/`, place));
  }
  return out;
}

const byPlace = (a: { place: number[] }, b: { place: number[] }): number => {
  for (let i = 0; i < Math.max(a.place.length, b.place.length); i++) {
    const d = (a.place[i] ?? -Infinity) - (b.place[i] ?? -Infinity);
    if (d) return d;
  }
  return 0;
};

export interface PaletteInput { path: string; node: GraphNode; field: AskField }
export interface PaletteOutput { path: string; node: GraphNode; label: string }
export interface Palette { inputs: PaletteInput[]; outputs: PaletteOutput[] }

const UNITS: Record<string, string> = { length: 'm', angle: '°' };

/** An input node's field: its kind's, its label (the palette's, else the node's name, else its kind's), its limits, its value. */
export function fieldOf(path: string, node: GraphNode, spec: NodeSpec | undefined, label: (s: string) => string = (s) => s): AskField | null {
  const p = (node.palette ?? {}) as NodePalette;
  const v = node.values ?? {};
  const name = p.label ?? node.label ?? label(spec?.label ?? node.kind);
  const limits = { ...(p.min !== undefined ? { min: p.min } : {}), ...(p.max !== undefined ? { max: p.max } : {}), ...(p.step !== undefined ? { step: p.step } : {}) };
  const def = (port: string) => v[port] ?? spec?.inputs.find((x) => x.name === port)?.default;
  switch (node.kind) {
    case 'number': {
      const unit = UNITS[String(def('unit') ?? '')];
      return { key: path, kind: 'number', label: name, value: def('value') ?? 0, ...(unit ? { unit } : {}), ...limits };
    }
    case 'integer': return { key: path, kind: 'number', label: name, value: def('value') ?? 0, integer: true, ...limits };
    case 'text': return { key: path, kind: 'text', label: name, value: String(def('value') ?? '') };
    case 'bool': return { key: path, kind: 'bool', label: name, value: def('value') === true };
    case 'choice': {
      const options = String(def('options') ?? '').split(',').map((x) => x.trim()).filter(Boolean);
      if (!options.length) return null;
      return { key: path, kind: 'choice', label: name, value: def('value'), choices: options.map((o) => ({ value: o, label: o })) };
    }
    case 'pick': return { key: path, kind: 'element', label: name, value: Array.isArray(v.ids) ? v.ids : [], multiple: true, selection: false, required: false };
    case 'storey': return { key: path, kind: 'storey', label: name, value: String(def('value') ?? ''), required: false };
    case 'type': return { key: path, kind: 'type', label: name, value: String(def('value') ?? ''), cls: String(def('class') ?? 'IfcWallType'), required: false };
    default: return null;
  }
}

/**
 * The palette of a graph: its input nodes (at any depth, those not hidden) as fields, in the order set (those without
 * one after, by where they stand on the canvas: top to bottom, left to right); its Watch nodes (not hidden) and the nodes
 * marked "Show in palette" as outputs, in the canvas's order.
 */
export function paletteOf(graph: GraphJson, specOf: (kind: string) => NodeSpec | undefined, label: (s: string) => string = (s) => s): Palette {
  const nodes = allNodes(graph);
  const inputs = nodes.filter(({ node }) => isInput(node.kind) && !(node.palette as NodePalette | undefined)?.hide)
    .sort((a, b) => ((a.node.palette as NodePalette | undefined)?.order ?? Infinity) - ((b.node.palette as NodePalette | undefined)?.order ?? Infinity) || byPlace(a, b))
    .flatMap(({ path, node }) => { const field = fieldOf(path, node, specOf(node.kind), label); return field ? [{ path, node, field }] : []; });
  const outputs = nodes.filter(({ node }) => {
    const p = node.palette as NodePalette | undefined;
    return node.kind === 'watch' ? !p?.hide && p?.show !== false : p?.show === true && !isInput(node.kind);
  }).sort(byPlace).map(({ path, node }) => ({ path, node, label: (node.palette as NodePalette | undefined)?.label ?? node.label ?? label(specOf(node.kind)?.label ?? node.kind) }));
  return { inputs, outputs };
}

/** A field's state from its value as the graph keeps it: a number as text, a choice by its index, ids, an id ('' for none). */
export function stateOf(f: AskField): FieldState {
  switch (f.kind) {
    case 'number': return typeof f.value === 'number' && Number.isFinite(f.value) ? String(f.value) : '';
    case 'text': return typeof f.value === 'string' ? f.value : '';
    case 'bool': return f.value === true;
    case 'choice': return Math.max(0, f.choices!.findIndex((c) => c.value === f.value));
    case 'element': return Array.isArray(f.value) ? (f.value as unknown[]).filter((x): x is string => typeof x === 'string') : [];
    case 'storey':
    case 'type': return typeof f.value === 'string' ? f.value : '';
  }
}

/** A field's state as the value the graph keeps, checked (a number within its least and most), or why it is not one. */
export function valueOf(f: AskField, s: FieldState): { value: unknown } | { error: string } {
  switch (f.kind) {
    case 'number': {
      const n = readNumber(String(s ?? ''));
      if (n === null) return { error: 'Type a number.' };
      if (f.integer && !Number.isInteger(n)) return { error: 'Type a whole number.' };
      if (f.min !== undefined && n < f.min) return { error: `At least ${f.min}${f.unit ? ` ${f.unit}` : ''}.` };
      if (f.max !== undefined && n > f.max) return { error: `At most ${f.max}${f.unit ? ` ${f.unit}` : ''}.` };
      return { value: n };
    }
    case 'text': return { value: String(s ?? '') };
    case 'bool': return { value: s === true };
    case 'choice': return { value: f.choices![typeof s === 'number' && s >= 0 && s < f.choices!.length ? s : 0]!.value };
    case 'element': return { value: Array.isArray(s) ? s : [] };
    case 'storey':
    case 'type': return { value: typeof s === 'string' ? s : '' };
  }
}

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

/**
 * The fields changed from what the graph keeps, as graphs.evaluate's and graphs.set's `values` (by path); the first
 * field that does not read, with why. A field that does not read is left out of the values (the graph keeps its own).
 */
export function changedValues(p: Palette, state: Record<string, FieldState>): { values: Record<string, unknown>; wrong: { key: string; error: string } | null } {
  const values: Record<string, unknown> = {};
  let wrong: { key: string; error: string } | null = null;
  for (const { path, field } of p.inputs) {
    if (!(path in state)) continue;
    const r = valueOf(field, state[path]!);
    if ('error' in r) { wrong ??= { key: path, error: r.error }; continue; }
    const kept = valueOf(field, stateOf(field));
    if (!('value' in kept) || !same(kept.value, r.value)) values[path] = r.value;
  }
  return { values, wrong };
}

/** An output's value as the palette shows it: a value, a short list, or a small table (a list of lists); with how many in all. */
export type Shown =
  | { kind: 'value'; text: string }
  | { kind: 'list'; items: string[]; total: number }
  | { kind: 'table'; rows: string[][]; total: number };

/** The most items or rows a palette shows of a list. */
export const SHOWN_ITEMS = 8;

const realItems = (v: unknown[]): { items: unknown[]; total: number } => {
  const more = v.find((x) => x && typeof x === 'object' && '$more' in (x as object)) as { $more?: number } | undefined;
  const items = v.filter((x) => !(x && typeof x === 'object' && '$more' in (x as object)));
  return { items, total: items.length + (more?.$more ?? 0) };
};

/** An output's value (as graphs.evaluate gives it) as the palette shows it. */
export function shown(v: unknown): Shown {
  if (!Array.isArray(v)) return { kind: 'value', text: showValue(v) };
  const { items, total } = realItems(v);
  if (items.length > 0 && items.every((x) => Array.isArray(x))) {
    const rows = items.slice(0, SHOWN_ITEMS).map((r) => realItems(r as unknown[]).items.slice(0, 6).map((x) => showValue(x, 1)));
    return { kind: 'table', rows, total };
  }
  return { kind: 'list', items: items.slice(0, SHOWN_ITEMS).map((x) => showValue(x, 1)), total };
}

/** A graph's tool as My tools shows it (none when it is not shown): the button's name (the graph's, else), icon, tooltip, group. */
export function toolOf(graph: GraphJson, graphName: string): { name: string; icon?: string; tooltip?: string; group: string } | null {
  const t = (graph.tool ?? {}) as GraphTool;
  if (t.show !== true) return null;
  return { name: t.name?.trim() || graphName, ...(t.icon ? { icon: t.icon } : {}), ...(t.tooltip ? { tooltip: t.tooltip } : {}), group: t.group?.trim() || GRAPHS_GROUP };
}

/** Where a palette floats, kept inside the window (its header reachable). */
export function clampAt(x: number, y: number, win: { w: number; h: number }, size = { w: 300, h: 40 }): { x: number; y: number } {
  return { x: Math.max(0, Math.min(x, win.w - Math.min(size.w, win.w))), y: Math.max(0, Math.min(y, win.h - size.h)) };
}

