/**
 * The direct way of sketching in 3D, SketchUp's (decision 374; sketch/direct.ts
 * holds the state, sketch/inference.ts what the cursor infers): Studio's Line,
 * Rectangle and Circle draw right on the face under the cursor, lit, or on the
 * storey's level in empty space; the first click locks the plane. The cursor
 * infers endpoints, midpoints, edges, the model's axes (red, green, blue) and
 * edges' ways (magenta); → ← ↑ lock an axis, ↓ the edge's way, Shift holds what
 * is inferred. A value is typed at any time (1200, 1200,600, r300) and applied
 * by Enter. A closed shape gets the push/pull arrow at once (Esc keeps it a
 * sketch); a line left open (Enter, Esc) stays a sketch on the face.
 */

import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { X } from 'lucide-react';
import type { Vec2 } from '../platform/module';
import { notify, useUi } from '../app/store';
import { activeStorey, execute, query, useSession } from '../engine/session';
import { useCamera, type Vec3 } from './camera';
import { useAxisDrag } from './axisDrag';
import { frameOf } from './faceSketch';
import { formatLength, parseLength } from './liveInput';
import { levelFrame, rayOnPlane, toPlane, toWorld, workPlaneView, type WorkFrame } from './workPlane';
import { startCommand, useCad } from './cad/session';
import { useDisplay } from '../app/display';
import { activate, findTool } from '../platform/modules';
import { arcThrough, toJson, type Shape } from './cad/shapes';
import { arcPts, click, insideDistance, tapeOutcome, endDirect, finishOpen, offsetOutcome, preview, resetDirect, typed, useDirect, type DirectPlane, type Outcome } from './sketch/direct';
import { edgesInPlane, guidesInPlane, guidesOf, sceneSegments } from './sketch/edges';
import { arrowLock, axesInPlane, holdLock, infer, segsOf, type Seg2 } from './sketch/inference';
import { parseVcb } from './sketch/vcb';
import { pushPullShape, type FacePick } from './sketch/sketch3d';

type Pt = { x: number; y: number };

/** SketchUp's one-letter shortcuts (View → the Studio tab's SketchUp shortcuts): the tool each starts in the 3D view. */
export const SKETCHUP_KEYS: Record<string, string> = {
  L: 'studio.sketch.line', R: 'studio.sketch.rect', C: 'studio.sketch.circle', A: 'studio.sketch.arc', P: 'studio.pushPull',
  F: 'studio.sketch.offset', T: 'studio.sketch.tape', M: 'studio.move3d', E: 'cad:ERASE', ' ': 'select',
};
const SNAP_PX = 10;
const TOOL_NAME = { line: 'Line', rect: 'Rectangle', circle: 'Circle', arc: '2-point arc', offset: 'Offset', tape: 'Tape measure' } as const;
const HINT = {
  line: 'Click the points · back on the first closes it · Enter or Esc leaves it open',
  rect: 'Click the opposite corner, or type width,height',
  circle: 'Click the radius, or type it (300 or r300)',
  arc: 'Click the start, the end, then the bulge (or type the chord, then the bulge)',
  offset: 'Click a face (or a region of it), then the offset, or type it',
  tape: 'From an edge: a guide line parallel to it · from a point: the distance and a guide point · or type it',
} as const;

/**
 * The flat faces' outlines the engine gave, by element and plane: the face, or each region of it that sketched lines
 * split off (decision 375), asked again when the cursor is in none known yet; a face with no flat face there is `flatless`.
 */
const faces = new Map<string, Vec3[][]>();
const asking = new Set<string>();
const flatless = new Set<string>();
/** The known outline of a face holding a point. */
const outlineAt = (key: string, normal: Vec3, at: Vec3): Vec3[] | undefined =>
  faces.get(key)?.find((o) => { const f = frameOf(normal, o); return insideDistance(o.map((q) => toPlane(f, q)), toPlane(f, at)) >= -1e-6; });
const faceKey = (id: string, n: Vec3, p: Vec3) => `${id}|${n.x.toFixed(3)},${n.y.toFixed(3)},${n.z.toFixed(3)}|${(n.x * p.x + n.y * p.y + n.z * p.z).toFixed(3)}`;

export function Sketch3D({ camera, canvas, scene }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null>; scene: React.RefObject<THREE.Scene | null> }) {
  useCamera();
  const st = useDirect();
  const dragging = useAxisDrag((s) => !!s.spec);
  const command = useCad((s) => s.command);
  const tool = useUi((s) => s.tool);
  const nodes = useSession((s) => s.nodes);
  const input = useRef<HTMLInputElement>(null);
  const [, bump] = useState(0);

  // Another tool, or a command of the command line, ends it.
  useEffect(() => { if (st.tool && tool) endDirect(); }, [tool, st.tool]);
  useEffect(() => { if (st.tool && command) endDirect(); }, [command, st.tool]);
  useEffect(() => () => endDirect(), []);
  useEffect(() => { faces.clear(); asking.clear(); flatless.clear(); }, [nodes]);
  // SketchUp's one-letter shortcuts in the 3D view, when switched on (decision 376).
  const sketchupKeys = useDisplay((d) => d.sketchupKeys);
  useEffect(() => {
    if (!sketchupKeys) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey || e.repeat) return;
      if ((e.target as HTMLElement | null)?.closest?.('input, textarea, select, [contenteditable]')) return;
      const cad = useCad.getState();
      if (cad.command || cad.buffer) return;
      const id = SKETCHUP_KEYS[e.key === ' ' ? ' ' : e.key.toUpperCase()];
      if (!id) return;
      e.preventDefault(); e.stopImmediatePropagation();
      if (id === 'select') { endDirect(); useUi.setState({ tool: null }); return; }
      if (id.startsWith('cad:')) { endDirect(); void startCommand(id.slice(4)); return; }
      const t = findTool(id);
      if (t) activate(t);
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [sketchupKeys]);

  const rectOf = () => canvas.current?.getBoundingClientRect() ?? null;
  const toScreen = (p: Vec3): Pt | null => {
    const c = camera.current, r = rectOf();
    if (!c || !r) return null;
    c.updateMatrixWorld();
    const v = new THREE.Vector3(p.x, p.y, p.z).project(c);
    if (v.z > 1 || v.z < -1) return null;
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };

  useEffect(() => {
    if (!st.tool || dragging) return;
    const inside = (e: PointerEvent) => {
      const r = rectOf();
      return !!r && e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    };
    const rayAt = (e: { clientX: number; clientY: number }): THREE.Raycaster | null => {
      const c = camera.current, r = rectOf();
      if (!c || !r) return null;
      c.updateMatrixWorld();
      const ray = new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
      return ray;
    };
    /** The storey's level, as the plane in empty space. */
    const levelPlane = (): DirectPlane => {
      const storey = activeStorey();
      const ui = useUi.getState();
      const work = workPlaneView({ selection: null, planViewId: ui.planViewId, storeyId: ui.storeyId ?? storey?.id ?? null }, useSession.getState().nodes);
      return { frame: levelFrame(Number(storey?.props.elevation ?? 0) || 0), storeyId: storey?.id ?? null, name: work?.name ?? 'Level' };
    };
    /** The face under the cursor (its plane, lit once the engine gives its outline), else the level. */
    const planeUnder = (ray: THREE.Raycaster): DirectPlane => {
      const s = scene.current;
      const meshes: THREE.Object3D[] = [];
      s?.traverse((o) => { if ((o as THREE.Mesh).isMesh && o.visible && o.userData.nodeId && !(o as THREE.InstancedMesh).isInstancedMesh) meshes.push(o); });
      const hit = ray.intersectObjects(meshes, false)[0];
      if (!hit?.face) return levelPlane();
      const n3 = hit.face.normal.clone().transformDirection(hit.object.matrixWorld);
      const normal = { x: n3.x, y: n3.y, z: n3.z }, at = { x: hit.point.x, y: hit.point.y, z: hit.point.z };
      const id = String(hit.object.userData.nodeId);
      const pick: FacePick = { id, point: [at.x, at.y, at.z], normal: [normal.x, normal.y, normal.z] };
      const key = faceKey(id, normal, at);
      const outline = outlineAt(key, normal, at);
      if (!outline && !asking.has(key) && !flatless.has(key)) {
        asking.add(key);
        void query<{ normal: number[]; outline: number[][] }>('studio.faceAt', { ...pick })
          .then((f) => {
            if (f && f.outline.length >= 3) faces.set(key, [...(faces.get(key) ?? []), f.outline.map((q) => ({ x: q[0], y: q[1], z: q[2] }))]);
            else flatless.add(key);
            bump((k) => k + 1);
          })
          .catch(() => flatless.add(key))
          .finally(() => asking.delete(key));
      }
      const name = useSession.getState().nodes.get(id)?.name ?? 'Face';
      const f = frameOf(normal, outline ?? []);
      // Origin on the face's plane through the point hit (the outline's first corner when known).
      const frame: WorkFrame = outline ? f : { ...f, origin: at };
      return { frame, pick, outline, key, storeyId: useSession.getState().nodes.get(id)?.storeyId ?? null, name };
    };
    /** Pixels per metre on the plane at a point (the snaps' reach). */
    const ppm = (frame: WorkFrame, p: Vec2) => {
      const a = toScreen(toWorld(frame, p)), b = toScreen(toWorld(frame, { x: p.x + 1, y: p.y }));
      const c = toScreen(toWorld(frame, { x: p.x, y: p.y + 1 }));
      if (!a || !b || !c) return 50;
      return Math.max(1e-3, Math.max(Math.hypot(b.x - a.x, b.y - a.y), Math.hypot(c.x - a.x, c.y - a.y)));
    };
    /** Other bodies' edges in the locked plane (and the points where edges pass through it), found once per plane. */
    let near: { frame: WorkFrame; edges: Seg2[]; points: Vec2[] } | null = null;
    const others = (plane: DirectPlane) => {
      if (!near || near.frame !== plane.frame) {
        const bodies = edgesInPlane(sceneSegments(scene.current, plane.frame), plane.frame);
        const guides = guidesInPlane(guidesOf(useSession.getState().nodes), plane.frame);
        near = { frame: plane.frame, edges: [...bodies.edges, ...guides.edges], points: [...bodies.points, ...guides.points] };
      }
      return near;
    };
    const edgesOf = (plane: DirectPlane, pts: Vec2[], locked: boolean): Seg2[] => {
      const face = plane.outline ? segsOf(plane.outline.map((q) => toPlane(plane.frame, q)), true) : [];
      return [...face, ...segsOf(pts, false), ...(locked ? others(plane).edges : [])];
    };
    const aimAt = (e: PointerEvent | { clientX: number; clientY: number; shiftKey: boolean }) => {
      const ray = rayAt(e);
      if (!ray) return;
      const s0 = useDirect.getState();
      let plane = s0.locked && s0.plane ? s0.plane : planeUnder(ray);
      // Locked before the engine gave the face's outline: lit (and snapped to) once it has.
      if (s0.locked && plane.key && !plane.outline && plane.pick) {
        const pp = plane.pick.point, nn = plane.pick.normal;
        const o = outlineAt(plane.key, { x: nn[0], y: nn[1], z: nn[2] }, { x: pp[0], y: pp[1], z: pp[2] });
        if (o) plane = { ...plane, outline: o };
      }
      const raw = rayOnPlane(plane.frame, ray.ray.origin, ray.ray.direction);
      if (!raw) { useDirect.setState({ plane, inferred: null }); return; }
      const base = s0.pts.length ? s0.pts[s0.pts.length - 1] : null;
      const inferred = infer({
        raw, base, edges: edgesOf(plane, s0.pts, s0.locked), points: [...s0.pts.slice(0, 1), ...(s0.locked ? others(plane).points : [])],
        axes: axesInPlane(plane.frame.right, plane.frame.up), lock: s0.lock, tol: SNAP_PX / ppm(plane.frame, raw),
      });
      useDirect.setState({ plane, inferred });
    };
    const onMove = (e: PointerEvent) => {
      if (e.buttons !== 0 || !inside(e)) return;
      aimAt(e);
      e.stopPropagation();
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .axis-drag-value, input, button')) return;
      e.stopPropagation(); e.preventDefault();
      aimAt(e);
      const s0 = useDirect.getState();
      if (!s0.tool || !s0.inferred || !s0.plane) return;
      if (s0.tool === 'tape') {
        // The first click: on an edge a guide line parallel to it follows, elsewhere a measure from the point.
        if (!s0.pts.length) {
          const e2 = s0.inferred.kind === 'On edge' ? s0.inferred.edge ?? null : null;
          useDirect.setState({ pts: [s0.inferred.point], tapeEdge: e2, locked: true, text: '' });
          return;
        }
        apply(tapeOutcome(s0.pts[0], s0.tapeEdge, s0.inferred.point, null));
        return;
      }
      if (s0.tool === 'offset') {
        // The first click takes the face (or its region) under it; the second places the offset.
        if (!s0.loop) {
          const o = s0.plane.outline;
          if (!s0.plane.pick || !o) { notify('Offset works on a face: click one.'); return; }
          useDirect.setState({ loop: o.map((q) => toPlane(s0.plane!.frame, q)), locked: true });
          return;
        }
        apply(offsetOutcome(s0.loop, s0.inferred.point, null));
        return;
      }
      apply(click(s0.tool, s0.pts, s0.inferred.point));
    };
    const onUp = (e: PointerEvent) => { if (inside(e) && e.button === 0) e.stopPropagation(); };
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement | null;
      const s0 = useDirect.getState();
      if (!s0.tool) return;
      if (e.key.startsWith('Arrow')) {
        const plane = s0.plane;
        if (!plane) return;
        const l = arrowLock(e.key, s0.lock, axesInPlane(plane.frame.right, plane.frame.up), s0.inferred?.edge ?? null);
        e.preventDefault(); e.stopPropagation();
        if (l !== undefined) useDirect.setState({ lock: l, shiftLock: false });
        return;
      }
      if (e.key === 'Shift' && !s0.lock && s0.inferred) { const l = holdLock(s0.inferred); if (l) useDirect.setState({ lock: l, shiftLock: true }); return; }
      if (el !== input.current && el?.closest?.('input, textarea, select, [contenteditable]')) return;
      if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); enter(); }
      else if (e.key === 'Escape') {
        e.preventDefault(); e.stopPropagation();
        if (!s0.pts.length && !s0.loop) { endDirect(); return; }
        if (s0.loop) { resetDirect(); return; }
        apply(finishOpen(s0.tool, s0.pts));
      } else if (e.key === 'Backspace' && !s0.text) {
        e.preventDefault(); e.stopPropagation();
        if (s0.pts.length <= 1) resetDirect(); else useDirect.setState({ pts: s0.pts.slice(0, -1) });
      } else if (el !== input.current && /^[0-9.,+\-'"<r ]$/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey && (s0.pts.length || s0.loop)) input.current?.focus();
    };
    const onKeyUp = (e: KeyboardEvent) => { if (e.key === 'Shift' && useDirect.getState().shiftLock) useDirect.setState({ lock: null, shiftLock: false }); };
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    window.addEventListener('keyup', onKeyUp, true);
    return () => {
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
      window.removeEventListener('keydown', onKey, true);
      window.removeEventListener('keyup', onKeyUp, true);
    };
  }, [st.tool, dragging, camera, canvas, scene]);

  if (!st.tool || dragging) return null;
  const plane = st.plane;
  const f = plane?.frame;
  const cursor = st.inferred?.point ?? null;
  const vcb = parseVcb(st.text, (s) => parseLength(s));
  const tape = st.tool === 'tape' && st.pts.length ? tapeOutcome(st.pts[0], st.tapeEdge, cursor, vcb?.kind === 'length' ? vcb.value : null) : null;
  const typedShape = st.tool === 'tape' ? null : st.tool === 'offset' ? (st.loop ? offsetOutcome(st.loop, cursor, vcb?.kind === 'length' ? vcb.value : null) : null)
    : vcb && st.pts.length ? typed(st.tool, st.pts, vcb, cursor) : null;
  const shown = typedShape?.kind === 'closed' ? { pts: typedShape.outline, closed: true }
    : typedShape?.kind === 'more' ? { pts: typedShape.pts, closed: false }
      : typedShape?.kind === 'arc' ? { pts: arcPts(typedShape.start, typedShape.through, typedShape.end), closed: false }
        : tape?.kind === 'guideLine' ? { pts: [tape.a, tape.b], closed: false }
          : tape?.kind === 'guidePoint' ? { pts: [tape.from, tape.at], closed: false }
            : st.tool === 'offset' || st.tool === 'tape' ? { pts: [], closed: false } : preview(st.tool, st.pts, cursor);
  const w = (p: Vec2) => (f ? toWorld(f, p) : { x: 0, y: 0, z: 0 });
  const pathOf = (list: Vec3[], close: boolean) => {
    const s = list.map(toScreen);
    if (s.length < 2 || s.some((p) => !p)) return null;
    return s.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('') + (close ? 'Z' : '');
  };
  const facePath = plane?.outline ? pathOf(plane.outline, true) : null;
  const shapePath = f ? pathOf(shown.pts.map(w), shown.closed) : null;
  const cur = f && cursor ? toScreen(w(cursor)) : null;
  const g = st.inferred?.guide;
  const ga = f && g ? toScreen(w(g.from)) : null, gb = f && g ? toScreen(w(g.to)) : null;
  const last = st.pts[st.pts.length - 1];
  let label = '';
  if (cursor && st.pts.length) {
    if (tape && (tape.kind === 'guideLine' || tape.kind === 'guidePoint')) label = formatLength(tape.distance);
    else if (st.tool === 'rect') label = `${formatLength(Math.abs(cursor.x - st.pts[0].x))}, ${formatLength(Math.abs(cursor.y - st.pts[0].y))}`;
    else label = formatLength(Math.hypot(cursor.x - last.x, cursor.y - last.y));
  }
  const kind = st.inferred?.kind;
  const what = kind === 'On face' ? (plane?.pick ? 'On face' : 'On level') : kind;
  const color = st.inferred?.color ?? '';
  const markClass = kind === 'Endpoint' ? 'sketch-end' : kind === 'Midpoint' ? 'sketch-mid' : kind === 'On edge' || kind === 'Intersection' ? 'sketch-edge' : 'face-sketch-cursor';

  return (
    <>
      <svg className="grip-overlay axis-drag face-sketch" data-sketch3d="overlay">
        {facePath && <path d={facePath} className="face-sketch-face" />}
        {ga && gb && <line x1={ga.x} y1={ga.y} x2={gb.x} y2={gb.y} className={`sketch-guide ${g!.color}`} />}
        {shapePath && <path d={shapePath} className={shown.closed ? 'face-sketch-shape' : 'face-sketch-line'} />}
        {st.pts.map((p, i) => { const s = toScreen(w(p)); return s && <circle key={i} cx={s.x} cy={s.y} r={3.5} className="axis-origin" />; })}
        {cur && <circle cx={cur.x} cy={cur.y} r={kind && kind !== 'On face' ? 5 : 3} className={markClass} />}
      </svg>
      {cur && what && <div className={`face-sketch-snap-name ${color}`} style={{ left: cur.x + 10, top: cur.y + 8 }} data-sketch3d="inference">{what}</div>}
      {cur && (st.pts.length > 0 || !!st.loop) && (
        <div className="axis-drag-value" style={{ left: cur.x + 18, top: cur.y - 30 }}>
          <input ref={input} value={st.text} placeholder={label || '…'} aria-label="Measurements" spellCheck={false} data-sketch3d="vcb"
            onChange={(e) => useDirect.setState({ text: e.target.value })}
            onKeyDown={(e) => {
              if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); enter(); }
              else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); e.nativeEvent.stopImmediatePropagation(); useDirect.setState({ text: '' }); input.current?.blur(); }
            }} />
        </div>
      )}
      <div className="axis-drag-chip mode-chip" data-sketch3d="chip">
        <span>{TOOL_NAME[st.tool]}</span>
        {plane && <span data-no-i18n>{plane.name}</span>}
        <span className="mode-hint">{HINT[st.tool]}</span>
        <span className="mode-hint">{st.lock ? 'Locked: the arrow again or Shift lets go' : '→ red · ← green · ↑ blue · ↓ along the edge · Shift holds'}</span>
        <button type="button" className="mode-close" title="Done (Esc)" aria-label="Done" onClick={endDirect}><X size={12} /></button>
      </div>
    </>
  );

  function enter() {
    const s0 = useDirect.getState();
    if (!s0.tool) return;
    const v = parseVcb(s0.text, (s) => parseLength(s));
    if (s0.tool === 'tape') { if (s0.pts.length && v?.kind === 'length') apply(tapeOutcome(s0.pts[0], s0.tapeEdge, s0.inferred?.point ?? null, v.value)); return; }
    if (s0.tool === 'offset') { if (s0.loop && v?.kind === 'length') apply(offsetOutcome(s0.loop, s0.inferred?.point ?? null, v.value)); return; }
    if (v && s0.pts.length) { apply(typed(s0.tool, s0.pts, v, s0.inferred?.point ?? null)); return; }
    if (!s0.text.trim()) apply(finishOpen(s0.tool, s0.pts));
  }

  /** What a click, a typed value or Enter made: the next point (the plane locked), or the shape stored. */
  function apply(o: Outcome) {
    const s0 = useDirect.getState();
    const p = s0.plane;
    if (o.kind === 'none') { if (!s0.pts.length || s0.tool !== 'line') return; resetDirect(); return; }
    if (o.kind === 'more') { useDirect.setState({ pts: o.pts, locked: true, text: '', lock: s0.shiftLock ? s0.lock : null }); return; }
    resetDirect();
    if (!p) return;
    void store(p, o);
  }
}

/** A shape done on the plane: a closed one pushed or pulled at once (kept a sketch by Esc), an open line kept a sketch. */
async function store(p: DirectPlane, o: Exclude<Outcome, { kind: 'none' } | { kind: 'more' }>): Promise<void> {
  const f = p.frame;
  const w = (q: Vec2) => toWorld(f, q);
  if (o.kind === 'guideLine' || o.kind === 'guidePoint') {
    const p3 = (q: Vec2) => { const v = w(q); return [v.x, v.y, v.z]; };
    const r = await execute('studio.addGuide', o.kind === 'guideLine' ? { a: p3(o.a), b: p3(o.b) } : { a: p3(o.at) });
    if (r) notify(o.kind === 'guideLine' ? `Guide line at ${formatLength(o.distance)}.` : `Distance: ${formatLength(o.distance)}.`);
    return;
  }
  if (o.kind === 'arc') {
    const p3 = (q: Vec2) => { const v = w(q); return [v.x, v.y, v.z]; };
    if (p.pick) { if (await execute('studio.onFace', { ...p.pick, arc: { start: p3(o.start), through: p3(o.through), end: p3(o.end) } })) notify('The arc stays on the face as a sketch: with lines it can close a region to push or pull.'); return; }
    const a = arcThrough(o.start, o.through, o.end);
    if (a) await drawOnLevel(p, a);
    return;
  }
  if (o.kind === 'open') {
    const outline = o.pts.map(w);
    if (p.pick) { if (await execute('studio.onFace', { ...p.pick, open: true, outline: outline.map((q) => [q.x, q.y, q.z]) })) notify('The line stays on the face as a sketch.'); return; }
    await drawOnLevel(p, o.pts.length === 2 ? { type: 'line', a: o.pts[0], b: o.pts[1] } : { type: 'polyline', pts: o.pts, closed: false });
    return;
  }
  const outline = o.outline.map(w);
  if (p.pick) {
    pushPullShape({ outline, normal: f.normal, pick: p.pick, stored: o.circle ? { circle: { center: w(o.circle.c), radius: o.circle.r } } : { outline } });
    return;
  }
  const id = await drawOnLevel(p, o.circle ? { type: 'circle', c: o.circle.c, r: o.circle.r } : { type: 'polyline', pts: o.outline, closed: true });
  if (id) pushPullShape({ outline, normal: f.normal, storeyId: p.storeyId, stored: { sketchId: id } });
}

/** A shape drafted on the storey's level (its plan's drafts): the id made. */
async function drawOnLevel(p: DirectPlane, shape: Shape): Promise<string | null> {
  const ui = useUi.getState();
  const work = workPlaneView({ selection: null, planViewId: ui.planViewId, storeyId: p.storeyId ?? ui.storeyId ?? activeStorey()?.id ?? null }, useSession.getState().nodes);
  if (!work) { notify('Choose a storey to sketch on its level.'); return null; }
  const r = await execute('drafting.draw', { viewId: work.id, shape: toJson(shape) }) as { ids?: string[] } | null;
  return r?.ids?.[0] ?? null;
}
