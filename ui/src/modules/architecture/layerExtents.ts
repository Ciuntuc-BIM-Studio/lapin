/**
 * A wall's or floor's layers reaching past its edges, or stopping short of them (forum topic 89, docs/plan-layer-extents.md):
 * the element's own `layerExtents` (the engine's LayerExtents), each a layer (its index in the type's set), an edge (a wall's
 * start, end, bottom or top; a floor's outline all round, or one edge of it by `index`), an offset in metres (positive past
 * the edge) and whether the piece past the edge cuts the elements it meets (`cuts`: true, or "order": only those its element
 * outranks by the cut order). A wall type or a floor type holds the extents its elements start with; an element's own at the
 * same place wins (an own 0 keeps the layer at that edge). Set one at a time (architecture.setLayerExtent, one undo step; an
 * offset of 0 takes it away, `inherit` gives the place back to the type's).
 */
import type { Layer } from '../../engine/model';

export interface LayerExtent { layer: number; edge: string; index?: number; offset: number; cuts?: boolean | 'order' }

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
    return [{ layer: o.layer, edge: o.edge, offset: o.offset, ...(typeof o.index === 'number' ? { index: o.index } : {}), ...(o.cuts === true || o.cuts === 'order' ? { cuts: o.cuts } : {}) }];
  });
}

export const sameKey = (a: LayerExtent, b: LayerExtent) => a.layer === b.layer && a.edge === b.edge && (a.index ?? null) === (b.index ?? null);

/** The command arguments that set one extent for an element. */
export const setArgs = (id: string, x: LayerExtent) => ({ id, layer: x.layer, edge: x.edge, offset: x.offset, cuts: x.cuts ?? false, ...(x.edge === EDGE ? { index: x.index ?? 0 } : {}) });

/** The command arguments that take an element's own extent away: back to its type's where the type has one there, else gone. */
export const removeArgs = (id: string, x: LayerExtent, typeHas: boolean) => typeHas
  ? { id, layer: x.layer, edge: x.edge, inherit: true, ...(x.edge === EDGE ? { index: x.index ?? 0 } : {}) }
  : setArgs(id, { ...x, offset: 0 });

/** A row of the section: an extent the element has, its own or its type's (`fromType`), and the type's at its place when its own replaces it. */
export interface ExtentRow { x: LayerExtent; fromType: boolean; typeOffset?: number }

/** The rows an element shows: its type's (those it does not set itself), then its own. */
export function extentRows(own: LayerExtent[], fromType: LayerExtent[]): ExtentRow[] {
  return [
    ...fromType.filter((t) => !own.some((o) => sameKey(o, t))).map((x) => ({ x, fromType: true })),
    ...own.map((x) => { const t = fromType.find((y) => sameKey(x, y)); return { x, fromType: false, ...(t ? { typeOffset: t.offset } : {}) }; }),
  ];
}

/**
 * The steps that turn an extent into another: one when only its offset or cut changes; when its layer or edge changes, the old
 * one taken away and the new one set (run as one edit). None when the new one's place is another extent's already.
 */
export function changeSteps(id: string, all: LayerExtent[], was: LayerExtent, next: LayerExtent, typeHas: (x: LayerExtent) => boolean = () => false): { command: string; args: Record<string, unknown> }[] {
  if (sameKey(was, next)) return [{ command: 'architecture.setLayerExtent', args: setArgs(id, next) }];
  if (all.some((x) => sameKey(x, next))) return [];
  return [
    { command: 'architecture.setLayerExtent', args: removeArgs(id, was, typeHas(was)) },
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
