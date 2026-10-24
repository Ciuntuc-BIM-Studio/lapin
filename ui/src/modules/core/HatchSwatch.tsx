/** A material's look in one kind of view: its fill and hatch strokes over a square (the engine draws the strokes). */

import { useEffect, useState } from 'react';
import { query } from '../../engine/session';

type Preview = { solid: boolean; segments: number[] };
const cache = new Map<string, Preview>();

export function HatchSwatch({ hatch, scale = 1, fill, size = 44, membrane = false, revision = 0 }: { hatch: string | null; scale?: number; fill?: string | null; size?: number; membrane?: boolean; revision?: number }) {
  const key = `${hatch}|${scale}|${revision}`;
  const [p, setP] = useState<Preview | null>(cache.get(key) ?? null);
  useEffect(() => {
    if (!hatch) { setP(null); return; }
    const hit = cache.get(key);
    if (hit) { setP(hit); return; }
    let live = true;
    void query<Preview>('hatch.preview', { pattern: hatch, scale, size: 20 }).then((r) => { if (r) { cache.set(key, r); if (live) setP(r); } });
    return () => { live = false; };
  }, [key, hatch, scale]);
  const k = size / 20;
  const segs = p?.segments ?? [];
  return (
    <svg className="hatch-swatch" width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      <rect x={0.5} y={0.5} width={size - 1} height={size - 1} fill={fill ?? (p?.solid ? '#555' : 'var(--paper, #fff)')} stroke="var(--ink-muted)" />
      {membrane && <line x1={2} y1={size / 2} x2={size - 2} y2={size / 2} stroke="var(--ink)" strokeWidth={2} strokeDasharray="4 2" />}
      {segs.length > 0 && (
        <path d={Array.from({ length: segs.length / 4 }, (_, i) => {
          const [ax, ay, bx, by] = segs.slice(i * 4, i * 4 + 4).map((v) => v * k);
          return ax === bx && ay === by ? `M${ax} ${size - ay}h0.6` : `M${ax} ${size - ay}L${bx} ${size - by}`;
        }).join('')} stroke="var(--ink)" strokeWidth={0.6} fill="none" />
      )}
    </svg>
  );
}
