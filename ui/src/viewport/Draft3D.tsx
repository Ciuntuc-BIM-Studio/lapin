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
import { X } from 'lucide-react';
import * as THREE from 'three';
import type { Vec2 } from '../platform/module';
import { select, useUi } from '../app/store';
import { pickTarget } from '../platform/groups';
import { hitAt, rayAt as rayAt3 } from './scene3d';
import { activeStorey, execute, useSession } from '../engine/session';
import { useCamera } from './camera';
import { useDrawing } from './drawing';
import { useCadCanvas, snapShapes, type Aimed } from './cad/aim';
import { CadOverlay } from './cad/canvas';
import { CommandLine } from './cad/CommandLine';
import { cancel, feedElement, feedPoint, useCad } from './cad/session';
import { useTagHover } from './tagTarget';
import { nearestOnShape, type Shape } from './cad/shapes';
import { frameOfView, rayOnPlane, toPlane, toWorld, workPlaneView } from './workPlane';
import { axesInPlane, infer, type Inferred } from './sketch/inference';
import { edgesInPlane, guidesInPlane, guidesOf, sceneSegments } from './sketch/edges';
import { clearWorkPlane, finishSketch, lookAtPlane, useWorkPlane } from './sketch/sketch3d';
import { middleOf, useAxisDrag } from './axisDrag';

export function Draft3D({ camera, canvas, scene }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null>; scene: React.RefObject<THREE.Scene | null> }) {
  useCamera();
  const command = useCad((s) => s.command);
  const nodes = useSession((s) => s.nodes);
  const selection = useUi((s) => s.selection);
  const planViewId = useUi((s) => s.planViewId);
  const storeyId = useUi((s) => s.storeyId) ?? activeStorey()?.id ?? null;
  const sketch = useWorkPlane((s) => s.plane);
  const dragging = useAxisDrag((s) => !!s.spec);
  const work = useMemo(() => workPlaneView({ selection, planViewId, storeyId, sketch: sketch?.planeId }, nodes), [selection, planViewId, storeyId, sketch, nodes]);
  const frame = useMemo(() => (work ? frameOfView(work.id, nodes) : null), [work, nodes]);
  const active = !!command && !!frame && !!work;
  const drawing = useDrawing(active ? work!.id : null);
  // The sketch's face, lit and snapped to (its edges as the plane's own lines: the shared core of decision 374).
  const faceLines = useMemo((): Shape[] => {
    if (!frame || !sketch || sketch.planeId !== work?.id) return [];
    const o = sketch.outline ?? [];
    const pts = o.map((q) => toPlane(frame, q));
    // The face's outline, and the other bodies' edges lying in the plane (decision 375).
    const others = [...edgesInPlane(sceneSegments(scene.current, frame), frame).edges, ...guidesInPlane(guidesOf(nodes), frame).edges];
    return [...pts.map((a, i) => ({ type: 'line' as const, a, b: pts[(i + 1) % pts.length] })), ...others.map((e) => ({ type: 'line' as const, a: e.a, b: e.b }))];
  }, [sketch, frame, work, scene, nodes]);
  const [inferred, setInferred] = useState<Inferred | null>(null);
  // The plane gone (undone, another project): the sketch with it.
  useEffect(() => { if (sketch && !nodes.has(sketch.planeId)) clearWorkPlane(); }, [sketch, nodes]);
  const [aimed, setAimed] = useState<Aimed | null>(null);
  const [scale, setScale] = useState(50);
  const [facePick, setFacePick] = useState(false);
  const { drafts } = useMemo(() => snapShapes(drawing), [drawing]);
  const scaleRef = useRef(scale);
  scaleRef.current = scale;
  const { aim, acquire } = useCadCanvas({
    viewId: active ? work!.id : null, drawing, scale, extra: faceLines,
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
  useEffect(() => { if (!command) { setAimed(null); setFacePick(false); setInferred(null); } }, [command]);
  const sketchRef = useRef(sketch);
  sketchRef.current = sketch;
  const faceLinesRef = useRef(faceLines);
  faceLinesRef.current = faceLines;

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
    let hoverFrame = 0;
    const onMove = (e: PointerEvent) => {
      if (e.buttons !== 0 || !inside(e) || facePickRef.current) return;
      const step = useCad.getState().step;
      // The element a step waiting for one would take, lit under the cursor (tagTarget.ts; the 3D view draws it).
      if (step?.hover && step.want === 'select' && !hoverFrame) {
        const [cx, cy] = [e.clientX, e.clientY];
        hoverFrame = requestAnimationFrame(() => {
          hoverFrame = 0;
          const ray = rayAt3({ camera, canvas, scene }, cx, cy);
          const id = (ray ? hitAt(scene.current, ray)?.nodeId : null) ?? null;
          if (useTagHover.getState().element !== id) useTagHover.setState({ element: id });
        });
      }
      const raw = rawAt(e);
      if (!raw) return;
      const a = inferOver(aimRef.current(raw, step?.base ?? null, { orthoHeld: e.shiftKey }), raw, step?.base ?? null);
      setAimed(a);
      useCad.setState({ cursor: a.point, aim: a });
    };
    /**
     * Where the CAD aim found nothing (no object snap, no guide, no ortho), the 3D sketch's inference: the model's axes
     * in the plane from the base (red, green, blue), and the edges' ways (magenta), as the direct tools infer them.
     */
    const inferOver = (a: Aimed, raw: Vec2, base: Vec2 | null): Aimed => {
      if (a.kind === 'point' || a.lock) { setInferred(null); return a; }
      const edges = faceLinesRef.current.map((l) => (l.type === 'line' ? { a: l.a, b: l.b } : null)).filter((x): x is { a: Vec2; b: Vec2 } => !!x);
      const i = infer({ raw, base, edges, axes: axesInPlane(frame.right, frame.up), tol: 10 / scaleRef.current });
      if (i.kind === 'On face') { setInferred(null); return a; }
      setInferred(i);
      return { ...a, point: i.point, kind: 'point' };
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .cad-line, input, button')) return;
      e.stopPropagation(); e.preventDefault();
      if (facePickRef.current) { void planeOnFace(e); return; }
      const step = useCad.getState().step;
      // Selecting for a command typed in 3D (M, CO, RO, MI with nothing selected; decision 399): the body under the click,
      // as a click selects it in 3D, added to the selection; Enter goes on. What lies on the plane only (a draft) as before.
      if (step?.want === 'select' && !sketchRef.current) {
        const ray = rayAt3({ camera, canvas, scene }, e.clientX, e.clientY);
        const hit = ray ? hitAt(scene.current, ray) : null;
        // A step that takes elements (Match properties' source and targets, decision 490): the element itself, not the selection.
        if (step.element && hit?.nodeId) { feedElement(hit.nodeId); return; }
        const id = pickTarget(hit?.nodeId);
        if (id) { if (!useUi.getState().selected.includes(id)) select(id, true); return; }
      }
      const raw = rawAt(e);
      if (!step || !raw) return;
      const a = inferOver(aimRef.current(raw, step.base ?? null, { orthoHeld: e.shiftKey }), raw, step.base ?? null);
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
      if (hoverFrame) cancelAnimationFrame(hoverFrame);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
    };
  }, [active, frame, camera, canvas, scene, acquire]);

  // The sketch's work plane with no command running: Enter finishes it, Esc lets it go (the lines stay).
  useEffect(() => {
    if (!sketch || command || dragging) return;
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement | null)?.closest?.('input, textarea, select, [contenteditable]')) return;
      if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); void finish(); }
      else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); clearWorkPlane(); }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  });
  const finish = () => finishSketch(frame, sketch?.pick ? null : storeyId, () => cancel(true));
  const lookAt = () => { if (frame) lookAtPlane(frame.normal, sketch?.outline?.length ? middleOf(sketch.outline) : frame.origin); };
  const sketchFace = (() => {
    const o = sketch?.planeId === work?.id ? sketch?.outline : undefined;
    if (!o || dragging) return null;
    const s = o.map((q) => project(new THREE.Vector3(q.x, q.y, q.z)));
    return s.some((p) => !p) ? null : s.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('') + 'Z';
  })();
  const sketchChip = sketch && !dragging ? (
    <div className="axis-drag-chip mode-chip sketch-chip" data-draft3d="sketch">
      <span>Sketching on</span><span data-no-i18n>{sketch.name}</span>
      {!command && <span className="mode-hint">Draw with Line, Rectangle, Circle or Arc (or the command line); Enter finishes, Esc leaves</span>}
      <button type="button" className="toggle" data-draft3d="finish" title="Finish sketch: the closed shape drawn gets the push/pull arrow (Enter)" onClick={() => void finish()}>Finish sketch</button>
      <button type="button" className="toggle" data-draft3d="look" title="Look at plane: the view square to it" onClick={lookAt}>Look at plane</button>
      <button type="button" className="mode-close" title="Leave the sketch (Esc): what is drawn stays" aria-label="Leave the sketch" onClick={() => { cancel(true); clearWorkPlane(); }}><X size={12} /></button>
    </div>
  ) : null;
  const facePathEl = sketchFace ? <svg className="grip-overlay face-sketch" data-draft3d="face"><path d={sketchFace} className="face-sketch-face" /></svg> : null;
  const inferPt = inferred && frame ? toScreen(inferred.point) : null;
  const inferGuide = inferred?.guide && frame ? [toScreen(inferred.guide.from), toScreen(inferred.guide.to)] : null;
  const inferEl = inferred && inferPt ? (
    <>
      <svg className="grip-overlay face-sketch">
        {inferGuide && <line x1={inferGuide[0].x} y1={inferGuide[0].y} x2={inferGuide[1].x} y2={inferGuide[1].y} className={`sketch-guide ${inferred.guide!.color}`} />}
      </svg>
      <div className={`face-sketch-snap-name ${inferred.color ?? ''}`} style={{ left: inferPt.x + 10, top: inferPt.y + 8 }}>{inferred.kind}</div>
    </>
  ) : null;

  // The command line in 3D too, as on a plan: what is typed, the prompt.
  if (!command) return <>{facePathEl}{sketchChip}<CommandLine floating /></>;
  if (!active) {
    return (
      <>
        <div className="axis-drag-chip mode-chip" data-draft3d="none">
          <span>Choose a storey, or select a reference plane, to draft in 3D</span>
        </div>
        <CommandLine floating />
      </>
    );
  }
  return (
    <>
      {facePathEl}
      <svg className="grip-overlay draft-3d" data-draft3d="overlay">
        <CadOverlay aimed={aimed} toScreen={toScreen} />
      </svg>
      {inferEl}
      {sketchChip ?? <div className="axis-drag-chip mode-chip" data-draft3d="plane">
        <span>Drafting on</span><span data-no-i18n>{work!.name}</span>
        <span className="mode-hint">{work!.id.startsWith('plan:') || nodes.get(work!.id)?.props.kind === 'plan' ? 'Select a reference plane to draw on it instead' : 'Deselect it to draw on the storey\'s level'}</span>
        <button type="button" className={`toggle${facePick ? ' active' : ''}`} aria-pressed={facePick} data-draft3d="face"
          title="Click a face of the model: a reference plane is made on it, and the shapes go on it" onClick={() => setFacePick((v) => !v)}>Pick a face</button>
      </div>}
      <CommandLine floating />
    </>
  );
}
