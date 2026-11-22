/**
 * The SubD cage edit mode in the 3D view (docs/plan-studio-freeform.md §8, F1c): the cage drawn as a hull wireframe over the
 * smooth body (points, edges; creased edges thicker and coloured; the border of an open surface dashed), a pick filter (Vertex,
 * Edge, Face: keys 1, 2, 3), a click picks and Shift adds; what is picked is dragged with the 3D snaps and the axes of Move 3D
 * (snap3d.ts: → red, ← green, ↑ blue; a distance or dx,dy,dz typed, Enter), and letting go is ONE `studio.subdEdit`. The body is
 * redrawn by the engine after the release; while dragging only the wireframe follows. The rules are in subdModel.ts.
 */

import { useEffect, useRef, useState } from 'react';
import { Dot, Slash, Square, X } from 'lucide-react';
import type { Overlay3DProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { claimValue } from '../../app/valueOwner';
import { useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { poseKey, useCamera, type Vec3 } from '../../viewport/camera';
import { formatLength, parseLength } from '../../viewport/liveInput';
import { moveTarget } from '../../viewport/moveIn3d';
import { insideView, projector, snapAt, rayAt, type ViewRefs } from '../../viewport/scene3d';
import { arrowLock3, segNearRay } from '../../viewport/snap3d';
import {
  anchorOf, dragBy, edgeClasses, edgeKey, edgeOf, moves, opProblem, pick, pickAt, pointsOf, previewPoints, SHARPNESS, type Lens, type Picked, type PickMode,
} from './subdModel';
import { applyMove, applyOp, applyTransform, editingCage, leaveCage, refreshCage, setMode, useSubD } from './subdStore';
import { notify } from '../../app/store';

/** Dragged less than this (px) is a click. */
const CLICK_PX = 4;

const FILTERS: { mode: PickMode; label: string; key: string; icon: typeof Dot }[] = [
  { mode: 'vertex', label: 'Vertex', key: '1', icon: Dot }, { mode: 'edge', label: 'Edge', key: '2', icon: Slash }, { mode: 'face', label: 'Face', key: '3', icon: Square },
];

const isPicked = (p: Picked, mode: PickMode, key: string) => p.mode === mode && p.keys.includes(key);

export function SubDEdit3D({ camera, canvas, scene, invalidate }: Overlay3DProps) {
  useCamera();
  void poseKey();
  const st = useSubD();
  const view = useUi((s) => s.view);
  const selected = useUi((s) => s.selected);
  const version = useSession((s) => s.version);
  const nodes = useSession((s) => s.nodes);
  const active = st.id != null && view === '3d';
  const refs: ViewRefs = { camera, canvas, scene: scene ?? { current: null } };
  const refsRef = useRef(refs);
  refsRef.current = refs;
  const target = useRef<Vec3 | null>(null);

  // Leaves when another solid is selected, the solid is gone, or the view is not 3D.
  useEffect(() => {
    if (!st.id) return;
    if (view !== '3d' || selected.length !== 1 || selected[0] !== st.id || !nodes.get(st.id)) leaveCage();
  }, [st.id, view, selected, nodes]);
  // The cage asked again when the model changes (an undo, another edit).
  useEffect(() => { if (st.id) void refreshCage(); }, [st.id, version]);
  useEffect(() => { invalidate?.current?.(); }, [st.id, invalidate]);

  const dragging = !!st.drag?.moved;
  const dragText = st.drag?.text ?? '';
  const targetOf = (): Vec3 | null => {
    const s = useSubD.getState();
    if (!s.drag) return null;
    return moveTarget(s.drag.base, s.drag.at, s.drag.text, s.drag.lock, false, (x) => parseLength(x));
  };
  const finish = async () => {
    const s = useSubD.getState();
    if (!s.drag) return;
    const to = targetOf();
    const by = to ? dragBy(s.drag.base, to) : { x: 0, y: 0, z: 0 };
    if (moves(by)) await applyMove(by);
    useSubD.setState({ drag: null });
  };
  const finishRef = useRef(finish);
  finishRef.current = finish;

  // The typed distance is the drag's once it begins (decision 378): digits and Enter go to it.
  useEffect(() => {
    if (!dragging) return;
    return claimValue({
      text: () => useSubD.getState().drag?.text ?? '',
      set: (text) => useSubD.setState((s) => (s.drag ? { drag: { ...s.drag, text } } : {})),
      enter: () => { if ((useSubD.getState().drag?.text ?? '').trim()) void finishRef.current(); },
    });
  }, [dragging]);

  useEffect(() => {
    if (!active) return;
    let press: { x: number; y: number; mode: PickMode; item: number | [number, number]; base: Vec3; was: boolean; additive: boolean; moved: boolean } | null = null;
    let frame = 0;
    let last: { x: number; y: number } | null = null;
    const lens = (): Lens => {
      const project = projector(refsRef.current);
      const c = camera.current;
      const o = c ? { x: c.position.x, y: c.position.y, z: c.position.z } : { x: 0, y: 0, z: 0 };
      return { project: (p) => project({ x: p[0]!, y: p[1]!, z: p[2]! }), depth: (p) => Math.hypot(p[0]! - o.x, p[1]! - o.y, p[2]! - o.z) };
    };
    const local = (e: { clientX: number; clientY: number }) => { const r = canvas.current?.getBoundingClientRect(); return { x: e.clientX - (r?.left ?? 0), y: e.clientY - (r?.top ?? 0) }; };
    const keyOf = (mode: PickMode, item: number | [number, number]) => (mode === 'edge' ? edgeKey((item as [number, number])[0], (item as [number, number])[1]) : String(item));
    const hitAt = (e: { clientX: number; clientY: number }) => {
      const s = useSubD.getState();
      return s.cage ? pickAt(s.cage, s.mode, lens(), local(e)) : null;
    };
    const follow = () => {
      frame = 0;
      const s = useSubD.getState();
      if (!last || !s.drag || !s.cage) return;
      const own = new Set(pointsOf(s.cage, s.picked));
      const points = s.cage.points.filter((_, i) => !own.has(i)).map((p) => ({ x: p[0]!, y: p[1]!, z: p[2]! }));
      const at = snapAt(refsRef.current, last.x, last.y, { level: s.drag.base.z, base: s.drag.base, lock: s.drag.lock, skip: new Set([s.id!]), points });
      useSubD.setState({ drag: { ...s.drag, at } });
      target.current = targetOf();
    };
    const queue = (e: PointerEvent) => { last = { x: e.clientX, y: e.clientY }; if (!frame) frame = requestAnimationFrame(follow); };
    const onUi = (e: Event) => !!(e.target as Element | null)?.closest?.('.sketch-bar, .mode-chip, input, button, select, textarea');

    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !insideView(canvas.current, e.clientX, e.clientY) || onUi(e) || useUi.getState().tool) return;
      const s = useSubD.getState();
      if (s.drag || !s.cage) return;
      const item = hitAt(e);
      // Not on the cage: the press goes on (the view turns); a click that follows lets go of the pick (subdStore's pickHandlers).
      if (item == null) return;
      e.stopPropagation(); e.preventDefault();
      const key = keyOf(s.mode, item);
      const was = isPicked(s.picked, s.mode, key);
      let base = anchorOf(s.cage, s.mode, item)!;
      if (s.mode === 'edge') {
        const [a, b] = item as [number, number];
        const ray = rayAt(refsRef.current, e.clientX, e.clientY);
        const pa = s.cage.points[a]!, pb = s.cage.points[b]!;
        if (ray) base = segNearRay({ a: { x: pa[0]!, y: pa[1]!, z: pa[2]! }, b: { x: pb[0]!, y: pb[1]!, z: pb[2]! } }, ray);
      }
      // Pressed on what is picked (without Shift) keeps the pick to drag it all; a click without a drag then picks it alone.
      if (!(was && !e.shiftKey)) useSubD.setState({ picked: pick(s.picked, s.mode, item, e.shiftKey) });
      press = { x: e.clientX, y: e.clientY, mode: s.mode, item, base, was, additive: e.shiftKey, moved: false };
    };
    const onMove = (e: PointerEvent) => {
      if (!insideView(canvas.current, e.clientX, e.clientY)) return;
      const s = useSubD.getState();
      if (press) {
        e.stopPropagation();
        if (!press.moved && Math.hypot(e.clientX - press.x, e.clientY - press.y) >= CLICK_PX && useSubD.getState().picked.keys.length) {
          press.moved = true;
          useSubD.setState({ drag: { base: press.base, at: null, lock: null, text: '', moved: true } });
        }
        if (press.moved) queue(e);
        return;
      }
      if (s.drag) { e.stopPropagation(); queue(e); return; }
      if (e.buttons !== 0 || onUi(e)) return;
      const item = hitAt(e);
      const hover = item == null ? null : { mode: s.mode, key: keyOf(s.mode, item) };
      if (hover?.key !== s.hover?.key || hover?.mode !== s.hover?.mode) useSubD.setState({ hover });
    };
    const onUp = (e: PointerEvent) => {
      if (e.button !== 0 || !press) return;
      const p = press;
      press = null;
      e.stopPropagation();
      if (p.moved) { void finishRef.current(); return; }
      // A click on what was picked, with no drag: that alone (or off it with Shift).
      if (p.was) {
        const s = useSubD.getState();
        useSubD.setState({ picked: p.additive ? pick(s.picked, p.mode, p.item, true) : pick(s.picked, p.mode, p.item, false) });
      }
    };
    const onKey = (e: KeyboardEvent) => {
      const s = useSubD.getState();
      if (e.target && (e.target as HTMLElement).closest?.('input, textarea, select, [contenteditable], [role="dialog"]')) return;
      if (e.key === 'Escape') {
        e.preventDefault(); e.stopImmediatePropagation();
        if (s.drag) { press = null; if (s.drag.text) useSubD.setState({ drag: { ...s.drag, text: '' } }); else useSubD.setState({ drag: null }); } else leaveCage();
        return;
      }
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      if (s.drag) {
        const l = arrowLock3(e.key, s.drag.lock);
        if (l !== undefined) { e.preventDefault(); e.stopImmediatePropagation(); useSubD.setState({ drag: { ...s.drag, lock: l } }); if (!frame && last) frame = requestAnimationFrame(follow); }
        return;
      }
      const f = FILTERS.find((x) => x.key === e.key);
      if (f) { e.preventDefault(); e.stopImmediatePropagation(); setMode(f.mode); }
    };
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerup', onUp, true);
      window.removeEventListener('keydown', onKey, true);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, canvas, camera]);

  // Leaving takes the mode's pointer states with it.
  useEffect(() => () => { if (editingCage()) leaveCage(); }, []);

  const cam = camera.current, rect = canvas.current?.getBoundingClientRect();
  if (!active || !st.cage || !cam || !rect) return null;
  const cage = st.cage;
  cam.updateMatrixWorld();
  const project = projector(refs);
  const to = st.drag?.moved ? targetOf() : null;
  const by = st.drag && to ? dragBy(st.drag.base, to) : { x: 0, y: 0, z: 0 };
  const pts = st.drag?.moved ? previewPoints(cage, st.picked, by) : cage.points;
  const px = pts.map((p) => project({ x: p[0]!, y: p[1]!, z: p[2]! }));
  const seg = (a: number, b: number) => { const s = px[a], e = px[b]; return s && e ? `M${s.x.toFixed(1)} ${s.y.toFixed(1)}L${e.x.toFixed(1)} ${e.y.toFixed(1)}` : ''; };
  const classes = edgeClasses(cage);
  const sel = st.picked;
  const pickedPts = new Set(pointsOf(cage, sel));
  const hot = st.hover;
  const faceD = (f: number[]) => f.map((v, i) => { const s = px[v]; return s ? `${i ? 'L' : 'M'}${s.x.toFixed(1)} ${s.y.toFixed(1)}` : ''; }).join('') + 'Z';
  const tag = st.drag?.at && dragging ? project(st.drag.at.point) : null;
  const guide = st.drag?.at?.guide ? [project(st.drag.at.guide.from), project(st.drag.at.guide.to)] : null;
  const len = Math.hypot(by.x, by.y, by.z);
  const baseAt = st.drag ? project(st.drag.base) : null;
  const sharp = classes.crease;
  return (
    <>
      <svg className="grip-overlay subd-cage" data-subd="cage" style={{ pointerEvents: 'none' }}>
        {st.mode === 'face' && cage.faces.map((f, i) => (isPicked(sel, 'face', String(i)) || (hot?.mode === 'face' && hot.key === String(i)))
          ? <path key={`f${i}`} d={faceD(f)} className={`subd-face${isPicked(sel, 'face', String(i)) ? ' on' : ' hot'}`} /> : null)}
        <path d={classes.plain.map(([a, b]) => seg(a, b)).join('')} className="subd-edge" />
        <path d={classes.border.map(([a, b]) => seg(a, b)).join('')} className="subd-edge border" />
        <path d={sharp.filter((c) => c[2] < 10).map(([a, b]) => seg(a, b)).join('')} className="subd-edge crease semi" />
        <path d={sharp.filter((c) => c[2] >= 10).map(([a, b]) => seg(a, b)).join('')} className="subd-edge crease sharp" />
        {st.mode === 'edge' && [...(sel.mode === 'edge' ? sel.keys : []), ...(hot?.mode === 'edge' && !isPicked(sel, 'edge', hot.key) ? [hot.key] : [])].map((k) => {
          const [a, b] = edgeOf(k);
          return <path key={`e${k}`} d={seg(a, b)} className={`subd-edge ${isPicked(sel, 'edge', k) ? 'on' : 'hot'}`} />;
        })}
        {px.map((s, i) => {
          if (!s) return null;
          const on = pickedPts.has(i) && (sel.mode === 'vertex' || st.drag?.moved);
          const lit = hot?.mode === 'vertex' && hot.key === String(i);
          if (st.mode !== 'vertex' && !on) return <circle key={i} cx={s.x} cy={s.y} r={2} className="subd-point dim" />;
          return <circle key={i} cx={s.x} cy={s.y} r={on || lit ? 5 : 3.5} className={`subd-point${on ? ' on' : lit ? ' hot' : ''}`} data-subd-point={i} />;
        })}
        {guide?.[0] && guide[1] && st.drag?.at?.guide && <line x1={guide[0].x} y1={guide[0].y} x2={guide[1].x} y2={guide[1].y} className={`subd-guide ${st.drag.at.guide.color}`} />}
        {dragging && baseAt && tag && <line x1={baseAt.x} y1={baseAt.y} x2={tag.x} y2={tag.y} className="grip3d-guide" />}
        {tag && <circle cx={tag.x} cy={tag.y} r={4} className="face-sketch-cursor" data-subd="mark" />}
      </svg>
      {tag && st.drag?.at && <div className={`face-sketch-snap-name ${st.drag.at.color ?? ''}`} style={{ left: tag.x + 10, top: tag.y + 8 }} data-subd="inference">
        {st.drag.at.kind}{len > 1e-6 || dragText ? ` · ${dragText || formatLength(len)}` : ''}
      </div>}
      <SubDBar />
    </>
  );
}

/** The bar over the 3D view while the mode is on: the filter, the ops, the scale and the turn, the way out. */
function SubDBar() {
  const st = useSubD();
  const name = useSession((s) => (st.id ? s.nodes.get(st.id)?.name : '') ?? '');
  const n = st.picked.keys.length;
  const run = (op: Parameters<typeof applyOp>[0]) => { const why = opProblem(st.picked, op.op); if (why) { notify(why); return; } void applyOp(op); };
  const sharpness = useSharp();
  return (
    <div className="sketch-bar in3d subd-bar" data-sketch-bar="subd" role="toolbar">
      <span className="sketch-bar-title"><span>Cage of</span> <span data-no-i18n>{name}</span></span>
      <span className="sketch-bar-group" data-group="filter">
        {FILTERS.map((f) => (
          <button key={f.mode} type="button" className={`icon${st.mode === f.mode ? ' on' : ''}`} title={`${f.label} (${f.key})`} aria-pressed={st.mode === f.mode} data-subd-filter={f.mode} onClick={() => setMode(f.mode)}>
            <f.icon size={14} />
          </button>
        ))}
      </span>
      <span className="muted small" data-subd="picked">{n ? <>{n} <span>{FILTERS.find((f) => f.mode === st.mode)!.label}</span></> : 'Pick on the cage'}</span>
      <span className="sketch-bar-group" data-group="shape">
        <LengthField metres={st.extrude} title="Distance (negative: in); Enter extrudes the picked faces" style={{ width: 64 }}
          onCommit={(m) => { if (m != null) { useSubD.setState({ extrude: m }); run({ op: 'extrude', distance: m }); } }} />
        <button type="button" className="btn small" data-subd-op="extrude" title="Extrude the picked faces by the distance" onClick={() => run({ op: 'extrude', distance: st.extrude })}>Extrude</button>
        <button type="button" className="btn small" data-subd-op="loop" title="An edge loop across the ring of quads the picked edge starts" onClick={() => run({ op: 'loop' })}>Insert loop</button>
        <button type="button" className="btn small" data-subd-op="delete" title="Delete the picked faces: the cage opens there" onClick={() => run({ op: 'delete' })}>Delete faces</button>
      </span>
      <span className="sketch-bar-group" data-group="crease">
        <select className="cell" title="The crease's sharpness: smooth takes it off, semi-sharp rounds off after that many subdivisions, sharp stays" value={sharpness.value} onChange={(e) => sharpness.set(Number(e.target.value))} data-subd="sharpness">
          {SHARPNESS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
        <button type="button" className="btn small" data-subd-op="crease" title="Crease the picked edges" onClick={() => run({ op: 'crease', sharpness: sharpness.value })}>Crease</button>
      </span>
      <span className="sketch-bar-group" data-group="transform">
        <label title="Scale the picked points about their middle; Enter applies"><span className="muted">Scale</span> <NumberField value={st.scale} style={{ width: 52 }}
          onCommit={(v) => { if (v != null) { useSubD.setState({ scale: v }); void applyTransform({ scale: v }); } }} /></label>
        <label title="Turn the picked points about the upright through their middle; Enter applies"><span className="muted">Turn (°)</span> <NumberField value={st.turn} style={{ width: 52 }}
          onCommit={(v) => { if (v != null) { useSubD.setState({ turn: v }); void applyTransform({ rotate: v }); } }} /></label>
      </span>
      <span className="sketch-bar-end">
        <button type="button" className="btn small" data-action="leave-cage" title="Leave the cage edit mode (Esc)" onClick={leaveCage}><X size={12} />Done</button>
      </span>
    </div>
  );
}

// The crease sharpness chosen, kept for the session.
let sharpnessChosen = 10;
function useSharp(): { value: number; set: (v: number) => void } {
  const [value, setValue] = useState(sharpnessChosen);
  return { value, set: (v) => { sharpnessChosen = v; setValue(v); } };
}
