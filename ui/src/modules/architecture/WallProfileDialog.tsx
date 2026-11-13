/**
 * A wall by its profile (a tester's wish, #100, decision 372): its top drawn in elevation, seen from its start to its end.
 * A curtain wall's boundary the same way (#127): its grid, mullions, transoms and panels follow what is drawn here.
 * The points are dragged (10 mm steps; Shift keeps the height), a click on the top line adds one, a right-click takes one
 * out; the table holds them in millimetres. Shapes to start from: a gable, a slope, steps. The wall stays a wall: its
 * thickness, layers, joins, doors and windows as they were.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { wallLength } from '../../engine/model';

type Pt = [number, number];

/** Shapes a profile starts from, for a wall <paramref name="l"/> long and <paramref name="h"/> high (metres). */
export function profileShape(kind: 'gable' | 'slope' | 'steps' | 'monopitch', l: number, h: number, peak: number, steps = 3): Pt[] {
  if (kind === 'gable') return [[0, h], [l / 2, peak], [l, h]];
  if (kind === 'slope' || kind === 'monopitch') return [[0, h], [l, peak]];
  const o: Pt[] = [];
  for (let i = 0; i < steps; i++) {
    const v = h + ((peak - h) * i) / Math.max(1, steps - 1);
    const a = (l * i) / steps, b = (l * (i + 1)) / steps;
    o.push([a, v], [b, v]);
  }
  return o;
}

/** Points kept in order along the wall, inside it, 50 mm over its base at least, to the millimetre. */
export function tidyProfile(pts: Pt[], l: number): Pt[] {
  return pts.map(([u, v]): Pt => [Math.round(Math.min(Math.max(u, 0), l) * 1000) / 1000, Math.round(Math.max(v, 0.05) * 1000) / 1000])
    .sort((a, b) => a[0] - b[0]);
}

export function WallProfileDialog() {
  const id = useUi((s) => s.selection);
  const wall = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const close = () => useUi.setState({ dialog: null });
  const l = wall ? wallLength(wall) : 0;
  // Its height as the engine works it out (to its top, the forum's topic #45), its own until that comes.
  const [h, setH] = useState(() => Number(wall?.props.height ?? 3));
  const stored = (Array.isArray(wall?.props.topProfile) ? wall!.props.topProfile : null) as Pt[] | null;
  const [pts, setPts] = useState<Pt[]>(() => stored ?? [[0, h], [l, h]]);
  const [peak, setPeak] = useState(() => String(Math.round((h + 1.5) * 1000)));
  const [drag, setDrag] = useState<number | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  useEffect(() => {
    let live = true;
    const own = Number(wall?.props.height ?? 3);
    setH(own);
    setPts(stored ?? [[0, own], [l, own]]);
    if (id) void query<{ height: number }>('architecture.wallTop', { id }).then((r) => {
      if (!live || !r) return;
      setH(r.height);
      if (!stored) setPts([[0, r.height], [l, r.height]]);
    });
    return () => { live = false; };
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  const W = 560, H = 300, pad = 30;
  const top = Math.max(h, ...pts.map((p) => p[1])) * 1.1 + 0.2;
  const k = Math.min((W - 2 * pad) / Math.max(l, 0.1), (H - 2 * pad) / top);
  const sx = (u: number) => pad + u * k, sy = (v: number) => H - pad - v * k;
  const toModel = (e: { clientX: number; clientY: number }): Pt => {
    const r = svg.current!.getBoundingClientRect();
    const x = ((e.clientX - r.left) / r.width) * W, y = ((e.clientY - r.top) / r.height) * H;
    return [Math.round(((x - pad) / k) * 100) / 100, Math.round(((H - pad - y) / k) * 100) / 100];
  };
  const outline = useMemo(() => {
    const ps = [[0, 0] as Pt, [0, pts[0]?.[1] ?? h] as Pt, ...pts, [l, pts[pts.length - 1]?.[1] ?? h] as Pt, [l, 0] as Pt];
    return ps.map(([u, v]) => `${sx(u).toFixed(1)},${sy(v).toFixed(1)}`).join(' ');
  }, [pts, l, h, k]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!wall || (wall.type !== 'IfcWall' && wall.type !== 'IfcCurtainWall')) return null;
  const curtain = wall.type === 'IfcCurtainWall';
  const mm = (v: number) => Math.round(v * 1000);
  const setPoint = (i: number, p: Pt) => setPts((cur) => cur.map((q, j) => (j === i ? p : q)));
  const apply = async (next: Pt[] | null) => {
    const top = next ? tidyProfile(next, l) : null;
    const r = curtain ? await execute('architecture.setCurtainWallProfile', { ids: [wall.id], top }) : await execute('architecture.setWallProfile', { id: wall.id, top });
    if (r !== undefined) notify(next ? (curtain ? 'The curtain wall follows its boundary.' : 'The wall follows its profile.') : curtain ? 'The curtain wall is flat on top again.' : 'The wall is flat again.');
  };
  const shape = (kind: 'gable' | 'slope' | 'steps') => setPts(profileShape(kind, l, h, Number(peak) / 1000 || h + 1));
  // A click on the top line between two points puts a point there.
  const addAt = (e: React.MouseEvent) => {
    const [u, v] = toModel(e);
    const i = pts.findIndex((p) => p[0] > u);
    if (i <= 0) return;
    setPts([...pts.slice(0, i), [u, v], ...pts.slice(i)]);
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label={curtain ? 'Curtain wall boundary' : 'Wall profile'} data-testid="wall-profile" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">{curtain ? 'Curtain wall boundary' : 'Wall profile'} · <span data-no-i18n>{wall.name}</span></span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Seen from its start to its end. Drag a point (Shift keeps its height); click the top line to add one, right-click a point to take it out. A step is two points one over the other.{curtain && ' The grid, the mullions, the transoms and the panels follow the boundary; the type\'s settings stay.'}</p>
          <svg ref={svg} viewBox={`0 0 ${W} ${H}`} width={W} height={H} className="wall-profile-svg"
            onPointerMove={(e) => { if (drag == null) return; const [u, v] = toModel(e); setPoint(drag, [Math.min(Math.max(u, 0), l), e.shiftKey ? pts[drag][1] : Math.max(v, 0.05)]); }}
            onPointerUp={() => setDrag(null)} onPointerLeave={() => setDrag(null)}>
            <line x1={sx(0) - 10} y1={sy(0)} x2={sx(l) + 10} y2={sy(0)} stroke="var(--ink-muted)" strokeDasharray="4 3" />
            <polygon points={outline} fill="var(--wash)" stroke="var(--ink)" strokeWidth={1.2} />
            <line x1={sx(0)} y1={sy(h)} x2={sx(l)} y2={sy(h)} stroke="var(--rule-strong)" strokeDasharray="2 3" />
            <polyline points={pts.map(([u, v]) => `${sx(u)},${sy(v)}`).join(' ')} fill="none" stroke="transparent" strokeWidth={12} style={{ cursor: 'copy' }} onClick={addAt} />
            {pts.map(([u, v], i) => (
              <g key={i}>
                <circle cx={sx(u)} cy={sy(v)} r={6} fill="var(--accent)" stroke="#fff" strokeWidth={1.5} style={{ cursor: 'grab' }} data-profile-point={i}
                  onPointerDown={(e) => { (e.target as Element).setPointerCapture?.(e.pointerId); setDrag(i); }}
                  onContextMenu={(e) => { e.preventDefault(); if (pts.length > 2) setPts(pts.filter((_, j) => j !== i)); }} />
                <text x={sx(u) + 8} y={sy(v) - 8} fontSize={10} fill="var(--ink-muted)" data-no-i18n>{mm(v)}</text>
              </g>
            ))}
            <text x={sx(0)} y={sy(0) + 16} fontSize={10} fill="var(--ink-muted)">Start</text>
            <text x={sx(l) - 22} y={sy(0) + 16} fontSize={10} fill="var(--ink-muted)">End</text>
          </svg>
          <div className="row wrap" style={{ gap: 6, margin: '8px 0' }} data-i18n-ctx="wallProfile">
            <span className="eyebrow">Start from</span>
            <button type="button" className="btn small" data-action="profile-gable" onClick={() => shape('gable')}>Gable</button>
            <button type="button" className="btn small" data-action="profile-slope" onClick={() => shape('slope')}>Slope</button>
            <button type="button" className="btn small" data-action="profile-steps" onClick={() => shape('steps')}>Steps</button>
            <label className="small">to <input className="num mini" style={{ width: 70 }} data-field="profile-peak" value={peak} onChange={(e) => setPeak(e.target.value)} /> mm</label>
          </div>
          <table className="grid small">
            <thead><tr><th>#</th><th>Along (mm)</th><th>Height (mm)</th></tr></thead>
            <tbody>
              {pts.map(([u, v], i) => (
                <tr key={`${i}:${u}:${v}`}>
                  <td data-no-i18n>{i + 1}</td>
                  <td><input className="num mini" defaultValue={mm(u)} onBlur={(e) => setPoint(i, [Number(e.target.value) / 1000 || 0, v])} /></td>
                  <td><input className="num mini" defaultValue={mm(v)} onBlur={(e) => setPoint(i, [u, Number(e.target.value) / 1000 || v])} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" data-action="profile-flat" onClick={() => void apply(null).then(close)}>Flat top</button>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-action="profile-apply" onClick={() => void apply(pts).then(close)}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
