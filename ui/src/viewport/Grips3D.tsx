/**
 * Direct editing in the 3D view: the selection's grips and reference lines
 * (the plan's, from every module), drawn over the scene at the height each
 * element stands at and dragged on that horizontal plane. The same editor as
 * the plan's: shared grips, Alt and Tab, Ctrl/⌘ to insert or remove a vertex,
 * typed lengths and angles, flip arrows. A grip dragged infers its point as
 * Move does (snap3d.ts, decision 399): an endpoint before a midpoint before an
 * edge within the reach on the screen, then the red and green axes through the
 * grip's start, a face, else its plane; the inference is marked and named at the
 * cursor, and a length or an angle typed shows in the live box.
 */

import { useRef, useState } from 'react';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { num } from '../engine/model';
import { useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { useGripEditor } from './GripLayer';
import { useCamera } from './camera';
import { projector, snapAt } from './scene3d';
import { gripTarget, type Snapped3 } from './snap3d';
import { LiveBox } from './LiveBox';
import { useLive } from './liveInput';

/** The height an element's grips stand at: its base (a floor's top, a beam's top, a roof's wall plate). */
function gripHeight(n: NodeDto | undefined, nodes: Map<string, NodeDto>): number {
  if (!n) return 0;
  const storey = n.storeyId ? nodes.get(n.storeyId) : undefined;
  const floor = num(storey, 'elevation'), top = floor + num(storey, 'height', 3);
  switch (n.type) {
    case 'IfcSlab': return floor + num(n, 'offset');
    case 'IfcBeam': return top + num(n, 'topOffset');
    case 'IfcRoof': return top + num(n, 'eaveOffset');
    default: return floor + num(n, 'baseOffset');
  }
}

export function Grips3D({ camera, canvas, scene }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null>; scene: React.RefObject<THREE.Scene | null> }) {
  const svg = useRef<SVGSVGElement>(null);
  const cam = useCamera();
  const nodes = useSession((s) => s.nodes);
  const tool = findTool(useUi((s) => s.tool));
  // What the dragged grip infers now (marked at the cursor), and the height of its plane (the live box stands on it).
  const [snap, setSnap] = useState<Snapped3 | null>(null);
  const plane = useRef(0);
  const liveOwner = useLive((s) => s.owner);
  const size = () => { const r = canvas.current?.getBoundingClientRect(); return { w: r?.width ?? 1, h: r?.height ?? 1, left: r?.left ?? 0, top: r?.top ?? 0 }; };
  const toScreen = (p: Vec2, z = 0) => {
    const c = camera.current;
    if (!c) return { x: -9999, y: -9999 };
    const v = new THREE.Vector3(p.x, p.y, z).project(c);
    if (v.z > 1 || v.z < -1) return { x: -9999, y: -9999 };
    const { w, h } = size();
    return { x: ((v.x + 1) / 2) * w, y: ((1 - v.y) / 2) * h };
  };
  const aim = (clientX: number, clientY: number, from: Vec2, z = 0): Vec2 => {
    const c = camera.current;
    if (!c) return from;
    plane.current = z;
    // Onto the other elements' corners, middles, edges and faces, or the axes through the grip's start (docs/plan-3d-modelling.md,
    // stage C; decision 399): Move's ranking, the point under them on the grip's plane.
    const snapped = snapAt({ camera, canvas, scene }, clientX, clientY, { level: z, base: { x: from.x, y: from.y, z }, axes: ['red', 'green'], skip: new Set(useUi.getState().selected) });
    const to = gripTarget(snapped);
    setSnap(to ? snapped : null);
    if (to) return to;
    const { w, h, left, top } = size();
    const ray = new THREE.Raycaster();
    ray.setFromCamera(new THREE.Vector2(((clientX - left) / w) * 2 - 1, -((clientY - top) / h) * 2 + 1), c);
    const hit = ray.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), -z), new THREE.Vector3());
    // To the centimetre, as a drag in plan snaps to its grid.
    return hit ? { x: Math.round(hit.x * 100) / 100, y: Math.round(hit.y * 100) / 100 } : from;
  };
  // Pixels per metre at the camera's target: what keeps shared grips together.
  const { h } = size();
  const scale = cam.ortho ? h / (2 * cam.distance * Math.tan(cam.fov / 2)) : h / (2 * Math.max(cam.distance, 0.1) * Math.tan(cam.fov / 2));
  const editor = useGripEditor({
    viewIds: [], mode: 'plan', anyStorey: true, toScreen, aim, scale, svg,
    zOf: (owner) => gripHeight(owner ? nodes.get(owner) : undefined, nodes),
    disabled: !!tool?.gesture,
  });
  const project = projector({ camera, canvas, scene });
  const mark = snap ? project(snap.point) : null;
  const guide = snap?.guide ? [project(snap.guide.from), project(snap.guide.to)] : null;
  const markClass = snap?.kind === 'Endpoint' ? 'sketch-end' : snap?.kind === 'Midpoint' ? 'sketch-mid' : snap?.kind === 'On edge' ? 'sketch-edge' : 'face-sketch-cursor';
  return (
    <>
      <svg ref={svg} className="grip-overlay" onPointerDown={(e) => { if (editor.onPointerDown(e)) e.stopPropagation(); }}
        onPointerMove={(e) => { if (!editor.onPointerMove(e) && snap) setSnap(null); }} onPointerUp={() => { editor.onPointerUp(); setSnap(null); }} onPointerLeave={editor.onPointerLeave}>
        {editor.render}
        {mark && <g className="face-sketch">
          {guide?.[0] && guide[1] && <line x1={guide[0].x} y1={guide[0].y} x2={guide[1].x} y2={guide[1].y} className={`sketch-guide ${snap!.guide!.color}`} />}
          <circle cx={mark.x} cy={mark.y} r={snap!.kind === 'On face' ? 3 : 5} className={markClass} data-grip3d="mark" />
        </g>}
      </svg>
      {mark && snap && <div className={`face-sketch-snap-name ${snap.color ?? ''}`} style={{ left: mark.x + 10, top: mark.y + 8 }} data-grip3d="inference">{snap.kind}</div>}
      {liveOwner === 'grip' && <LiveBox toScreen={(p) => toScreen(p, plane.current)} />}
    </>
  );
}
