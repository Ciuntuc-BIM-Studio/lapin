/**
 * The splats' sort, away from the page's thread (splatSort.ts): a cloud's centres sent once, page by page as they come,
 * then a depth row a sort; the order comes back as a transferred array.
 */

import { sortBackToFront, type DepthRow } from './splatSort';

type In =
  | { kind: 'positions'; id: string; at: number; positions: Float32Array; capacity: number }
  | { kind: 'sort'; id: string; row: DepthRow; n: number; seq: number }
  | { kind: 'drop'; id: string };

const clouds = new Map<string, { positions: Float32Array; scratch: { depth?: Int32Array; counts?: Uint32Array } }>();

self.onmessage = (e: MessageEvent<In>) => {
  const m = e.data;
  if (m.kind === 'drop') { clouds.delete(m.id); return; }
  if (m.kind === 'positions') {
    let c = clouds.get(m.id);
    if (!c || c.positions.length < m.capacity * 3) {
      const positions = new Float32Array(m.capacity * 3);
      if (c) positions.set(c.positions.subarray(0, Math.min(c.positions.length, positions.length)));
      c = { positions, scratch: c?.scratch ?? {} };
      clouds.set(m.id, c);
    }
    c.positions.set(m.positions, m.at * 3);
    return;
  }
  const c = clouds.get(m.id);
  if (!c) return;
  const n = Math.min(m.n, c.positions.length / 3);
  const order = sortBackToFront(c.positions, n, m.row, new Uint32Array(n), c.scratch);
  (self as unknown as Worker).postMessage({ id: m.id, seq: m.seq, n, order }, [order.buffer]);
};
