/**
 * The splats' order for blending (docs/plan-gaussian-splats.md, G1): back to front along the view, by a counting sort of
 * their depths quantized to 16 bits (two passes over the splats, no comparison), so that a few million are sorted in a
 * worker in tens of milliseconds. Pure, shared by the worker (splatSort.worker.ts) and the tests.
 */

/** The view's depth row: a point's view-space z is r[0]·x + r[1]·y + r[2]·z + r[3] (three's camera looks down −z). */
export type DepthRow = readonly [number, number, number, number];

/** The depth row of a view matrix (three's column-major `elements`). */
export function depthRow(view: ArrayLike<number>): DepthRow {
  return [view[2], view[6], view[10], view[14]];
}

/**
 * The first `n` splats' indices back to front (the farthest first: the most negative view z), into `out` (reused across
 * sorts; at least `n` long). Splats at the same quantized depth keep their order. Returns `out`.
 */
export function sortBackToFront(positions: Float32Array, n: number, row: DepthRow, out: Uint32Array, scratch?: { depth?: Int32Array; counts?: Uint32Array }): Uint32Array {
  const depth = scratch?.depth && scratch.depth.length >= n ? scratch.depth : new Int32Array(n);
  const counts = scratch?.counts ?? new Uint32Array(65536);
  counts.fill(0);
  const [a, b, c, d] = row;
  let lo = Infinity, hi = -Infinity;
  // Depths as fixed point (1/1024 m), the range found as they are worked out.
  for (let i = 0; i < n; i++) {
    const z = (a * positions[3 * i] + b * positions[3 * i + 1] + c * positions[3 * i + 2] + d) * 1024 | 0;
    depth[i] = z;
    if (z < lo) lo = z;
    if (z > hi) hi = z;
  }
  if (n === 0) return out;
  const k = hi > lo ? 65535 / (hi - lo) : 0;
  for (let i = 0; i < n; i++) {
    const q = ((depth[i] - lo) * k) | 0;
    depth[i] = q;
    counts[q]++;
  }
  // Ascending view z: the most negative (the farthest) first.
  let at = 0;
  for (let q = 0; q < 65536; q++) { const m = counts[q]; counts[q] = at; at += m; }
  for (let i = 0; i < n; i++) out[counts[depth[i]]++] = i;
  if (scratch) { scratch.depth = depth; scratch.counts = counts; }
  return out;
}
