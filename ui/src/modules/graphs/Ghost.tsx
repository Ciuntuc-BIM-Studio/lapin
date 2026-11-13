/**
 * What the open graph would make (docs/plan-visual-graphs.md §3), drawn as a ghost in every 3D view and plan: the meshes
 * the engine built for the elements its commands made on a copy of the model, their plan lines on their storey, and the
 * points and polylines of the geometry nodes. Nothing in it takes the pointer.
 */

import { useEffect, useState } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps, PlanOverlayProps } from '../../platform/module';
import { useGraphs, type Ghost } from './store';

const GHOST_COLOUR = 0x7c4dff;

/** The ghost as objects of the scene. */
export function ghostGroup(g: Ghost): THREE.Group {
  const group = new THREE.Group();
  group.name = 'graph-ghost';
  const fill = new THREE.MeshBasicMaterial({ color: GHOST_COLOUR, transparent: true, opacity: 0.28, depthWrite: false, side: THREE.DoubleSide });
  const line = new THREE.LineBasicMaterial({ color: GHOST_COLOUR, transparent: true, opacity: 0.9 });
  for (const p of g.parts) {
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(p.positions, 3));
    geo.setIndex(p.indices);
    const mesh = new THREE.Mesh(geo, fill);
    mesh.raycast = () => undefined;
    mesh.renderOrder = 3;
    group.add(mesh);
    if (p.edges.length > 0) {
      const eg = new THREE.BufferGeometry();
      eg.setAttribute('position', new THREE.Float32BufferAttribute(p.edges, 3));
      const edges = new THREE.LineSegments(eg, line);
      edges.raycast = () => undefined;
      group.add(edges);
    }
  }
  for (const l of g.lines) {
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(l.closed ? [...l.points, ...l.points.slice(0, 3)] : l.points, 3));
    const obj = new THREE.Line(geo, line);
    obj.raycast = () => undefined;
    group.add(obj);
  }
  if (g.points.length > 0) {
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(g.points, 3));
    const pts = new THREE.Points(geo, new THREE.PointsMaterial({ color: GHOST_COLOUR, size: 7, sizeAttenuation: false }));
    pts.raycast = () => undefined;
    group.add(pts);
  }
  group.userData.dispose = () => {
    group.traverse((o) => { if (o instanceof THREE.Mesh || o instanceof THREE.Line || o instanceof THREE.Points) { o.geometry.dispose(); if (o instanceof THREE.Points) (o.material as THREE.Material).dispose(); } });
    fill.dispose();
    line.dispose();
  };
  return group;
}

export function GraphGhost3D({ canvas, scene, invalidate }: Overlay3DProps) {
  const ghost = useGraphs((s) => s.ghost);
  // The view makes its scene after its overlays first mount: wait for it.
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (ready) return;
    let frame = 0;
    const look = () => { if (scene?.current) setReady(true); else frame = requestAnimationFrame(look); };
    look();
    return () => cancelAnimationFrame(frame);
  }, [scene, ready]);
  useEffect(() => {
    const s = scene?.current;
    if (!ready || !s || !ghost) return;
    const group = ghostGroup(ghost);
    s.add(group);
    // For the guide's scenes: how many parts the ghost shows.
    const host = canvas.current;
    if (host) host.dataset.graphGhost = String(ghost.parts.length + ghost.lines.length);
    invalidate?.current?.();
    return () => {
      s.remove(group);
      if (host) delete host.dataset.graphGhost;
      (group.userData.dispose as () => void)();
      invalidate?.current?.();
    };
  }, [ready, canvas, scene, invalidate, ghost]);
  return null;
}

export function GraphGhostPlan({ toScreen, view }: PlanOverlayProps) {
  const ghost = useGraphs((s) => s.ghost);
  if (!ghost) return null;
  const path = (pts: number[], stride: number, closed: boolean) => {
    let d = '';
    for (let i = 0; i + 1 < pts.length; i += stride) { const p = toScreen({ x: pts[i]!, y: pts[i + 1]! }); d += `${d ? 'L' : 'M'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`; }
    return closed ? `${d}Z` : d;
  };
  const items = ghost.plan.filter((p) => !view.storeyId || !p.storey || p.storey === view.storeyId);
  const dots: { x: number; y: number }[] = [];
  for (let i = 0; i + 2 < ghost.points.length; i += 3) dots.push(toScreen({ x: ghost.points[i]!, y: ghost.points[i + 1]! }));
  return (
    <g className="graph-ghost-plan" data-testid="graph-ghost-plan" data-count={items.length + ghost.lines.length} pointerEvents="none">
      {items.map((p, i) => <path key={`p${i}`} d={path(p.points, 2, p.closed)} fill={p.closed ? 'rgba(124,77,255,0.18)' : 'none'} stroke="#7c4dff" strokeWidth={1.5} />)}
      {ghost.lines.map((l, i) => <path key={`l${i}`} d={path(l.points, 3, l.closed)} fill="none" stroke="#7c4dff" strokeWidth={1.5} strokeDasharray="6 3" />)}
      {dots.map((d, i) => <circle key={`d${i}`} cx={d.x} cy={d.y} r={3} fill="#7c4dff" />)}
    </g>
  );
}
