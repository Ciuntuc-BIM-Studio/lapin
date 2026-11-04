/**
 * A wall layer's priority where walls join (the testers' report #81): its own, else its material's priority in the
 * project's cut order (Manage → Cut order), else 50 for the core and 0 for the rest — the engine's Layers.Rank.
 */
import { ofClass, type ModelState } from '../../engine/model';

export const CORE_RANK = 50;
export const FINISH_RANK = 0;

/** The priority a layer of this material takes when it gives none: what the Priority column shows greyed. */
export function layerRankHint(model: ModelState, material: string, core: boolean): number {
  const name = material.trim().toLowerCase();
  const mat = ofClass(model, 'IfcMaterial').find((m) => m.name.trim().toLowerCase() === name);
  const table = ofClass(model, 'LapinCutOrder')[0]?.props.materials as Record<string, unknown> | undefined;
  const own = mat && table ? table[mat.id] : undefined;
  return typeof own === 'number' ? own : core ? CORE_RANK : FINISH_RANK;
}
