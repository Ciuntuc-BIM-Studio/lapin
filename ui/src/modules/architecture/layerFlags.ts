/**
 * What a type's layer says of itself besides its material and thickness (the forum's topic #30): its category in this
 * build-up (IfcMaterialLayer.Category, over its material's; a view style hides the finishes by it), and, for a wall's
 * layer, whether it goes on into the floors' layers ranked under it, down to the one it stands on and up to the one over
 * it (the engine's Walls.Extensions).
 */
import type { Layer } from '../../engine/model';

/** The categories a layer can take (the engine's Materials.Categories); none: its material's. */
export const LAYER_CATEGORIES = ['Structure', 'Finish', 'Insulation', 'Membrane', 'Other'] as const;

export interface LayerFlags { category: string | null; down: boolean; up: boolean }

/** A stored layer's flags. */
export const flagsOf = (l: Layer): LayerFlags => ({
  category: typeof l.category === 'string' && l.category ? l.category : null,
  down: l.extendDown === true,
  up: l.extendUp === true,
});

/** The flags as a command sends them: only what is set, the extensions only for a wall's layer. */
export function flagsJson(f: LayerFlags, wall: boolean): { category?: string; extendDown?: true; extendUp?: true } {
  return {
    ...(f.category ? { category: f.category } : {}),
    ...(wall && f.down ? { extendDown: true as const } : {}),
    ...(wall && f.up ? { extendUp: true as const } : {}),
  };
}

/** A new wall type's flags for a layer: its core goes down onto the floor's structure, the rest stays on the floor's top. */
export const newWallFlags = (core: boolean): LayerFlags => ({ category: null, down: core, up: false });
