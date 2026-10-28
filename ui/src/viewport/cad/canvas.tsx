/** The overlay a 2D view draws for drafting: the snap, the guides, the running command's preview and a move's trace (the aim itself is in aim.ts). */

import type { Vec2 } from '../../platform/module';
import type { SnapMode } from './snaps';
import { polylines, type Shape } from './shapes';
import { movedShape, useCad } from './session';
import type { Aimed } from './aim';
import { livePoint, useLive } from '../liveInput';
import { traceClass, traceState, useTrace } from '../moveTrace';

type ToScreen = (p: Vec2) => { x: number; y: number };
const pointsOf = (s: Shape, toScreen: ToScreen) => polylines(s).map((pl) => pl.map((p) => { const q = toScreen(p); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; }).join(' '));

/**
 * A move's trace (decision 165): the ghost of what is moved where it was, the
 * dashed line from the base point to the moved point, the base marker; the
 * line and the marker in the aim's colour.
 */
function MoveTrace({ aimed, to, toScreen }: { aimed: Aimed; to: Vec2; toScreen: ToScreen }) {
  const trace = useTrace((s) => s.trace);
  const ortho = useCad((s) => s.ortho);
  if (!trace) return null;
  const { base } = trace;
  const square = Math.abs(to.x - base.x) < 1e-9 !== Math.abs(to.y - base.y) < 1e-9;
  const cls = traceClass(traceState(aimed, ortho || square));
  const a = toScreen(base), b = toScreen(to);
  const d = { x: to.x - base.x, y: to.y - base.y };
  return (
    <g className={`move-trace ${cls}`} aria-label="Move trace">
      {trace.ghost.flatMap((s, i) => pointsOf(s, toScreen).map((pts, j) => <polyline key={`g${i}:${j}`} className="move-ghost" points={pts} />))}
      {trace.carry && trace.ghost.flatMap((s, i) => pointsOf(movedShape(s, d), toScreen).map((pts, j) => <polyline key={`m${i}:${j}`} className="cad-preview" points={pts} />))}
      <line className="move-vector" x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
      <g className="move-base">
        <circle cx={a.x} cy={a.y} r={5} />
        <path d={`M${a.x - 9},${a.y}H${a.x + 9}M${a.x},${a.y - 9}V${a.y + 9}`} />
      </g>
      <circle className="move-end" cx={b.x} cy={b.y} r={2.5} />
    </g>
  );
}

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
export function CadOverlay({ aimed, toScreen }: { aimed: Aimed | null; toScreen: ToScreen }) {
  const step = useCad((s) => s.step);
  const live = useLive();
  if (!aimed) return null;
  const c = toScreen(aimed.point);
  const typed = live.owner && (live.len || live.ang) ? livePoint({ ...live, cursor: aimed.point }) : null;
  const to = typed ?? aimed.point;
  const preview = step?.preview ? step.preview(to) : [];
  return (
    <g className="cad-overlay">
      <MoveTrace aimed={aimed} to={to} toScreen={toScreen} />
      {aimed.guides.map((g, i) => {
        const a = toScreen(g.from), b = toScreen(g.to);
        const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
        const ex = { x: b.x + ((b.x - a.x) / len) * 40, y: b.y + ((b.y - a.y) / len) * 40 };
        return <g key={i}><line className="cad-guide" x1={a.x} y1={a.y} x2={ex.x} y2={ex.y} /><path className="cad-acquired" d={`M${a.x - 3},${a.y}H${a.x + 3}M${a.x},${a.y - 3}V${a.y + 3}`} /></g>;
      })}
      {preview.flatMap((s, i) => pointsOf(s, toScreen).map((pts, j) => <polyline key={`${i}:${j}`} className="cad-preview" points={pts} />))}
      {aimed.mode && <g className="cad-snap">{GLYPH[aimed.mode](c.x, c.y)}<text x={c.x + 9} y={c.y - 9}>{aimed.mode}</text></g>}
    </g>
  );
}
