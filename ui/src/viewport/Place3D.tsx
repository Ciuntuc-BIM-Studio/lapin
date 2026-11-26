/**
 * The placing tools in the 3D view (docs/plan-3d-modelling.md, stage B;
 * testers' report #105; the clicks' rules in gesture3d.ts): while a tool that
 * takes points is on (a wall, a floor, a column, a railing, a road…), the
 * cursor is snapped in 3D (snap3d.ts: the model's endpoints, midpoints, edges
 * and faces, the red and green axes from the last point) and brought onto the
 * active level, where the tool takes its plan points; the points and the
 * outline they make are drawn on the level. A value typed at any time goes
 * from the last point the way the cursor points (1200, 1000<30; a rectangle's
 * 1200,600), Enter applies it or closes a polygon and ends a polyline,
 * Backspace takes the last point back, Esc drops the points and then the tool.
 * A drag still turns the view.
 */

import { screenPath } from './screenPath';
import { keyIsForField } from './overlayKeys';
import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { setTool, useUi } from '../app/store';
import { claimValue } from '../app/valueOwner';
import { activeStorey, useSession } from '../engine/session';
import { num, storeys } from '../engine/model';
import { findTool, useToolTick } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { useCamera, type Vec3 } from './camera';
import { useCad } from './cad/session';
import { formatLength, parseLength } from './liveInput';
import { completeTool } from './tools';
import { clickStep, constrained, enterStep, isPointGesture, previewOf, storeyAt, typedPoint, type PointGesture } from './gesture3d';
import { insideView, projector, snapAt, type ViewRefs } from './scene3d';
import { arrowLock3, type Lock3, type Snapped3 } from './snap3d';
import { parseVcb } from './sketch/vcb';
import { useMove3d } from './moveIn3d';
import { playSound } from '../app/sounds';
import { usePrefs } from '../app/prefs';
import { PrecisionPill } from './PrecisionPill';

const HINT: Record<PointGesture['kind'], string> = {
  point: 'Click a point: on the level, or a corner or an edge of the model',
  twoPoints: 'Click the start, then the end, or type a length',
  rectangle: 'Click a corner, then the opposite one, or type width,height',
  threePoints: 'Click the start, the end, then a point the arc passes through',
  polygon: 'Click the corners · back on the first, or Enter, closes it · Backspace takes one back',
  polyline: 'Click the points · Enter ends it · Backspace takes one back',
};

/** The storey a point in space stands on (gesture3d.ts's rule over the project's storeys). */
export const storeyOf = (z: number): string | null =>
  storeyAt(z, storeys(useSession.getState()).map((n) => ({ id: n.id, elevation: num(n, 'elevation') })));

export function Place3D({ camera, canvas, scene }: ViewRefs) {
  const ghost = usePrefs((p) => p.ghostPreview);
  useCamera();
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  const g = isPointGesture(tool?.gesture) ? tool!.gesture as PointGesture : null;
  const command = useCad((s) => s.command);
  const moving = useMove3d((s) => s.ids.length > 0);
  const active = !!g && !command && !moving;
  const [pts, setPts] = useState<Vec2[]>([]);
  const [at, setAt] = useState<Snapped3 | null>(null);
  const [text, setText] = useState('');
  const [lock, setLock] = useState<Lock3>(null);
  const refs: ViewRefs = { camera, canvas, scene };
  const refsRef = useRef(refs);
  refsRef.current = refs;
  /** The height of each point clicked (3D snaps), kept for a tool that measures in space. */
  const zs = useRef<number[]>([]);
  const state = useRef({ pts, at, text, lock });
  state.current = { pts, at, text, lock };
  const level = () => num(activeStorey() ?? undefined, 'elevation');

  // A new tool, a new gesture.
  useToolTick((s) => s.n);
  const toolMode = tool?.mode?.();
  useEffect(() => { setPts([]); setText(''); setLock(null); setAt(null); }, [toolId, toolMode]);

  const cursor2 = (s: Snapped3 | null): Vec2 | null => (s ? { x: s.point.x, y: s.point.y } : null);
  const placeAt = (p: Vec2, typed?: number, hit?: Snapped3 | null) => {
    if (!g || !tool) return;
    const step = clickStep(g, state.current.pts, constrained(g, state.current.pts, p), 8 / pxPerM());
    setPts(step.pts);
    // The height of the point clicked (a snap off the level), kept for a tool that measures in space.
    const prev = state.current.pts.length;
    const z = hit && hit.kind !== 'On level' ? hit.point.z : level();
    const heights = step.done ? (step.done.length > prev ? [...zs.current.slice(0, prev), z] : zs.current.slice(0, step.done.length)) : undefined;
    zs.current = step.done ? (step.pts.length === 1 ? [z] : []) : step.pts.length > prev ? [...zs.current.slice(0, prev), z] : zs.current.slice(0, step.pts.length);
    setText('');
    setLock(null);
    // One click on a floor of another storey (a chair on the first floor's slab): the element goes on that storey.
    const on = g.kind === 'point' && hit && hit.kind !== 'On level' ? storeyOf(hit.point.z) : null;
    if (step.done) { playSound('place'); void completeTool(tool, { points: step.done, heights, ...(typed != null ? { typed } : {}) }, undefined, on); }
    else playSound('snap');
  };
  /** Pixels a metre where the cursor is (how near closes a polygon). */
  const pxPerM = () => {
    const c = useCamera.getState();
    const h = canvas.current?.getBoundingClientRect().height ?? 800;
    return h / (2 * Math.max(c.distance, 0.1) * Math.tan(c.fov / 2));
  };
  const enter = () => {
    if (!g || !tool) return;
    const s = state.current;
    const v = s.text ? parseVcb(s.text, (x) => parseLength(x)) : null;
    // Measuring (Scale image, Calibrate scale): the second point is the cursor's, the length typed their true distance.
    if (v && g.kind === 'twoPoints' && g.measure && s.pts.length === 1 && v.kind === 'length' && s.at) { placeAt(cursor2(s.at)!, v.value, s.at); return; }
    if (v) {
      const p = typedPoint(g, s.pts, v, cursor2(s.at));
      if (p) placeAt(p, v.kind === 'length' || v.kind === 'lengthAngle' ? v.value : undefined);
      return;
    }
    const step = enterStep(g, s.pts);
    if (step && !step.done) { setPts(step.pts); return; }
    if (step) { setPts(step.pts); void completeTool(tool, { points: step.done!, heights: Array.from({ length: step.done!.length }, (_, i) => zs.current[i] ?? level()) }); zs.current = []; }
  };
  const enterRef = useRef(enter);
  enterRef.current = enter;
  const placeRef = useRef(placeAt);
  placeRef.current = placeAt;

  // The value's keys are the tool's once a point is down (decision 378).
  const waiting = active && pts.length > 0;
  useEffect(() => {
    if (!waiting) return;
    return claimValue({ text: () => state.current.text, set: setText, enter: () => enterRef.current() });
  }, [waiting]);

  useEffect(() => {
    if (!active) return;
    let frame = 0;
    let last: { x: number; y: number } | null = null;
    let down: { x: number; y: number } | null = null;
    const base = (): Vec3 | null => { const p = state.current.pts; const q = p[p.length - 1]; return q ? { x: q.x, y: q.y, z: level() } : null; };
    const follow = () => {
      frame = 0;
      if (!last) return;
      setAt(snapAt(refsRef.current, last.x, last.y, { level: level(), base: base(), lock: state.current.lock, axes: ['red', 'green'] }));
    };
    const onMove = (e: PointerEvent) => {
      if (e.buttons !== 0 || !insideView(canvas.current, e.clientX, e.clientY)) return;
      last = { x: e.clientX, y: e.clientY };
      if (!frame) frame = requestAnimationFrame(follow);
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !insideView(canvas.current, e.clientX, e.clientY)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .axis-drag-value, input, button')) return;
      down = { x: e.clientX, y: e.clientY };
    };
    const onUp = (e: PointerEvent) => {
      const d = down;
      down = null;
      if (e.button !== 0 || !d || Math.hypot(e.clientX - d.x, e.clientY - d.y) > 4) return;
      // A click (not a drag, which turns the view): the point snapped where it is. The release goes on to the orbit
      // controls, which saw the press (the view's own click handling leaves point tools to this host).
      const s = snapAt(refsRef.current, e.clientX, e.clientY, { level: level(), base: base(), lock: state.current.lock, axes: ['red', 'green'] });
      if (s) placeRef.current({ x: s.point.x, y: s.point.y }, undefined, s);
    };
    const onKey = (e: KeyboardEvent) => {
      if (keyIsForField(e, '[data-place3d]')) return;
      const s = state.current;
      if (e.key === 'Escape') {
        e.preventDefault(); e.stopImmediatePropagation();
        if (s.text) setText(''); else if (s.pts.length) setPts([]); else setTool(null);
        return;
      }
      if (e.key === 'Backspace' && !s.text && s.pts.length) { e.preventDefault(); e.stopImmediatePropagation(); setPts(s.pts.slice(0, -1)); return; }
      if (e.key === 'Enter' && !s.text && s.pts.length) { e.preventDefault(); e.stopImmediatePropagation(); enterRef.current(); return; }
      if (s.pts.length && (e.key === 'ArrowRight' || e.key === 'ArrowLeft')) {
        const l = arrowLock3(e.key, s.lock);
        if (l !== undefined) { e.preventDefault(); e.stopImmediatePropagation(); setLock(l); if (!frame && last) frame = requestAnimationFrame(follow); }
      }
    };
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
      window.removeEventListener('keydown', onKey, true);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, canvas]);

  if (!active || !g || !tool) return null;
  const z = level();
  const project = projector(refs);
  // A tool that measures in space (Measure): its points drawn at the heights they were snapped at, the cursor's at its own.
  const space = !!tool.inSpace;
  const heightAt = (i: number) => (space ? (i < pts.length ? zs.current[i] ?? z : at && at.kind !== 'On level' ? at.point.z : z) : z);
  const w = (p: Vec2, i = -1): Vec3 => ({ x: p.x, y: p.y, z: i < 0 ? z : heightAt(i) });
  const cur2 = cursor2(at);
  const vcb = text ? parseVcb(text, (x) => parseLength(x)) : null;
  const typed = vcb ? typedPoint(g, pts, vcb, cur2) : null;
  const aim = typed ?? (cur2 ? constrained(g, pts, cur2) : null);
  const shape = previewOf(g, pts, aim);
  const screen = shape.pts.map((p, i) => project(w(p, space ? i : -1)));
  const path = screenPath(screen, !!shape.closed);
  // Volumetric 3D ghost preview (docs/plan-ux-game-like.md, P4), a Flow extra: extruded translucent walls/envelope at storey height.
  const ghostH = ghost && !space && shape.pts.length >= 2 ? (tool.id.includes('slab') ? 0.25 : num(activeStorey() ?? undefined, 'height', 3)) : 0;
  const topScreen = ghostH > 0 ? shape.pts.map((p) => project({ x: p.x, y: p.y, z: z + ghostH })) : [];
  const topPath = topScreen.length >= 2 ? screenPath(topScreen, !!shape.closed) : null;
  const ghostQuads: string[] = [];
  if (ghostH > 0 && screen.length === topScreen.length && screen.length >= 2) {
    const segs = shape.closed ? screen.length : screen.length - 1;
    for (let i = 0; i < segs; i++) {
      const j = (i + 1) % screen.length;
      const a = screen[i], b = screen[j], c = topScreen[j], d = topScreen[i];
      if (a && b && c && d) ghostQuads.push(`M${a.x.toFixed(1)} ${a.y.toFixed(1)}L${b.x.toFixed(1)} ${b.y.toFixed(1)}L${c.x.toFixed(1)} ${c.y.toFixed(1)}L${d.x.toFixed(1)} ${d.y.toFixed(1)}Z`);
    }
  }
  const cur = at ? project(at.point) : null;
  // A point snapped off the level (a beam's end, a slab's top above it): a plumb line down to where the tool takes it.
  const foot = at && Math.abs(at.point.z - z) > 1e-3 ? project({ x: at.point.x, y: at.point.y, z }) : null;
  const guide = at?.guide ? [project(at.guide.from), project(at.guide.to)] : null;
  const lastPt = pts[pts.length - 1];
  // In space (Measure, Calibrate scale): the leg's true length, its heights counted.
  const label = lastPt && aim ? formatLength(Math.hypot(aim.x - lastPt.x, aim.y - lastPt.y, space ? heightAt(pts.length) - heightAt(pts.length - 1) : 0)) : '';
  const markClass = at?.kind === 'Endpoint' ? 'sketch-end' : at?.kind === 'Midpoint' ? 'sketch-mid' : at?.kind === 'On edge' ? 'sketch-edge' : 'face-sketch-cursor';
  const storey = activeStorey();
  // What the tool measures so far, the cursor's point counted (Measure's running total, its area).
  const info = tool.liveInfo ? tool.liveInfo([...pts.map((p, i) => ({ x: p.x, y: p.y, z: heightAt(i) })), ...(aim ? [{ x: aim.x, y: aim.y, z: heightAt(pts.length) }] : [])]) : null;

  return (
    <>
      <svg className="grip-overlay axis-drag face-sketch" data-place3d="overlay">
        {guide?.[0] && guide[1] && <line x1={guide[0].x} y1={guide[0].y} x2={guide[1].x} y2={guide[1].y} className={`sketch-guide ${at!.guide!.color}`} />}
        {ghostQuads.map((d, i) => <path key={`gq-${i}`} d={d} className={`place3d-ghost-face${text && !typed ? ' bad' : ''}`} />)}
        {topPath && <path d={topPath} className={`place3d-ghost-top${text && !typed ? ' bad' : ''}`} />}
        {path && <path d={path} className={shape.closed ? 'face-sketch-shape' : 'face-sketch-line'} />}
        {pts.map((p, i) => { const s = project(w(p, space ? i : -1)); return s && <circle key={i} cx={s.x} cy={s.y} r={3.5} className="axis-origin" />; })}
        {cur && foot && <line x1={cur.x} y1={cur.y} x2={foot.x} y2={foot.y} className="sketch-guide blue" strokeDasharray="3 3" />}
        {cur && <circle cx={cur.x} cy={cur.y} r={at && at.kind !== 'On face' && at.kind !== 'On level' ? 5 : 3} className={markClass} />}
      </svg>
      {cur && at && <div className={`face-sketch-snap-name ${at.color ?? ''}`} style={{ left: cur.x + 10, top: cur.y + 8 }} data-place3d="inference" data-at={`${at.point.x.toFixed(3)},${at.point.y.toFixed(3)},${at.point.z.toFixed(3)}`}>{at.kind}</div>}
      {cur && pts.length > 0 && (
        <PrecisionPill
          x={cur.x + 18}
          y={cur.y - 30}
          text={text}
          placeholder={label || '…'}
          bad={Boolean(text && !typed)}
          badge={lock ? lock.axis.toUpperCase() : at?.kind ?? null}
          badgeColor={lock?.axis ?? at?.color ?? null}
          ariaLabel="Measurements"
          dataTestAttr={{ 'data-place3d': 'vcb' }}
          onChange={setText}
          onEnter={() => enterRef.current()}
        />
      )}
      <div className="axis-drag-chip mode-chip" data-place3d="chip">
        <span>{tool.label}</span>
        {info && <span className="mode-hint mode-info" data-place3d="info">{info}</span>}
        {storey && !space && <><span>Placing on</span><span data-no-i18n>{storey.name}</span></>}
        <span className="mode-hint">{g.kind === 'threePoints' && !g.arc && g.hint ? g.hint : HINT[g.kind]}</span>
        {pts.length > 0 && <span className="mode-hint">{lock ? 'Locked: the arrow again lets go' : '→ red · ← green'}</span>}
        <button type="button" className="mode-close" title="Done (Esc)" aria-label="Done" onClick={() => setTool(null)}><X size={12} /></button>
      </div>
    </>
  );
}
