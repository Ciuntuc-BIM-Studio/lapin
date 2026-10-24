/**
 * The key plan over a view: the active storey's footprint in miniature with
 * north up, and on it what a designer reads at a glance. In 3D: the camera
 * as the viewpoint symbol of a sheet (a dot with its cone of view), the
 * compass stops (seen from N, E, S, W), the four isometrics, the plan from
 * above, perspective or orthographic, "look from section A–A", and a strip
 * with the camera's height against the storeys. Drag the dot to move the
 * camera, drag or click elsewhere to look there, wheel to come closer,
 * double-click to frame everything. In a plan: the part of it on screen. In a
 * section or elevation: where it is cut from and where it looks.
 */

import { useCallback, useMemo, useRef } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../app/store';
import { activeStorey, useSession } from '../engine/session';
import { num, storeys } from '../engine/model';
import type { Vec2 } from '../platform/module';
import { STANDARD, lookOf, useCamera, type CameraPose } from './camera';

const W = 136, H = 104, STRIP = 150;

interface Props {
  mode: '3d' | 'plan' | 'drawing';
  /** A plan: the corners of what is on screen (world). */
  frame?: [Vec2, Vec2] | null;
  onLookAt?: (p: Vec2) => void;
  /** A section or elevation: the view shown. */
  viewId?: string | null;
}

/** The storey's footprint: one path of every plan line of its elements, and their bounds. */
function useFootprint(storeyId: string | null) {
  const render = useSession((s) => s.render);
  return useMemo(() => {
    let b: [number, number, number, number] | null = null;
    const polys: number[][] = [];
    for (const r of render.values()) {
      if (r.storeyId !== storeyId) continue;
      for (const it of r.plan) {
        if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid' || it.points.length < 4) continue;
        polys.push(it.points as number[]);
        for (let i = 0; i + 1 < it.points.length; i += 2) {
          const x = it.points[i], y = it.points[i + 1];
          b = b ? [Math.min(b[0], x), Math.min(b[1], y), Math.max(b[2], x), Math.max(b[3], y)] : [x, y, x, y];
        }
      }
    }
    return { polys, bounds: b };
  }, [render, storeyId]);
}

export function KeyPlan({ mode, frame, onLookAt, viewId }: Props) {
  const on = useUi((s) => s.keyPlan);
  useUi((s) => s.storeyId);
  const nodes = useSession((s) => s.nodes);
  const storey = activeStorey();
  const { polys, bounds } = useFootprint(storey?.id ?? null);
  const cam = useCamera();
  const svg = useRef<SVGSVGElement>(null);
  const drag = useRef<'move' | 'aim' | 'height' | null>(null);

  // The map's scale: the footprint padded, else a default square.
  const b = bounds ?? [-5, -5, 5, 5];
  const cx = (b[0] + b[2]) / 2, cy = (b[1] + b[3]) / 2;
  const k = Math.min(W / Math.max(b[2] - b[0], 2), H / Math.max(b[3] - b[1], 2)) * 0.72;
  const toMap = useCallback((p: { x: number; y: number }) => ({ x: (p.x - cx) * k + W / 2, y: H / 2 - (p.y - cy) * k }), [cx, cy, k]);
  const toWorld = useCallback((x: number, y: number): Vec2 => ({ x: (x - W / 2) / k + cx, y: (H / 2 - y) / k + cy }), [cx, cy, k]);
  const path = useMemo(() => {
    let d = '';
    for (const pts of polys) {
      for (let i = 0; i + 1 < pts.length; i += 2) { const q = toMap({ x: pts[i], y: pts[i + 1] }); d += `${i === 0 ? 'M' : 'L'}${q.x.toFixed(1)} ${q.y.toFixed(1)}`; }
    }
    return d;
  }, [polys, toMap]);

  // The storeys for the height strip, the sections for "look from".
  const levels = storeys(useSession.getState()).map((s) => ({ id: s.id, name: s.name, z: num(s, 'elevation') }));
  const zMin = Math.min(0, ...levels.map((l) => l.z)) - 1, zMax = Math.max(6, ...levels.map((l) => l.z)) + 5;
  const zToY = (z: number) => H - ((z - zMin) / (zMax - zMin)) * H;
  const yToZ = (y: number) => zMin + ((H - y) / H) * (zMax - zMin);
  const sections = [...nodes.values()].filter((n) => n.type === 'LapinView' && n.props.kind === 'section' && Array.isArray(n.props.start) && Array.isArray(n.props.end));

  if (!on) return null;
  const local = (e: React.PointerEvent | React.MouseEvent | React.WheelEvent) => {
    const r = svg.current!.getBoundingClientRect();
    return { x: ((e.clientX - r.left) / r.width) * 160, y: ((e.clientY - r.top) / r.height) * H };
  };
  const clampBox = (p: { x: number; y: number }) => ({ x: Math.max(5, Math.min(W - 5, p.x)), y: Math.max(5, Math.min(H - 5, p.y)) });

  // ─── 3D: the camera ────────────────────────────────────────────────────
  const api = cam.api;
  const camAt = clampBox(toMap(cam.position));
  const look = lookOf(cam);
  const horizontal = Math.hypot(look.x, look.y);
  const az = horizontal > 1e-6 ? Math.atan2(look.y, look.x) : Math.PI / 2;
  const half = Math.min(0.6, cam.fov * 0.8), len = 24;
  const cone = `M${camAt.x} ${camAt.y}L${camAt.x + Math.cos(az - half) * len} ${camAt.y - Math.sin(az - half) * len}L${camAt.x + Math.cos(az + half) * len} ${camAt.y - Math.sin(az + half) * len}Z`;
  const tgt = clampBox(toMap(cam.target));
  const standard = (p: { azimuth: number; pitch: number }) => api?.orbitTo(p, true);

  const onDown = (e: React.PointerEvent) => {
    if (mode === 'drawing') return;
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const p = local(e);
    if (p.x > W + 4) { if (mode === '3d') { drag.current = 'height'; api?.setHeight(yToZ(p.y)); } return; }
    if (mode === 'plan') { drag.current = 'aim'; onLookAt?.(toWorld(p.x, p.y)); return; }
    if (Math.hypot(p.x - camAt.x, p.y - camAt.y) < 9) { drag.current = 'move'; return; }
    drag.current = 'aim';
    const w = toWorld(p.x, p.y);
    api?.lookAt({ x: w.x, y: w.y, z: cam.target.z });
  };
  const onMove = (e: React.PointerEvent) => {
    const d = drag.current;
    if (!d) return;
    const p = local(e);
    if (d === 'height') { api?.setHeight(yToZ(Math.max(0, Math.min(H, p.y)))); return; }
    const w = toWorld(p.x, p.y);
    if (mode === 'plan') { onLookAt?.(w); return; }
    if (d === 'move') api?.moveTo(w);
    else api?.lookAt({ x: w.x, y: w.y, z: cam.target.z });
  };
  const onUp = () => { drag.current = null; };

  // ─── A section or elevation: its line and its look ─────────────────────
  const view = viewId ? nodes.get(viewId) : undefined;
  let cut: { a: { x: number; y: number }; b: { x: number; y: number }; look: { x: number; y: number } } | null = null;
  if (view?.props.kind === 'section' && Array.isArray(view.props.start) && Array.isArray(view.props.end)) {
    const s = view.props.start as number[], e = view.props.end as number[];
    const a = toMap({ x: s[0], y: s[1] }), bb = toMap({ x: e[0], y: e[1] });
    const dx = bb.x - a.x, dy = bb.y - a.y, l = Math.hypot(dx, dy) || 1;
    // The section looks left of its line (on screen y runs down: left of (dx, dy) is (dy, -dx)).
    cut = { a, b: bb, look: { x: dy / l, y: -dx / l } };
  }
  const elevationLook = view?.props.kind === 'elevation' ? ({ south: { x: 0, y: -1 }, north: { x: 0, y: 1 }, east: { x: 1, y: 0 }, west: { x: -1, y: 0 } } as Record<string, { x: number; y: number }>)[String(view.props.direction ?? 'south')] : null;

  const title = mode === '3d' ? 'Key plan' : mode === 'plan' ? 'On screen' : view?.props.kind === 'elevation' ? 'Seen from' : 'Cut at';
  return (
    <div className={`hud-map${mode !== '3d' ? ' under' : ''}`} data-testid="key-plan">
      <div className="row">
        <span className="eyebrow">{title} · {storey?.name ?? '—'}</span>
        <span className="grow" />
        <button type="button" className="icon mini" title="Hide the key plan (View › Key plan brings it back)" onClick={() => useUi.setState({ keyPlan: false })}><X size={11} /></button>
      </div>
      <svg ref={svg} viewBox={`0 0 160 ${H}`} onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerCancel={onUp}
        onWheel={mode === '3d' ? (e) => api?.orbitTo({ distance: Math.max(0.5, cam.distance * Math.exp(e.deltaY * 0.002)) }, false) : undefined}
        onDoubleClick={mode === '3d' ? () => api?.fit(true) : undefined}>
        <rect className="map-box" x={0.5} y={0.5} width={W - 1} height={H - 1} />
        {path ? <path className="footprint" d={path} /> : <rect className="footprint empty" x={W / 2 - 20} y={H / 2 - 14} width={40} height={28} />}
        {/* North, up. */}
        <g className="north" transform="translate(10 14)"><path d="M0 6V-6M-3 -2L0 -6L3 -2" /><text x={5} y={-3}>N</text></g>

        {mode === 'plan' && frame && (() => {
          const a = toMap(frame[0]), c = toMap(frame[1]);
          return <rect className="frame" x={Math.min(a.x, c.x)} y={Math.min(a.y, c.y)} width={Math.abs(c.x - a.x)} height={Math.abs(c.y - a.y)} />;
        })()}

        {cut && (
          <g className="section-mark">
            <line x1={cut.a.x} y1={cut.a.y} x2={cut.b.x} y2={cut.b.y} />
            {[cut.a, cut.b].map((p, i) => <path key={i} d={`M${p.x} ${p.y}l${cut!.look.x * 9} ${cut!.look.y * 9}`} markerEnd="url(#kp-arrow)" />)}
          </g>
        )}
        {elevationLook && (() => {
          const from = { x: W / 2 - elevationLook.x * (W / 2 - 10), y: H / 2 + elevationLook.y * (H / 2 - 10) };
          return <path className="section-mark" d={`M${from.x} ${from.y}l${elevationLook.x * 14} ${-elevationLook.y * 14}`} markerEnd="url(#kp-arrow)" />;
        })()}
        <defs><marker id="kp-arrow" viewBox="0 0 6 6" refX="5" refY="3" markerWidth="5" markerHeight="5" orient="auto"><path d="M0 0L6 3L0 6Z" /></marker></defs>

        {mode === '3d' && (
          <>
            <path className="cone" d={cone} />
            <path className="target" d={`M${tgt.x - 3} ${tgt.y}h6M${tgt.x} ${tgt.y - 3}v6`} />
            <circle className="cam" cx={camAt.x} cy={camAt.y} r={4} />
            {/* The compass stops: seen from the north, the east… and the isometrics in the corners. */}
            <text className="compass" x={W / 2} y={9} textAnchor="middle" onClick={() => standard(STANDARD.N)}>N</text>
            <text className="compass" x={W / 2} y={H - 3} textAnchor="middle" onClick={() => standard(STANDARD.S)}>S</text>
            <text className="compass" x={W - 4} y={H / 2 + 3} textAnchor="end" onClick={() => standard(STANDARD.E)}>E</text>
            <text className="compass" x={4} y={H / 2 + 3} onClick={() => standard(STANDARD.W)}>W</text>
            <path className="iso" d={`M${W - 3} 3l-8 0l8 8z`} onClick={() => standard(STANDARD.NE)}><title>From the north-east</title></path>
            <path className="iso" d={`M${W - 3} ${H - 3}l-8 0l8 -8z`} onClick={() => standard(STANDARD.SE)}><title>From the south-east</title></path>
            <path className="iso" d={`M3 ${H - 3}l8 0l-8 -8z`} onClick={() => standard(STANDARD.SW)}><title>From the south-west</title></path>
            <path className="iso" d="M3 3l8 0l-8 8z" onClick={() => standard(STANDARD.NW)}><title>From the north-west</title></path>
            {/* The height strip: the storeys, and the camera. */}
            <g className="strip">
              <line x1={STRIP} y1={0} x2={STRIP} y2={H} />
              {levels.map((l) => <g key={l.id}><line className="tick" x1={STRIP - 4} y1={zToY(l.z)} x2={STRIP + 4} y2={zToY(l.z)} /><title>{l.name}</title></g>)}
              <path className="cam-h" d={`M${STRIP - 6} ${zToY(cam.position.z)}l-5 -4v8z`}><title>{`Camera at ${cam.position.z.toFixed(1)} m: drag`}</title></path>
            </g>
          </>
        )}
      </svg>
      {mode === '3d' && (
        <div className="hud-map-tools">
          <button type="button" className="cad-toggle" title="The plan from above" onClick={() => standard(STANDARD.plan)}>PLAN</button>
          <button type="button" className={`cad-toggle${cam.ortho ? ' on' : ''}`} title={cam.ortho ? 'Orthographic: click for perspective' : 'Perspective: click for orthographic'} onClick={() => api?.setOrtho(!cam.ortho)}>{cam.ortho ? 'ORTHO' : 'PERSP'}</button>
          <button type="button" className="cad-toggle" title="Frame everything" onClick={() => api?.fit(true)}>FIT</button>
          {sections.length > 0 && (
            <select className="mini-select" value="" title="Stand at a section's line, looking its way" onChange={(e) => {
              const n = nodes.get(e.target.value);
              if (!n || !api) return;
              const s = n.props.start as number[], en = n.props.end as number[];
              const dx = en[0] - s[0], dy = en[1] - s[1], l = Math.hypot(dx, dy) || 1;
              const lookDir = { x: -dy / l, y: dx / l };
              const mid = { x: (s[0] + en[0]) / 2, y: (s[1] + en[1]) / 2 };
              const z = (levels[0]?.z ?? 0) + 1.6;
              const pose: CameraPose = { azimuth: Math.atan2(lookDir.y, lookDir.x), pitch: 0, distance: Math.max(l, 8), target: { x: mid.x + lookDir.x * Math.max(l, 8) * 0.5, y: mid.y + lookDir.y * Math.max(l, 8) * 0.5, z } };
              api.orbitTo(pose, true);
            }}>
              <option value="">Look from…</option>
              {sections.map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
            </select>
          )}
        </div>
      )}
    </div>
  );
}
