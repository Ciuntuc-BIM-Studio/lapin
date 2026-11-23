/**
 * The profile editor in the plan (site grading T3): the line's station hovered in the graph is marked on it, the station picked
 * too, and each cross section is drawn across it; the line itself answers the pointer while the panel is open, so hovering it
 * shows its station in the graph and a click picks that station there.
 */

import type { PlanOverlayProps } from '../../platform/module';
import { useProfile } from './profileStore';
import { pointAtStation, stationAtPoint, stationName } from './profile';

export function ProfilePlanOverlay({ toScreen, toWorld }: PlanOverlayProps) {
  const d = useProfile((s) => s.data);
  const hover = useProfile((s) => s.hover);
  const station = useProfile((s) => s.station);
  const cross = useProfile((s) => s.cross);
  if (!d || d.path.length < 2) return null;
  const pts = d.path.map((p) => toScreen({ x: p[0], y: p[1] }));
  const poly = pts.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  const near = (e: React.PointerEvent | React.MouseEvent) => {
    const svg = (e.target as SVGElement).ownerSVGElement;
    if (!svg) return null;
    const r = svg.getBoundingClientRect();
    const w = toWorld(e.clientX - r.left, e.clientY - r.top);
    return stationAtPoint(d.path, w.x, w.y);
  };
  const mark = (s: number | null, on: boolean) => {
    if (s == null) return null;
    const m = pointAtStation(d.path, s);
    if (!m) return null;
    const p = toScreen({ x: m.x, y: m.y });
    return (
      <g key={on ? 'on' : 'hover'} className={`profile-plan-mark${on ? ' on' : ''}`} data-testid={on ? 'profile-plan-picked' : 'profile-plan-hover'}>
        <circle cx={p.x} cy={p.y} r={on ? 5 : 7} />
        <text x={p.x + 10} y={p.y - 8} data-no-i18n>{stationName(s)}</text>
      </g>
    );
  };
  return (
    <g className="profile-plan" data-testid="profile-plan">
      {cross.map((c) => {
        const [a, b] = [toScreen({ x: c.from[0], y: c.from[1] }), toScreen({ x: c.to[0], y: c.to[1] })];
        return <g key={c.station} className="profile-plan-cross"><line x1={a.x} y1={a.y} x2={b.x} y2={b.y} /><text x={b.x + 4} y={b.y - 4} data-no-i18n>{c.label}</text></g>;
      })}
      <polyline points={poly} className="profile-plan-hit" data-overlay-hit fill="none" stroke="transparent" strokeWidth={16} style={{ pointerEvents: 'stroke', cursor: 'crosshair' }}
        onPointerMove={(e) => { const n = near(e); if (n) useProfile.setState({ hover: n.station }); }}
        onPointerLeave={() => useProfile.setState({ hover: null })}
        onClick={(e) => { const n = near(e); if (n) useProfile.setState({ station: n.station, point: null, segment: null }); }} />
      {mark(station, true)}
      {mark(hover, false)}
    </g>
  );
}
