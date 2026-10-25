/**
 * Direct editing in the 3D view: the selection's grips and reference lines
 * (the plan's, from every module), drawn over the scene at the height each
 * element stands at and dragged on that horizontal plane. The same editor as
 * the plan's: shared grips, Alt and Tab, Ctrl/⌘ to insert or remove a vertex,
 * typed lengths and angles, flip arrows.
 */

import { useRef } from 'react';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { num } from '../engine/model';
import { useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { useGripEditor } from './GripLayer';
import { useCamera } from './camera';

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

export function Grips3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  const svg = useRef<SVGSVGElement>(null);
  const cam = useCamera();
  const nodes = useSession((s) => s.nodes);
  const tool = findTool(useUi((s) => s.tool));
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
  return (
    <svg ref={svg} className="grip-overlay" onPointerDown={(e) => { if (editor.onPointerDown(e)) e.stopPropagation(); }}
      onPointerMove={(e) => { editor.onPointerMove(e); }} onPointerUp={() => { editor.onPointerUp(); }}>
      {editor.render}
    </svg>
  );
}
