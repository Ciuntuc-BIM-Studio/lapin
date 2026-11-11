/**
 * The UCS's icon in a plan, as AutoCAD's: its X and Y axes from its origin, turned with it, a square at the
 * origin. Drawn only while a UCS is set (decision 445); the plan's own axes need none (the internal origin is
 * a location mark, Manage → Location).
 */

import type { Vec2 } from '../platform/module';
import { fromFrame, type Frame } from '../app/frame';

export function UcsIcon({ frame, toScreen, size = 44 }: { frame: Frame; toScreen: (p: Vec2) => Vec2; size?: number }) {
  const o = toScreen(frame.origin);
  // A screen direction from a frame axis: one metre along it, normalised to pixels.
  const dir = (q: Vec2) => {
    const p = toScreen(fromFrame(frame, q));
    const dx = p.x - o.x, dy = p.y - o.y, l = Math.hypot(dx, dy) || 1;
    return { x: dx / l, y: dy / l };
  };
  const x = dir({ x: 1, y: 0 }), y = dir({ x: 0, y: 1 });
  const tip = (d: Vec2) => ({ x: o.x + d.x * size, y: o.y + d.y * size });
  const head = (d: Vec2) => {
    const t = tip(d), n = { x: -d.y, y: d.x }, b = { x: t.x - d.x * 8, y: t.y - d.y * 8 };
    return `${t.x},${t.y} ${b.x + n.x * 4},${b.y + n.y * 4} ${b.x - n.x * 4},${b.y - n.y * 4}`;
  };
  const label = (d: Vec2) => ({ x: o.x + d.x * (size + 10), y: o.y + d.y * (size + 10) + 4 });
  const s = 6;
  return (
    <g className="ucs-icon" data-testid="ucs-icon" aria-label="UCS">
      <polygon className="ucs-origin" points={[[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([a, b]) => `${o.x + (x.x * a + y.x * b) * s / 2},${o.y + (x.y * a + y.y * b) * s / 2}`).join(' ')} />
      <line className="ucs-x" x1={o.x} y1={o.y} x2={tip(x).x} y2={tip(x).y} />
      <polygon className="ucs-x" points={head(x)} />
      <line className="ucs-y" x1={o.x} y1={o.y} x2={tip(y).x} y2={tip(y).y} />
      <polygon className="ucs-y" points={head(y)} />
      <text className="ucs-label" x={label(x).x} y={label(x).y} textAnchor="middle">X</text>
      <text className="ucs-label" x={label(y).x} y={label(y).y} textAnchor="middle">Y</text>
    </g>
  );
}
