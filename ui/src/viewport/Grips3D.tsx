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
 *
 * Besides the plan's grips, the 3D-only ones the modules give (Grip3D, decision 610): a wall's top and faces, a floor's
 * top, a roof's eave, each dragged along its own line (up, or across a wall), on a plane through it facing the camera.
 * The other elements' corners, middles and edges give the point it goes to (for a face, their faces too): their height,
 * or their place across; else the point of the line nearest the cursor, to the centimetre. The distance can be typed
 * (Enter takes it); letting go is one engine command, one undo step.
 */

import { useRef, useState } from 'react';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { num } from '../engine/model';
import { useUi } from '../app/store';
import { findTool, grips3dOf } from '../platform/modules';
import type { Grip3D, Vec2 } from '../platform/module';
import { runSteps } from './grips';
import { lineNearRay, type Ray3 } from './snap3d';
import { formatLength, liveBegin, liveCursor, liveEnd, livePoint } from './liveInput';
import { t } from '../i18n';
import { useGripEditor } from './GripLayer';
import { useCamera } from './camera';
import { hitAt, projector, rayAt, snapAt } from './scene3d';
import { gripTarget, type Snapped3 } from './snap3d';
import { LiveBox } from './LiveBox';
import { useLive } from './liveInput';

/** The inferences a 3D grip takes: a height from what is near; across a wall, a face too. */
const HEIGHT_TAKES = new Set(['Endpoint', 'Midpoint', 'On edge']);
const FACE_TAKES = new Set(['Endpoint', 'Midpoint', 'On edge', 'On face']);

/** How far along a 3D grip's line the cursor takes it: the line's point nearest the cursor's ray (null: they run together). */
export function alongGrip(g: Pick<Grip3D, 'at' | 'dir'>, ray: Ray3): number | null { return lineNearRay(g.at, g.dir, ray); }

/** How far along a 3D grip's line a model point lies: what a snapped corner or face gives. */
export const alongOf = (g: Pick<Grip3D, 'at' | 'dir'>, p: { x: number; y: number; z: number }): number =>
  (p.x - g.at.x) * g.dir.x + (p.y - g.at.y) * g.dir.y + (p.z - g.at.z) * g.dir.z;

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
  const selected = useUi((s) => s.selected);
  // The 3D-only grips (decision 610), the one dragged and how far, the one under the cursor.
  const [drag3, setDrag3] = useState<{ grip: Grip3D; by: number } | null>(null);
  const [hot3, setHot3] = useState<string | null>(null);
  const key3 = (g: Grip3D) => `${g.owner}:${g.key}`;
  // A face's grip only on a face turned to the camera (the other is behind the element).
  const facing = (g: Grip3D) => {
    const c = camera.current;
    if (g.kind !== 'face' || !c) return true;
    return (c.position.x - g.at.x) * g.dir.x + (c.position.y - g.at.y) * g.dir.y + (c.position.z - g.at.z) * g.dir.z > 0;
  };
  const grips3 = tool?.gesture ? [] : selected.flatMap((id) => { const n = nodes.get(id); return n?.editable ? grips3dOf(n) : []; }).filter((g) => facing(g) || key3(g) === (drag3 ? key3(drag3.grip) : null));
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
  const view = { camera, canvas, scene };
  const local = (e: { clientX: number; clientY: number }) => { const { left, top } = size(); return { x: e.clientX - left, y: e.clientY - top }; };
  /** The 3D grip under the cursor (within 8 px). */
  const hit3 = (e: { clientX: number; clientY: number }): Grip3D | null => {
    const p = local(e);
    let best: Grip3D | null = null, bd = 8;
    for (const g of grips3) {
      const q = project(g.at);
      const d = q ? Math.hypot(q.x - p.x, q.y - p.y) : Infinity;
      if (d < bd) { bd = d; best = g; }
    }
    return best;
  };
  /** How far the cursor takes a 3D grip: onto what it infers near (a height, a place across), else along its line to the centimetre. */
  const by3 = (g: Grip3D, e: { clientX: number; clientY: number }): number => {
    const snapped = snapAt(view, e.clientX, e.clientY, { level: g.at.z, base: g.at, axes: [], skip: new Set(selected) });
    const takes = g.kind === 'height' ? HEIGHT_TAKES : FACE_TAKES;
    const ray = rayAt(view, e.clientX, e.clientY);
    // A face is taken only when it is parallel to the one dragged (another wall's face, not the floor under the cursor).
    const parallel = () => { const h = ray ? hitAt(scene.current, ray, new Set(selected)) : null; return !!h?.normal && Math.abs(h.normal.x * g.dir.x + h.normal.y * g.dir.y + h.normal.z * g.dir.z) > 0.9; };
    if (snapped && takes.has(snapped.kind) && (snapped.kind !== 'On face' || parallel())) { setSnap(snapped); return alongOf(g, snapped.point); }
    setSnap(null);
    const t0 = ray ? alongGrip(g, ray) : null;
    return t0 == null ? 0 : Math.round(t0 * 100) / 100;
  };
  // Let go where it was taken: a click, which some grips answer (a terrain point made a control).
  const drop3 = (g: Grip3D, by: number) => { if (Math.abs(by) > 1e-6) void runSteps([g.step(by)]); else if (g.click) void runSteps([g.click()]); };
  const down3 = (e: React.PointerEvent): boolean => {
    if (e.button !== 0) return false;
    const g = hit3(e);
    if (!g) return false;
    (e.target as Element).setPointerCapture?.(e.pointerId);
    setDrag3({ grip: g, by: 0 });
    // The distance typed: a length along the grip's line (its sign the cursor's side), Enter takes it.
    liveBegin('grip3d', { x: 0, y: 0 }, (p) => { drop3(g, p.x); setDrag3(null); setSnap(null); liveEnd('grip3d'); });
    liveCursor({ x: 0, y: 0 });
    return true;
  };
  const move3 = (e: React.PointerEvent): boolean => {
    if (!drag3) {
      const g = grips3.length ? hit3(e) : null;
      if ((g ? key3(g) : null) !== hot3) setHot3(g ? key3(g) : null);
      return !!g;
    }
    const raw = by3(drag3.grip, e);
    liveCursor({ x: raw, y: 0 });
    const typed = livePoint({ ...useLive.getState(), cursor: { x: raw, y: 0 } });
    setDrag3({ ...drag3, by: typed ? typed.x : raw });
    return true;
  };
  const up3 = (): boolean => {
    if (!drag3) return false;
    drop3(drag3.grip, drag3.by);
    setDrag3(null);
    setSnap(null);
    liveEnd('grip3d');
    return true;
  };
  /** A 3D grip: a disc on a short stem along its line (both ways), its arrowheads pointing the ways it moves. */
  const shape3 = (g: Grip3D) => {
    const on = drag3 && key3(drag3.grip) === key3(g);
    const at = on ? { x: g.at.x + g.dir.x * drag3!.by, y: g.at.y + g.dir.y * drag3!.by, z: g.at.z + g.dir.z * drag3!.by } : g.at;
    const q = project(at), r = project({ x: at.x + g.dir.x * 0.01, y: at.y + g.dir.y * 0.01, z: at.z + g.dir.z * 0.01 });
    if (!q) return null;
    const l = r ? Math.hypot(r.x - q.x, r.y - q.y) : 0;
    const [ux, uy] = r && l > 1e-6 ? [(r.x - q.x) / l, (r.y - q.y) / l] : [0, -1];
    const [nx, ny] = [-uy, ux];
    const head = (k: number) => `M${q.x + ux * 15 * k} ${q.y + uy * 15 * k}L${q.x + ux * 9 * k + nx * 4} ${q.y + uy * 9 * k + ny * 4}L${q.x + ux * 9 * k - nx * 4} ${q.y + uy * 9 * k - ny * 4}Z`;
    const cls = `grip3d ${g.kind}${g.marked ? ' marked' : ''}${on ? ' on' : hot3 === key3(g) ? ' hot' : ''}`;
    return (
      <g key={key3(g)} data-grip3d={g.key} data-owner={g.owner}>
        <line x1={q.x - ux * 10} y1={q.y - uy * 10} x2={q.x + ux * 10} y2={q.y + uy * 10} className="grip3d-stem" />
        <path d={`${head(1)}${head(-1)}`} className={cls} />
        <circle cx={q.x} cy={q.y} r={5} className={cls} />
        <title>{g.title ? t(g.title) : null}</title>
      </g>
    );
  };
  const guide3 = drag3 && Math.abs(drag3.by) > 1e-6 ? (() => {
    const g = drag3.grip, a = project(g.at), b = project({ x: g.at.x + g.dir.x * drag3.by, y: g.at.y + g.dir.y * drag3.by, z: g.at.z + g.dir.z * drag3.by });
    return a && b ? { a, b } : null;
  })() : null;
  const mark = snap ? project(snap.point) : null;
  const guide = snap?.guide ? [project(snap.guide.from), project(snap.guide.to)] : null;
  const markClass = snap?.kind === 'Endpoint' ? 'sketch-end' : snap?.kind === 'Midpoint' ? 'sketch-mid' : snap?.kind === 'On edge' ? 'sketch-edge' : 'face-sketch-cursor';
  return (
    <>
      <svg ref={svg} className="grip-overlay" onPointerDown={(e) => { if (down3(e) || editor.onPointerDown(e)) e.stopPropagation(); }}
        onPointerMove={(e) => { if (move3(e) && drag3) return; if (!editor.onPointerMove(e) && snap) setSnap(null); }} onPointerUp={() => { if (up3()) return; editor.onPointerUp(); setSnap(null); }} onPointerLeave={() => { editor.onPointerLeave(); if (!drag3) setHot3(null); }}>
        {editor.render}
        {grips3.map(shape3)}
        {guide3 && <g className="plan-preview" data-testid="grip3d-drag">
          <line x1={guide3.a.x} y1={guide3.a.y} x2={guide3.b.x} y2={guide3.b.y} className="grip3d-guide" />
          <text x={guide3.b.x + 12} y={guide3.b.y - 10}>{drag3!.by > 0 ? '+' : ''}{formatLength(drag3!.by)}</text>
        </g>}
        {mark && <g className="face-sketch">
          {guide?.[0] && guide[1] && <line x1={guide[0].x} y1={guide[0].y} x2={guide[1].x} y2={guide[1].y} className={`sketch-guide ${snap!.guide!.color}`} />}
          <circle cx={mark.x} cy={mark.y} r={snap!.kind === 'On face' ? 3 : 5} className={markClass} data-grip3d="mark" />
        </g>}
      </svg>
      {mark && snap && <div className={`face-sketch-snap-name ${snap.color ?? ''}`} style={{ left: mark.x + 10, top: mark.y + 8 }} data-grip3d="inference">{snap.kind}</div>}
      {liveOwner === 'grip' && <LiveBox toScreen={(p) => toScreen(p, plane.current)} />}
      {liveOwner === 'grip3d' && drag3 && <LiveBox toScreen={(p) => project({ x: drag3.grip.at.x + drag3.grip.dir.x * p.x, y: drag3.grip.at.y + drag3.grip.dir.y * p.x, z: drag3.grip.at.z + drag3.grip.dir.z * p.x }) ?? { x: -9999, y: -9999 }} />}
    </>
  );
}
