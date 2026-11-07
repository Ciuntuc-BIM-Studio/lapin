/**
 * Grips on sketch lines in the 3D view (stage B, decision 375): the selected
 * shape drafted on a reference plane (a face's, or one of its own) shows its
 * points on the plane; one dragged follows the cursor on that plane, inferred
 * as the sketch tools infer (the other shapes on the plane, the bodies' edges
 * in it, the axes), and the shape is changed in one step on release. The solid
 * made from the sketch follows it.
 */

import { useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import type { Vec2 } from '../platform/module';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import { useCamera } from './camera';
import { useCad } from './cad/session';
import { fromJson, toJson, type Shape } from './cad/shapes';
import { frameOfView, rayOnPlane, toWorld } from './workPlane';
import { useDirect } from './sketch/direct';
import { gripPoints, moveGrip } from './sketch/grips';
import { axesInPlane, infer, segsOf, type Inferred, type Seg2 } from './sketch/inference';
import { edgesInPlane, sceneSegments } from './sketch/edges';
import { arcPts } from './sketch/direct';

const flat = (s: Shape): Vec2[][] => {
  if (s.type === 'line') return [[s.a, s.b]];
  if (s.type === 'polyline') return [s.closed ? [...s.pts, s.pts[0]] : s.pts];
  if (s.type === 'circle') return [Array.from({ length: 49 }, (_, i) => ({ x: s.c.x + s.r * Math.cos((i * Math.PI) / 24), y: s.c.y + s.r * Math.sin((i * Math.PI) / 24) }))];
  const g = gripPoints(s);
  return [arcPts(g[0], g[1], g[2])];
};

export function SketchGrips3D({ camera, canvas, scene }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null>; scene: React.RefObject<THREE.Scene | null> }) {
  useCamera();
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const command = useCad((s) => s.command);
  const direct = useDirect((s) => !!s.tool);
  const [drag, setDrag] = useState<{ i: number; shape: Shape; inferred: Inferred | null } | null>(null);
  const others = useRef<{ edges: Seg2[]; points: Vec2[] } | null>(null);

  const draft = selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const viewId = typeof draft?.props.viewId === 'string' ? draft.props.viewId : null;
  const isPlane = viewId ? nodes.get(viewId)?.props.kind === 'plane' : false;
  const frame = useMemo(() => (viewId && isPlane ? frameOfView(viewId, nodes) : null), [viewId, isPlane, nodes]);
  const shape = draft?.props.shape ? fromJson(draft.props.shape as Record<string, unknown>) : null;
  if (!draft || !frame || !shape || command || direct) return null;

  const rect = () => canvas.current?.getBoundingClientRect() ?? null;
  const toScreen = (p: Vec2) => {
    const c = camera.current, r = rect();
    if (!c || !r) return null;
    c.updateMatrixWorld();
    const w = toWorld(frame, p);
    const v = new THREE.Vector3(w.x, w.y, w.z).project(c);
    if (v.z > 1 || v.z < -1) return null;
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  const ppm = (p: Vec2) => {
    const a = toScreen(p), b = toScreen({ x: p.x + 1, y: p.y });
    return a && b ? Math.max(1e-3, Math.hypot(b.x - a.x, b.y - a.y)) : 50;
  };
  const onPlane = (e: React.PointerEvent): Vec2 | null => {
    const c = camera.current, r = rect();
    if (!c || !r) return null;
    const ray = new THREE.Raycaster();
    ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
    return rayOnPlane(frame, ray.ray.origin, ray.ray.direction);
  };
  /** What the grip may snap to: the other shapes on the plane, and the bodies' edges in it. */
  const snapLines = () => {
    if (!others.current) {
      const edges: Seg2[] = [];
      for (const n of nodes.values()) {
        if (n.id === draft.id || n.props.viewId !== viewId || !n.props.shape) continue;
        const s = fromJson(n.props.shape as Record<string, unknown>);
        if (s) for (const pl of flat(s)) edges.push(...segsOf(pl, false));
      }
      const scn = edgesInPlane(sceneSegments(scene.current, frame), frame);
      others.current = { edges: [...edges, ...scn.edges], points: scn.points };
    }
    return others.current;
  };

  const grips = gripPoints(drag?.shape ?? shape);
  const path = (s: Shape) => flat(s).map((pl) => {
    const q = pl.map(toScreen);
    return q.some((x) => !x) ? '' : q.map((x, i) => `${i ? 'L' : 'M'}${x!.x.toFixed(1)} ${x!.y.toFixed(1)}`).join('');
  }).join('');
  const inf = drag?.inferred;
  const infAt = inf ? toScreen(inf.point) : null;

  return (
    <>
      <svg className="grip-overlay face-sketch" data-sketch3d="grips">
        {drag && <path d={path(drag.shape)} className="face-sketch-line" />}
        {grips.map((g, i) => {
          const s = toScreen(g);
          return s && (
            <rect key={i} x={s.x - 5} y={s.y - 5} width={10} height={10} className="grip" data-sketch3d="grip" style={{ pointerEvents: 'all', cursor: 'move', fill: '#2f9e6a', stroke: '#fff' }}
              onPointerDown={(e) => { if (e.button !== 0) return; e.stopPropagation(); e.preventDefault(); (e.target as Element).setPointerCapture(e.pointerId); others.current = null; setDrag({ i, shape, inferred: null }); }}
              onPointerMove={(e) => {
                if (!drag) return;
                e.stopPropagation();
                const raw = onPlane(e);
                if (!raw) return;
                const o = snapLines();
                const pts = gripPoints(shape);
                const i2 = infer({ raw, base: pts[drag.i === 0 ? 1 : drag.i - 1] ?? null, edges: o.edges, points: [...o.points, ...pts.filter((_, k) => k !== drag.i)], axes: axesInPlane(frame.right, frame.up), tol: 10 / ppm(raw) });
                const next = moveGrip(shape, drag.i, i2.point);
                if (next) setDrag({ i: drag.i, shape: next, inferred: i2 });
              }}
              onPointerUp={(e) => {
                e.stopPropagation();
                const d = drag;
                setDrag(null);
                if (d && d.shape !== shape) void execute('drafting.updateDraft', { ids: [draft.id], shape: toJson(d.shape) });
              }} />
          );
        })}
      </svg>
      {infAt && inf && inf.kind !== 'On face' && <div className={`face-sketch-snap-name ${inf.color ?? ''}`} style={{ left: infAt.x + 10, top: infAt.y + 8 }}>{inf.kind}</div>}
    </>
  );
}
