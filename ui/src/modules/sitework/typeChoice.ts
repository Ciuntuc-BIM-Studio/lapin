/**
 * The type a configurator opens on: the selected element's (what the user
 * is looking at), else the one chosen last (if the project still has it),
 * else the first of its kind. A configurator that opened on another type
 * than the selected scaffold's made its changes seem to go nowhere.
 */

import type { NodeDto } from '../../engine/protocol';
import { typeOf, type ModelState } from '../../engine/model';

export function openingType(model: ModelState, selected: readonly string[], elementClass: string, chosen: string | null, types: readonly NodeDto[]): string | null {
  for (const id of selected) {
    const e = model.nodes.get(id);
    if (e?.type !== elementClass) continue;
    const t = typeOf(model, id);
    if (t && types.some((x) => x.id === t.id)) return t.id;
  }
  if (chosen && types.some((x) => x.id === chosen)) return chosen;
  return types[0]?.id ?? null;
}
