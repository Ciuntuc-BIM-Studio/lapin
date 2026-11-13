/**
 * A graph palette's values as each user's own (decision 537, the follow-up to 536). The graph's tool setting **Palette
 * values** is either "shared" (the default: what the palette changes is written into the graph, so everyone sees it) or
 * "own": the graph's values are then only the defaults, and what a user changes in the palette is kept on their own
 * computer, per project (its IfcProject's GlobalId) and per graph, never written into the graph. Preview and Bake use the
 * user's values (graphs.bake with `values`, which an "own" graph does not write); Save as the graph's values (graphs.set)
 * makes them everyone's, Reset to the graph's values forgets them.
 *
 * Kept in the browser's storage (localStorage), on the desktop and the web alike: the desktop's engine has a user folder
 * (`scripting-trust.json`), but the web's runs in a Web Worker with none, and the values are only the interface's (the
 * engine is given them with each evaluation or Bake). The storage is per user of the computer already (the desktop's
 * webview keeps its own, the browser its profile's), as My tools and a palette's place are.
 *
 * A stored value that no longer fits its input (the node is gone or no longer an input of that kind, a choice no longer
 * offered, a number out of its least and most, a storey or type gone) is left out: the field falls back to the graph's.
 */

import type { AskField } from '../../shell/askFields';
import type { GraphJson } from './graph';
import type { GraphTool, Palette } from './palette';

export type PaletteValues = 'shared' | 'own';

/** A graph's Palette values setting: "own" only when the graph's tool says so. */
export const paletteValuesOf = (graph: GraphJson): PaletteValues => ((graph.tool ?? {}) as GraphTool).values === 'own' ? 'own' : 'shared';

/**
 * Whether a value fits a field as the graph's input node takes it: a finite number (whole for a whole number, within its
 * least and most), a text, yes or no, one of the choices, a list of element ids, a storey's or a type's id ('' for none).
 * `exists`, when given, says whether an id is still in the model (elements gone are dropped, a storey or type gone does not fit).
 */
export function fits(f: AskField, v: unknown, exists?: (id: string) => boolean): boolean {
  switch (f.kind) {
    case 'number':
      if (typeof v !== 'number' || !Number.isFinite(v)) return false;
      if (f.integer && !Number.isInteger(v)) return false;
      return (f.min === undefined || v >= f.min) && (f.max === undefined || v <= f.max);
    case 'text': return typeof v === 'string';
    case 'bool': return typeof v === 'boolean';
    case 'choice': return typeof v === 'string' && (f.choices ?? []).some((c) => c.value === v);
    case 'element': return Array.isArray(v) && v.every((x) => typeof x === 'string');
    case 'storey':
    case 'type': return typeof v === 'string' && (v === '' || !exists || exists(v));
  }
}

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

/**
 * The stored values that still fit the palette's inputs and differ from the graph's own (by path): the user's values
 * as they apply now. Elements no longer in the model are dropped from a pick.
 */
export function fittingOwn(p: Palette, stored: Record<string, unknown>, exists?: (id: string) => boolean): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const { path, field } of p.inputs) {
    if (!(path in stored)) continue;
    let v = stored[path];
    if (field.kind === 'element' && Array.isArray(v) && exists) v = v.filter((x) => typeof x === 'string' && exists(x));
    if (!fits(field, v, exists) || same(v, field.value)) continue;
    out[path] = v;
  }
  return out;
}

/** The palette with the user's values in its fields (those given), the graph's elsewhere. */
export function withOwn(p: Palette, own: Record<string, unknown>): Palette {
  if (Object.keys(own).length === 0) return p;
  return { ...p, inputs: p.inputs.map((i) => (i.path in own ? { ...i, field: { ...i.field, value: own[i.path] } } : i)) };
}

// ── kept on this computer ──

/** Where a user's values of one graph of one project are kept. */
export const ownKey = (projectId: string, graphId: string) => `lapin.graphValues.${projectId}.${graphId}`;

/** The user's values of a graph as kept (by path; {} when none or unreadable). */
export function readOwn(projectId: string, graphId: string): Record<string, unknown> {
  try {
    const v = JSON.parse(localStorage.getItem(ownKey(projectId, graphId)) ?? 'null');
    return v && typeof v === 'object' && !Array.isArray(v) ? v as Record<string, unknown> : {};
  } catch { return {}; }
}

/** The user's values of a graph kept (none: forgotten). */
export function keepOwn(projectId: string, graphId: string, values: Record<string, unknown>): void {
  try {
    if (Object.keys(values).length === 0) localStorage.removeItem(ownKey(projectId, graphId));
    else localStorage.setItem(ownKey(projectId, graphId), JSON.stringify(values));
  } catch { /* this session only */ }
}
