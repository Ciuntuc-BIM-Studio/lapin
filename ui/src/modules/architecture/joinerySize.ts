/**
 * A door's or window's own size (forum topic 140, decision 664): its `width` and `height` over its type's. The type is the
 * construction and the default size; an element without them is its type's size. Read here as the engine reads it
 * (Openings.SpecOf), and set with architecture.setJoinerySize.
 */

type Props = Record<string, unknown>;

export interface JoinerySize {
  width: number;
  height: number;
  /** Either dimension is the element's own. */
  own: boolean;
}

const n = (v: unknown): number | null => (typeof v === 'number' && Number.isFinite(v) ? v : null);

/** The element's size: its own width and height where it has them, else its type's spec's. */
export function joinerySize(props: Props, spec: { width?: number; height?: number } | undefined | null): JoinerySize {
  const w = n(props.width);
  const h = n(props.height);
  return { width: w ?? spec?.width ?? 0, height: h ?? spec?.height ?? 0, own: w != null || h != null };
}

/** architecture.setJoinerySize's arguments: a width or a height for the elements, or back to their type's. */
export function sizeArgs(ids: string[], change: { width?: number; height?: number } | 'reset'): Record<string, unknown> {
  return change === 'reset' ? { ids, reset: true } : { ids, ...change };
}
