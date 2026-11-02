/** A roof's edges as the configurator and the inspector edit them: one entry per side of its outline, null for "as the roof". */

export type Edge = { kind?: 'eave' | 'gable'; pitch?: number; overhang?: number; gutter?: boolean } | null;

/**
 * The edges with side `i` changed by `patch` (null: back to the roof's own):
 * one entry for each of `count` sides; a field set to undefined goes, and an
 * edge left with no field of its own becomes null.
 */
export function mergeEdge(edges: readonly Edge[], count: number, i: number, patch: Partial<NonNullable<Edge>> | null): Edge[] {
  const next: Edge[] = Array.from({ length: count }, (_, k) => edges[k] ?? null);
  const merged: Record<string, unknown> | null = patch === null ? null : { ...(next[i] ?? {}), ...patch };
  if (merged) for (const key of Object.keys(merged)) if (merged[key] === undefined) delete merged[key];
  next[i] = merged && Object.keys(merged).length ? (merged as NonNullable<Edge>) : null;
  return next;
}
