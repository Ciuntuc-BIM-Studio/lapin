/** The overlay a 2D view draws for drafting: the snap, the guides and the running command's preview (the aim itself is in aim.ts). */

import type { Vec2 } from '../../platform/module';
import type { SnapMode } from './snaps';
import { polylines } from './shapes';
import { useCad } from './session';
import type { Aimed } from './aim';

const GLYPH: Record<SnapMode, (x: number, y: number) => JSX.Element> = {
  endpoint: (x, y) => <rect x={x - 5} y={y - 5} width={10} height={10} />,
  midpoint: (x, y) => <path d={`M${x},${y - 6}L${x + 6},${y + 5}L${x - 6},${y + 5}Z`} />,
  intersection: (x, y) => <path d={`M${x - 5},${y - 5}L${x + 5},${y + 5}M${x + 5},${y - 5}L${x - 5},${y + 5}`} />,
  center: (x, y) => <circle cx={x} cy={y} r={5} />,
  quadrant: (x, y) => <path d={`M${x},${y - 6}L${x + 6},${y}L${x},${y + 6}L${x - 6},${y}Z`} />,
  perpendicular: (x, y) => <path d={`M${x - 5},${y + 5}H${x + 5}M${x - 3},${y + 5}V${y - 5}`} />,
  nearest: (x, y) => <path d={`M${x - 5},${y - 5}H${x + 5}L${x - 5},${y + 5}H${x + 5}Z`} />,
};

/** The snap marker, the guides and the running command's preview, in screen coordinates. */
export function CadOverlay({ aimed, toScreen }: { aimed: Aimed | null; toScreen: (p: Vec2) => { x: number; y: number } }) {
  const step = useCad((s) => s.step);
  if (!aimed) return null;
  const c = toScreen(aimed.point);
  const preview = step?.preview ? step.preview(aimed.point) : [];
  return (
    <g className="cad-overlay">
      {aimed.guides.map((g, i) => {
        const a = toScreen(g.from), b = toScreen(g.to);
        const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
        const ex = { x: b.x + ((b.x - a.x) / len) * 40, y: b.y + ((b.y - a.y) / len) * 40 };
        return <g key={i}><line className="cad-guide" x1={a.x} y1={a.y} x2={ex.x} y2={ex.y} /><path className="cad-acquired" d={`M${a.x - 3},${a.y}H${a.x + 3}M${a.x},${a.y - 3}V${a.y + 3}`} /></g>;
      })}
      {preview.flatMap((s, i) => polylines(s).map((pl, j) => (
        <polyline key={`${i}:${j}`} className="cad-preview" points={pl.map((p) => { const q = toScreen(p); return `${q.x},${q.y}`; }).join(' ')} />
      )))}
      {aimed.mode && <g className="cad-snap">{GLYPH[aimed.mode](c.x, c.y)}<text x={c.x + 9} y={c.y - 9}>{aimed.mode}</text></g>}
    </g>
  );
}
