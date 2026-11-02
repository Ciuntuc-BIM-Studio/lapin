/**
 * The type a configurator opens on: the selected element's (what the user
 * is looking at), else the one chosen last (if the project still has it),
 * else the first of its kind. A configurator that opened on another type
 * than the selected scaffold's made its changes seem to go nowhere.
 */

import type { NodeDto } from '../../engine/protocol';
import { typeOf, type ModelState } from '../../engine/model';

export function openingType(model: ModelState, selected: readonly string[], elementClass: string, chosen: string | null, types: readonly NodeDto[]): string | null {
  const own = selectedType(model, selected, elementClass, types);
  if (own) return own;
  if (chosen && types.some((x) => x.id === chosen)) return chosen;
  return types[0]?.id ?? null;
}

/** The type of the first selected element of that class (among `types` when given); null when none is selected. */
export function selectedType(model: ModelState, selected: readonly string[], elementClass: string, types?: readonly NodeDto[]): string | null {
  for (const id of selected) {
    const e = model.nodes.get(id);
    if (e?.type !== elementClass) continue;
    const t = typeOf(model, id);
    if (t && (!types || types.some((x) => x.id === t.id))) return t.id;
  }
  return null;
}

/**
 * A type remembered for the tools, when the project still has it as that
 * class (and, for formwork, of that kind: a type made before kinds is a
 * floor's); undefined when it went or is of another kind.
 */
export function rememberedType(nodes: ReadonlyMap<string, NodeDto>, id: string | null, cls: string, kind?: string): string | undefined {
  const t = id ? nodes.get(id) : undefined;
  if (!t || t.type !== cls) return undefined;
  return kind == null || (t.props.kind ?? 'slab') === kind ? t.id : undefined;
}
