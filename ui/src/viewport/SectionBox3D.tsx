/**
 * The section box over the 3D view: its edges, a handle on each face's
 * middle that drags the face along its axis (to the centimetre), and a round
 * handle past its top corner that turns it about the vertical through its
 * middle (by the degree; Shift: by 15°), as the plan's grips are drawn over
 * the plan. A drag ends as one change: a saved 3D view keeps the box then.
 */

import { useRef, useState } from 'react';
import * as THREE from 'three';
import { useCamera, type Vec3 } from './camera';
import { alongAxis, boxCentre, boxEdges, faceHandle, FACES, moveFace, rotateBox, rotateHandle, useSectionBox, type FaceKey, type SectionBox } from './sectionBox';
import { commitBox } from './sectionBoxTools';

type Drag = { face: FaceKey } | { turn: true; start: number; from: SectionBox };

export function SectionBox3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const box = useSectionBox((s) => s.box);
  const drag = useRef<Drag | null>(null);
  const [active, setActive] = useState<string | null>(null);
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
  const rayAt = (e: React.PointerEvent) => {
    const c = camera.current, r = rect();
    if (!c || !r) return null;
    const ray = new THREE.Raycaster();
    ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
    return ray.ray;
  };
  /** The pointer's angle about the box's middle, on the plane of its top. */
  const angleAt = (e: React.PointerEvent, b: SectionBox) => {
    const ray = rayAt(e);
    if (!ray) return null;
    const hit = ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), -b.max[2]), new THREE.Vector3());
    if (!hit) return null;
    const c = boxCentre(b);
    return Math.atan2(hit.y - c.y, hit.x - c.x);
  };
  const move = (e: React.PointerEvent) => {
    const d = drag.current, b = useSectionBox.getState().box;
    if (!d || !b) return;
    if ('face' in d) {
      const ray = rayAt(e);
      if (!ray) return;
      const h = faceHandle(b, d.face);
      const v = alongAxis(b, h.at, h.axis, ray.origin, ray.direction);
      if (v != null) useSectionBox.setState({ box: moveFace(b, d.face, Math.round(v * 100) / 100) });
      return;
    }
    const a = angleAt(e, d.from);
    if (a == null) return;
    const step = e.shiftKey ? Math.PI / 12 : Math.PI / 180;
    const rot = Math.round(((d.from.rot ?? 0) + a - d.start) / step) * step;
    useSectionBox.setState({ box: rotateBox(d.from, rot) });
  };
  const end = () => {
    if (!drag.current) return;
    drag.current = null;
    setActive(null);
    commitBox();
  };
  const lines = boxEdges(box).map(([a, b]) => [toScreen(a), toScreen(b)] as const).filter((l) => l[0] && l[1]);
  const turn = toScreen(rotateHandle(box)), corner = toScreen(boxEdges(box)[11][1]);
  return (
    <svg className="grip-overlay section-box" onPointerMove={move} onPointerUp={end} onPointerCancel={end}>
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
            onPointerDown={(e) => { e.stopPropagation(); e.currentTarget.setPointerCapture?.(e.pointerId); drag.current = { face: f }; setActive(f); }}>
            <circle cx={at.x} cy={at.y} r={9} className="section-box-hit" />
            <polygon points={`${p(16, 0)} ${p(4, 6)} ${p(4, -6)}`} />
            <circle cx={at.x} cy={at.y} r={4} />
          </g>
        );
      })}
      {turn && (
        <g className={`section-box-handle section-box-turn${active === 'turn' ? ' active' : ''}`} data-face="turn"
          onPointerDown={(e) => {
            const start = angleAt(e, box);
            if (start == null) return;
            e.stopPropagation(); e.currentTarget.setPointerCapture?.(e.pointerId);
            drag.current = { turn: true, start, from: box }; setActive('turn');
          }}>
          {corner && <line className="section-box-edge" x1={corner.x} y1={corner.y} x2={turn.x} y2={turn.y} />}
          <circle cx={turn.x} cy={turn.y} r={11} className="section-box-hit" />
          <path d={`M ${turn.x - 6} ${turn.y + 2} A 6 6 0 1 1 ${turn.x + 2} ${turn.y + 6}`} className="section-box-turn-arc" />
          <polygon points={`${turn.x - 9},${turn.y + 1} ${turn.x - 3},${turn.y + 1} ${turn.x - 6},${turn.y + 6}`} />
        </g>
      )}
    </svg>
  );
}
