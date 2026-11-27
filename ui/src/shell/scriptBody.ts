/**
 * What Properties shows of an element a script made (element.createBody) beyond its fields: the material it is associated
 * with and its parts' own materials, read only (a typed element shows its type's), and the script's own values
 * (externalAttrs) as text. Pure, over the session's model, so the panel and its tests share it.
 */

import type { NodeDto } from '../engine/protocol';
import { materialOf, typeOf, type ModelState } from '../engine/model';

export interface ElementMaterials {
  /** Its own material's name (IfcRelAssociatesMaterial to an IfcMaterial); null: none. */
  own: string | null;
  /** The names of the materials its parts are made of, each once, in order (a script body's parts[].material). */
  parts: string[];
}

/** A material's name by its id, or the text as it is (a part may name a material the project does not hold). */
function nameOf(s: ModelState, idOrName: string): string {
  const n = s.nodes.get(idOrName);
  return n?.type === 'IfcMaterial' ? n.name : idOrName;
}

/**
 * The materials an untyped element is made of, as Properties shows them read only; null when there is nothing to show (a typed
 * element, whose type's section shows its material; a layered one; an element of no material).
 */
export function elementMaterials(s: ModelState, node: NodeDto): ElementMaterials | null {
  if (typeOf(s, node.id)) return null;
  const rel = materialOf(s, node.id);
  const own = rel?.type === 'IfcMaterial' ? rel.name : null;
  const ids = Array.isArray(node.props.partMaterials) ? (node.props.partMaterials as unknown[]).filter((x): x is string => typeof x === 'string') : [];
  const parts = [...new Set(ids.map((id) => nameOf(s, id)))];
  return own || parts.length ? { own, parts } : null;
}

/** A value of the script's own as one line of text: a number, a word, a list or an object as JSON. */
function text(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  const json = JSON.stringify(v);
  return json.length > 120 ? `${json.slice(0, 117)}…` : json;
}

/** The script's own values (externalAttrs), each its key and its text, in the order it gave them. */
export function scriptValues(node: NodeDto): [string, string][] {
  const a = node.props.externalAttrs;
  if (!a || typeof a !== 'object' || Array.isArray(a)) return [];
  return Object.entries(a as Record<string, unknown>).map(([k, v]) => [k, text(v)]);
}
