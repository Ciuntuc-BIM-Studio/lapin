/**
 * The command line's commands in the 3D view (decision 312; workPlane.ts): while
 * one runs, a left click places its point on the work plane (the active
 * storey's level, or the reference plane selected), aimed as on a plan (the
 * object snaps on the plan's cut lines and its drafts, ortho, the guides);
 * the preview, the snap and the guides are drawn over the model. Pick a face
 * makes a reference plane on the face clicked and draws on it. The right and
 * middle buttons still turn and move the view.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import type { Vec2 } from '../platform/module';
import { useUi } from '../app/store';
import { activeStorey, execute, useSession } from '../engine/session';
import { useCamera } from './camera';
import { useDrawing } from './drawing';
import { useCadCanvas, snapShapes, type Aimed } from './cad/aim';
import { CadOverlay } from './cad/canvas';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import { nearestOnShape } from './cad/shapes';
import { frameOfView, rayOnPlane, toWorld, workPlaneView } from './workPlane';

export function Draft3D({ camera, canvas, scene }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null>; scene: React.RefObject<THREE.Scene | null> }) {
  useCamera();
  const command = useCad((s) => s.command);
  const nodes = useSession((s) => s.nodes);
  const selection = useUi((s) => s.selection);
  const planViewId = useUi((s) => s.planViewId);
  const storeyId = useUi((s) => s.storeyId) ?? activeStorey()?.id ?? null;
  const work = useMemo(() => workPlaneView({ selection, planViewId, storeyId }, nodes), [selection, planViewId, storeyId, nodes]);
  const frame = useMemo(() => (work ? frameOfView(work.id, nodes) : null), [work, nodes]);
  const active = !!command && !!frame && !!work;
  const drawing = useDrawing(active ? work!.id : null);
  const [aimed, setAimed] = useState<Aimed | null>(null);
  const [scale, setScale] = useState(50);
  const [facePick, setFacePick] = useState(false);
  const { drafts } = useMemo(() => snapShapes(drawing), [drawing]);
  const scaleRef = useRef(scale);
  scaleRef.current = scale;
  const { aim, acquire } = useCadCanvas({
    viewId: active ? work!.id : null, drawing, scale,
    pickAny: (p) => {
      const tol = 8 / scaleRef.current;
      let best: { id: string; d: number } | null = null;
      for (const [id, list] of drafts) for (const s of list) { const d = nearestOnShape(s, p).dist; if (d <= tol && (!best || d < best.d)) best = { id, d }; }
      return best?.id ?? null;
    },
  });
  const aimRef = useRef(aim);
  aimRef.current = aim;
  const facePickRef = useRef(facePick);
  facePickRef.current = facePick;
  useEffect(() => { if (!command) { setAimed(null); setFacePick(false); } }, [command]);

  const rectOf = () => canvas.current?.getBoundingClientRect() ?? null;
  const project = (p: THREE.Vector3): { x: number; y: number } | null => {
    const c = camera.current, r = rectOf();
    if (!c || !r) return null;
    c.updateMatrixWorld();
    const v = p.clone().project(c);
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  const toScreen = (p: Vec2) => {
    if (!frame) return { x: 0, y: 0 };
    const w = toWorld(frame, p);
    return project(new THREE.Vector3(w.x, w.y, w.z)) ?? { x: 0, y: 0 };
  };

  useEffect(() => {
    if (!active || !frame) return;
    const inside = (e: PointerEvent) => {
      const r = rectOf();
      return !!r && e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    };
    const rayAt = (e: PointerEvent): THREE.Raycaster | null => {
      const c = camera.current, r = rectOf();
      if (!c || !r) return null;
      c.updateMatrixWorld();
      const ray = new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
      return ray;
    };
    /** The cursor on the work plane, and the plane's pixels per metre there (for the snaps' reach). */
    const rawAt = (e: PointerEvent): Vec2 | null => {
      const ray = rayAt(e);
      if (!ray) return null;
      const o = ray.ray.origin, d = ray.ray.direction;
      const p = rayOnPlane(frame, { x: o.x, y: o.y, z: o.z }, { x: d.x, y: d.y, z: d.z });
      if (!p) return null;
      const a = toWorld(frame, p), b = toWorld(frame, { x: p.x + 1, y: p.y });
      const sa = project(new THREE.Vector3(a.x, a.y, a.z)), sb = project(new THREE.Vector3(b.x, b.y, b.z));
      if (sa && sb) { const px = Math.hypot(sb.x - sa.x, sb.y - sa.y); if (px > 1e-6 && Math.abs(px - scaleRef.current) / scaleRef.current > 0.05) setScale(px); }
      return p;
    };
    const onMove = (e: PointerEvent) => {
      if (e.buttons !== 0 || !inside(e) || facePickRef.current) return;
      const raw = rawAt(e);
      if (!raw) return;
      const step = useCad.getState().step;
      const a = aimRef.current(raw, step?.base ?? null, { orthoHeld: e.shiftKey });
      setAimed(a);
      useCad.setState({ cursor: a.point });
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .cad-line, input, button')) return;
      e.stopPropagation(); e.preventDefault();
      if (facePickRef.current) { void planeOnFace(e); return; }
      const step = useCad.getState().step;
      const raw = rawAt(e);
      if (!step || !raw) return;
      const a = aimRef.current(raw, step.base ?? null, { orthoHeld: e.shiftKey });
      void feedPoint(step.want === 'pick' || step.want === 'select' ? raw : a.point);
      acquire(a.point);
    };
    const onUp = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .cad-line, input, button')) return;
      e.stopPropagation();
    };
    /** A reference plane on the face under the click (Studio's Plane on face), selected: the next points go on it. */
    const planeOnFace = async (e: PointerEvent) => {
      setFacePick(false);
      const ray = rayAt(e), s = scene.current;
      if (!ray || !s) return;
      const meshes: THREE.Object3D[] = [];
      s.traverse((o) => { if ((o as THREE.Mesh).isMesh && o.visible && o.userData.nodeId) meshes.push(o); });
      const hit = ray.intersectObjects(meshes, false)[0];
      if (!hit?.face) return;
      const n = hit.face.normal.clone().transformDirection(hit.object.matrixWorld);
      const r = await execute('view.create', { kind: 'plane', point: [hit.point.x, hit.point.y, hit.point.z], normal: [n.x, n.y, n.z], elementId: hit.object.userData.nodeId }) as { id?: string } | null;
      if (r?.id) useUi.setState({ selection: r.id, selected: [r.id] });
    };
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    return () => {
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
    };
  }, [active, frame, camera, canvas, scene, acquire]);

  // The command line in 3D too, as on a plan: what is typed, the prompt.
  if (!command) return <CommandLine />;
  if (!active) {
    return (
      <>
        <div className="axis-drag-chip mode-chip" data-draft3d="none">
          <span>Choose a storey, or select a reference plane, to draft in 3D</span>
        </div>
        <CommandLine />
      </>
    );
  }
  return (
    <>
      <svg className="grip-overlay draft-3d" data-draft3d="overlay">
        <CadOverlay aimed={aimed} toScreen={toScreen} />
      </svg>
      <div className="axis-drag-chip mode-chip" data-draft3d="plane">
        <span>Drafting on</span><span data-no-i18n>{work!.name}</span>
        <span className="mode-hint">{work!.id.startsWith('plan:') || nodes.get(work!.id)?.props.kind === 'plan' ? 'Select a reference plane to draw on it instead' : 'Deselect it to draw on the storey\'s level'}</span>
        <button type="button" className={`toggle${facePick ? ' active' : ''}`} aria-pressed={facePick} data-draft3d="face"
          title="Click a face of the model: a reference plane is made on it, and the shapes go on it" onClick={() => setFacePick((v) => !v)}>Pick a face</button>
      </div>
      <CommandLine />
    </>
  );
}
