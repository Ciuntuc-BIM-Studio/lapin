/**
 * A wall's or floor's layers reaching past its edges, or stopping short of them (forum topic 89, docs/plan-layer-extents.md):
 * the element's own `layerExtents` (the engine's LayerExtents), each a layer (its index in the type's set), an edge (a wall's
 * start, end, bottom or top; a floor's outline all round, or one edge of it by `index`), an offset in metres (positive past
 * the edge) and whether the piece past the edge cuts the walls and floors it meets. Set one at a time
 * (architecture.setLayerExtent, one undo step; an offset of 0 takes it away).
 */
import type { Layer } from '../../engine/model';

export interface LayerExtent { layer: number; edge: string; index?: number; offset: number; cuts?: boolean }

export const WALL_EDGES = ['start', 'end', 'bottom', 'top'] as const;
export const OUTLINE = 'outline';
export const EDGE = 'edge';

/** The extents an element's props hold; none for one without them. */
export function extentsOf(props: Record<string, unknown>): LayerExtent[] {
  const raw = props.layerExtents;
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((x) => {
    if (!x || typeof x !== 'object') return [];
    const o = x as Record<string, unknown>;
    if (typeof o.layer !== 'number' || typeof o.edge !== 'string' || typeof o.offset !== 'number') return [];
    return [{ layer: o.layer, edge: o.edge, offset: o.offset, ...(typeof o.index === 'number' ? { index: o.index } : {}), ...(o.cuts === true ? { cuts: true } : {}) }];
  });
}

const sameKey = (a: LayerExtent, b: LayerExtent) => a.layer === b.layer && a.edge === b.edge && (a.index ?? null) === (b.index ?? null);

/** The command arguments that set one extent for an element. */
export const setArgs = (id: string, x: LayerExtent) => ({ id, layer: x.layer, edge: x.edge, offset: x.offset, cuts: x.cuts === true, ...(x.edge === EDGE ? { index: x.index ?? 0 } : {}) });

/**
 * The steps that turn an extent into another: one when only its offset or cut changes; when its layer or edge changes, the old
 * one taken away and the new one set (run as one edit). None when the new one's place is another extent's already.
 */
export function changeSteps(id: string, all: LayerExtent[], was: LayerExtent, next: LayerExtent): { command: string; args: Record<string, unknown> }[] {
  if (sameKey(was, next)) return [{ command: 'architecture.setLayerExtent', args: setArgs(id, next) }];
  if (all.some((x) => sameKey(x, next))) return [];
  return [
    { command: 'architecture.setLayerExtent', args: setArgs(id, { ...was, offset: 0 }) },
    { command: 'architecture.setLayerExtent', args: setArgs(id, next) },
  ];
}

/** The extent a new row starts with: the first layer with a thickness and an edge it has none at, 2 cm past it; null when every place is taken. */
export function newExtent(wall: boolean, layers: Layer[], all: LayerExtent[]): LayerExtent | null {
  const edges = wall ? ['bottom', 'top', 'start', 'end'] : [OUTLINE];
  for (let i = 0; i < layers.length; i++) {
    if (!(layers[i].thickness > 0)) continue;
    for (const edge of edges) {
      const x = { layer: i, edge, offset: 0.02 };
      if (!all.some((y) => sameKey(y, x))) return x;
    }
  }
  return null;
}
