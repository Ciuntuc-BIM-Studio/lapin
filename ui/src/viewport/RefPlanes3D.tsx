/**
 * The selected reference plane's handle over the 3D view: an arrow at the
 * middle of its quad that drags the plane along its normal (to the
 * centimetre), with the offset read out beside it while it moves, as the
 * section box's faces are dragged. The drag ends as one engine change
 * (`view.movePlane`), so the plane's view and the solids sketched on it follow.
 */

import { useRef, useState } from 'react';
import * as THREE from 'three';
import { create } from 'zustand';
import { execute, useSession } from '../engine/session';
import { useUi } from '../app/store';
import { useCamera, type Vec3 } from './camera';
import { formatLength } from './liveInput';
import { modelBounds } from './sectionBox';
import { alongNormal, planeCentre, planeOf } from './refPlanes';
import { t } from '../i18n';

/** The plane being dragged and how far along its normal it has gone, for the scene to draw it there meanwhile. */
export const useRefPlaneDrag = create<{ id: string | null; along: number }>(() => ({ id: null, along: 0 }));

export function RefPlanes3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const shown = useUi((s) => s.refPlanes);
  const selection = useUi((s) => s.selection);
  const node = useSession((s) => (selection ? s.nodes.get(selection) : undefined));
  const drag = useRef<{ start: number } | null>(null);
  const [along, setAlong] = useState<number | null>(null);
  if (!shown || !node || node.type !== 'LapinView' || node.props.kind !== 'plane') return null;
  const plane = planeOf(node);
  const st = useSession.getState();
  const centre = planeCentre(plane, modelBounds(st.render.values(), 0.5, 0, st.meshes));
  const rect = () => canvas.current?.getBoundingClientRect();
  const toScreen = (p: Vec3) => {
    const c = camera.current, r = rect();
    if (!c || !r) return null;
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
  const offsetAt = (e: React.PointerEvent) => {
    const ray = rayAt(e);
    return ray ? alongNormal(plane, centre, ray.origin, ray.direction) : null;
  };
  const move = (e: React.PointerEvent) => {
    const d = drag.current;
    if (!d) return;
    const v = offsetAt(e);
    if (v == null) return;
    const step = e.shiftKey ? 0.1 : 0.01;
    const a = Math.round((v - d.start) / step) * step;
    setAlong(a);
    useRefPlaneDrag.setState({ id: plane.id, along: a });
  };
  const end = () => {
    if (!drag.current) return;
    drag.current = null;
    const a = useRefPlaneDrag.getState().along;
    useRefPlaneDrag.setState({ id: null, along: 0 });
    setAlong(null);
    if (Math.abs(a) >= 0.005) void execute('view.movePlane', { id: plane.id, along: a });
  };
  const shift = along ?? 0;
  const moved = (off: number): Vec3 => ({ x: centre.x + plane.normal.x * off, y: centre.y + plane.normal.y * off, z: centre.z + plane.normal.z * off });
  const at = toScreen(moved(shift));
  const tip = toScreen(moved(shift + 0.01));
  if (!at) return null;
  let dx = tip ? tip.x - at.x : 0, dy = tip ? tip.y - at.y : -1;
  const len = Math.hypot(dx, dy) || 1;
  dx /= len; dy /= len;
  const p = (s: number, n: number) => `${at.x + dx * s - dy * n},${at.y + dy * s + dx * n}`;
  return (
    <svg className="grip-overlay ref-plane-handle" onPointerMove={move} onPointerUp={end} onPointerCancel={end}>
      <g className={`section-box-handle${along != null ? ' active' : ''}`} data-testid="ref-plane-handle"
        onPointerDown={(e) => {
          if (e.button !== 0) return;
          const v = offsetAt(e);
          if (v == null) return;
          e.stopPropagation(); e.currentTarget.setPointerCapture?.(e.pointerId);
          drag.current = { start: v };
          setAlong(0);
          useRefPlaneDrag.setState({ id: plane.id, along: 0 });
        }}>
        <title>{t('Drag to move the plane along its normal (Shift: by 10 cm); what is drawn on it follows')}</title>
        <circle cx={at.x} cy={at.y} r={10} className="section-box-hit" />
        <polygon points={`${p(18, 0)} ${p(5, 6)} ${p(5, -6)}`} />
        <polygon points={`${p(-18, 0)} ${p(-5, 6)} ${p(-5, -6)}`} />
        <circle cx={at.x} cy={at.y} r={4} />
      </g>
      {along != null && (
        <g className="plan-preview">
          <text x={at.x + 14} y={at.y - 12}>{`${along >= 0 ? '+' : '−'}${formatLength(Math.abs(along))}`}</text>
        </g>
      )}
    </svg>
  );
}
