/**
 * Selecting what is like the selection (forum topic 73): the elements of the same class and the same type (the same
 * class when the element has no type), or sharing one more thing the element is told by (its width, its core material, its
 * storey). Pure functions over the session's model, so the menu and the tests share them.
 */

import type { NodeDto } from '../engine/protocol';
import { layersOf, materialOf, typeLayers, typeOf, type ModelState } from '../engine/model';

/** What a selected element can be matched by, besides its class. */
export type SimilarKey = 'type' | 'width' | 'coreMaterial' | 'material' | 'storey';

export interface Criterion { key: SimilarKey; label: string; value: string }

/** The layers an element is built of: its type's, else its own layer set (a plain element has none). */
function layersFor(model: ModelState, id: string) {
  const type = typeOf(model, id);
  const own = type ? typeLayers(model, type.id) : [];
  if (own.length) return own;
  const set = materialOf(model, id);
  return set?.type === 'IfcMaterialLayerSet' ? layersOf(set) : [];
}

const mmOf = (m: number) => `${Math.round(m * 1000)} mm`;

/** What `id` is told apart by: each criterion's key, its label (a phrase of the interface) and its value as text. */
export function criteriaOf(model: ModelState, id: string): Criterion[] {
  const node = model.nodes.get(id);
  if (!node) return [];
  const out: Criterion[] = [];
  const type = typeOf(model, id);
  if (type) out.push({ key: 'type', label: 'Same type', value: type.name });
  const layers = layersFor(model, id);
  if (layers.length) {
    out.push({ key: 'width', label: 'Same width', value: mmOf(layers.reduce((s, l) => s + (l.thickness || 0), 0)) });
    const core = layers.find((l) => l.core);
    const coreName = core ? model.nodes.get(core.material)?.name : undefined;
    if (coreName) out.push({ key: 'coreMaterial', label: 'Same core material', value: coreName });
  } else {
    const mat = materialOf(model, id);
    if (mat && mat.type === 'IfcMaterial') out.push({ key: 'material', label: 'Same material', value: mat.name });
  }
  const storey = node.storeyId ? model.nodes.get(node.storeyId) : undefined;
  if (storey) out.push({ key: 'storey', label: 'Same storey', value: storey.name });
  return out;
}

/** The text an element answers a criterion with (null: it has no such thing, so it never matches). */
export function valueOf(model: ModelState, id: string, key: SimilarKey): string | null {
  return criteriaOf(model, id).find((c) => c.key === key)?.value ?? null;
}

/** The ids of the nodes of the class of any of `ids` that answer every key as that element does; the selection itself is included. `inScope` limits the candidates (what a view shows). */
export function similarIds(model: ModelState, ids: string[], keys: SimilarKey[], inScope: (n: NodeDto) => boolean = () => true): string[] {
  const refs = ids.map((id) => model.nodes.get(id)).filter((n): n is NodeDto => !!n);
  if (!refs.length) return [];
  // A reference's signature: its class and its answers; a candidate matches when its own equals one of them.
  const sig = (n: NodeDto): string | null => {
    const parts: string[] = [n.type];
    for (const k of keys) {
      const v = valueOf(model, n.id, k);
      if (v == null) return null;
      parts.push(v);
    }
    return parts.join('\u0000');
  };
  const want = new Set(refs.map(sig).filter((s): s is string => s != null));
  const classes = new Set(refs.map((n) => n.type));
  const out: string[] = [];
  for (const n of model.nodes.values()) {
    if (!classes.has(n.type) || !n.editable && !refs.includes(n)) continue;
    if (!refs.includes(n) && !inScope(n)) continue;
    const s = sig(n);
    if (s != null && want.has(s)) out.push(n.id);
  }
  return out;
}

/** The keys "Select similar" matches by: the type when the selection has one (each of them), else the class alone. */
export function defaultKeys(model: ModelState, ids: string[]): SimilarKey[] {
  return ids.every((id) => !!typeOf(model, id)) ? ['type'] : [];
}
