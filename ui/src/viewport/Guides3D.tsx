/**
 * The tape measure's guides in the 3D view (stage C, decision 376): each
 * guide line dashed across the view, each guide point a small cross. They are
 * the project's (`LapinGuide`), snapped to by both ways of sketching, never
 * exported or printed; Studio → Sketch → Erase guides takes them all away.
 */

import { useMemo } from 'react';
import * as THREE from 'three';
import { useSession } from '../engine/session';
import { useCamera, type Vec3 } from './camera';
import { guideSegment, guidesOf } from './sketch/edges';

/** A segment in the model clipped to what lies in front of the camera, on the screen; null when it is all behind. */
function onScreen(a: Vec3, b: Vec3, cam: THREE.Camera, w: number, h: number): [number, number, number, number] | null {
  cam.updateMatrixWorld();
  const pa = new THREE.Vector3(a.x, a.y, a.z).applyMatrix4(cam.matrixWorldInverse);
  const pb = new THREE.Vector3(b.x, b.y, b.z).applyMatrix4(cam.matrixWorldInverse);
  const near = -((cam as THREE.PerspectiveCamera).near ?? 0.1) - 1e-3;
  if (pa.z > near && pb.z > near) return null;
  if (pa.z > near) pa.lerp(pb, (pa.z - near) / (pa.z - pb.z));
  else if (pb.z > near) pb.lerp(pa, (pb.z - near) / (pb.z - pa.z));
  const s = (v: THREE.Vector3) => { const p = v.applyMatrix4(cam.projectionMatrix); return [((p.x + 1) / 2) * w, ((1 - p.y) / 2) * h]; };
  const [x1, y1] = s(pa), [x2, y2] = s(pb);
  return [x1, y1, x2, y2];
}

export function Guides3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const nodes = useSession((s) => s.nodes);
  const guides = useMemo(() => guidesOf(nodes), [nodes]);
  const cam = camera.current, r = canvas.current?.getBoundingClientRect();
  if (!guides.length || !cam || !r) return null;
  return (
    <svg className="grip-overlay face-sketch" data-sketch3d="guides">
      {guides.map((g, i) => {
        const seg = guideSegment(g);
        if (seg) {
          const s = onScreen(seg[0], seg[1], cam, r.width, r.height);
          return s && <line key={i} x1={s[0]} y1={s[1]} x2={s[2]} y2={s[3]} className="sketch-guide guide" />;
        }
        const s = onScreen(g.a, g.a, cam, r.width, r.height);
        return s && <path key={i} d={`M${s[0] - 5} ${s[1]}h10M${s[0]} ${s[1] - 5}v10`} className="sketch-guide guide" />;
      })}
    </svg>
  );
}
