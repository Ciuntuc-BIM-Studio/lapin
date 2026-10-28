/**
 * The section box over the 3D view: its edges, and a handle on each face's
 * middle that drags the face along its axis (to the centimetre), as the
 * plan's grips are drawn over the plan.
 */

import { useRef, useState } from 'react';
import * as THREE from 'three';
import { useCamera, type Vec3 } from './camera';
import { alongAxis, boxEdges, faceHandle, FACES, moveFace, useSectionBox, type FaceKey } from './sectionBox';

export function SectionBox3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const box = useSectionBox((s) => s.box);
  const drag = useRef<FaceKey | null>(null);
  const [active, setActive] = useState<FaceKey | null>(null);
  if (!box) return null;
  const rect = () => canvas.current?.getBoundingClientRect();
  const toScreen = (p: Vec3) => {
    const c = camera.current, r = rect();
    if (!c || !r) return null;
    // The camera as it is now (frames are drawn on demand: it may have moved since the last).
    c.updateMatrixWorld();
    const v = new THREE.Vector3(p.x, p.y, p.z).project(c);
    if (v.z > 1 || v.z < -1) return null;
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  const move = (e: React.PointerEvent) => {
    const f = drag.current, c = camera.current, r = rect(), b = useSectionBox.getState().box;
    if (!f || !c || !r || !b) return;
    const ray = new THREE.Raycaster();
    ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
    const h = faceHandle(b, f);
    const v = alongAxis(h.at, h.axis, ray.ray.origin, ray.ray.direction);
    if (v != null) useSectionBox.setState({ box: moveFace(b, f, Math.round(v * 100) / 100) });
  };
  const lines = boxEdges(box).map(([a, b]) => [toScreen(a), toScreen(b)] as const).filter((l) => l[0] && l[1]);
  return (
    <svg className="grip-overlay section-box" onPointerMove={move}
      onPointerUp={() => { drag.current = null; setActive(null); }}>
      {lines.map(([a, b], i) => <line key={i} className="section-box-edge" x1={a!.x} y1={a!.y} x2={b!.x} y2={b!.y} />)}
      {FACES.map((f) => {
        const h = faceHandle(box, f);
        const at = toScreen(h.at), tip = toScreen({ x: h.at.x + h.out.x * 0.01, y: h.at.y + h.out.y * 0.01, z: h.at.z + h.out.z * 0.01 });
        if (!at) return null;
        // An arrow out of the face, the way it grows.
        let dx = tip ? tip.x - at.x : 0, dy = tip ? tip.y - at.y : -1;
        const len = Math.hypot(dx, dy) || 1;
        dx /= len; dy /= len;
        const p = (s: number, n: number) => `${at.x + dx * s - dy * n},${at.y + dy * s + dx * n}`;
        return (
          <g key={f} className={`section-box-handle${active === f ? ' active' : ''}`} data-face={f}
            onPointerDown={(e) => { e.stopPropagation(); e.currentTarget.setPointerCapture?.(e.pointerId); drag.current = f; setActive(f); }}>
            <circle cx={at.x} cy={at.y} r={9} className="section-box-hit" />
            <polygon points={`${p(16, 0)} ${p(4, 6)} ${p(4, -6)}`} />
            <circle cx={at.x} cy={at.y} r={4} />
          </g>
        );
      })}
    </svg>
  );
}
