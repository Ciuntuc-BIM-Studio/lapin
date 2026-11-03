/**
 * The overlay of faceSketch.ts over the 3D view: the face drawn on lit, the
 * shape following the mouse on it, what the cursor snaps to, and the size,
 * which can be typed (a rectangle's "width, height", a circle's radius, a
 * polygon side's length). A rectangle and a circle take a second click, a
 * polygon its corners until a click on its first one or Enter; Backspace takes
 * the last corner back, Esc drops it. The right and middle buttons still turn
 * and move the view.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { X } from 'lucide-react';
import { useUi } from '../app/store';
import { useCamera, type Vec3 } from './camera';
import {
  circleOnFace, distance3, endFaceSketch, local, onPlane, parsePair, rectOnFace, rectTyped, roundOnFace, snapPoints, useFaceSketch, world,
} from './faceSketch';
import { formatLength, parseLength } from './liveInput';

type Pt = { x: number; y: number };
const SNAP_PX = 10;

export function FaceSketch3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const { spec, pts, cursor, snap, text } = useFaceSketch();
  const input = useRef<HTMLInputElement>(null);
  const tool = useUi((s) => s.tool);
  const startTool = useRef(tool);

  useEffect(() => { if (spec) startTool.current = useUi.getState().tool; }, [spec]);
  useEffect(() => { if (spec && tool !== startTool.current) endFaceSketch(); }, [tool, spec]);
  useEffect(() => () => endFaceSketch(), []);
  useEffect(() => { if (spec) requestAnimationFrame(() => input.current?.focus()); }, [spec, pts.length]);

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
    if (!spec) return;
    const inside = (e: PointerEvent) => {
      const r = rectOf();
      return !!r && e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    };
    /** The point under the cursor on the face's plane: a corner, an edge's middle or the face's middle near it, else to the centimetre. */
    const pointAt = (e: PointerEvent): { at: Vec3; what: string | null } | null => {
      const c = camera.current, r = rectOf();
      if (!c || !r) return null;
      c.updateMatrixWorld();
      const sx = e.clientX - r.left, sy = e.clientY - r.top;
      const ray = new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2((sx / r.width) * 2 - 1, -(sy / r.height) * 2 + 1), c);
      const hit = onPlane(spec.frame, ray.ray.origin, ray.ray.direction);
      if (!hit) return null;
      const st = useFaceSketch.getState();
      const near = [...snapPoints(spec.outline), ...(st.pts.length >= 3 && spec.shape === 'polygon' ? [{ at: st.pts[0], what: 'Close the shape' }] : [])]
        .map((s) => ({ ...s, px: toScreen(s.at) }))
        .filter((s) => s.px && Math.hypot(s.px.x - sx, s.px.y - sy) < SNAP_PX)
        .sort((a, b) => Math.hypot(a.px!.x - sx, a.px!.y - sy) - Math.hypot(b.px!.x - sx, b.px!.y - sy))[0];
      return near ? { at: near.at, what: near.what } : { at: roundOnFace(spec.frame, hit, e.shiftKey ? 0.1 : 0.01), what: null };
    };
    const onMove = (e: PointerEvent) => {
      if (e.buttons !== 0) return;
      const p = pointAt(e);
      if (p) useFaceSketch.setState({ cursor: p.at, snap: p.what });
      if (inside(e)) e.stopPropagation();
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      if ((e.target as Element | null)?.closest?.('.axis-drag-chip, .axis-drag-value')) return;
      e.stopPropagation(); e.preventDefault();
      const p = pointAt(e);
      if (p) place(p.at, p.what === 'Close the shape');
    };
    const onUp = (e: PointerEvent) => { if (inside(e) && e.button === 0) e.stopPropagation(); };
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement | null;
      if (el === input.current || el?.closest?.('input, textarea, select, [contenteditable]')) return;
      if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); enter(); }
      else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); endFaceSketch(); }
      else if (e.key === 'Backspace') { e.preventDefault(); e.stopPropagation(); back(); }
      else if (/^[0-9.,+\-'"a-z ]$/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey) input.current?.focus();
    };
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    return () => {
      window.removeEventListener('keydown', onKey, true);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
    };
  }, [spec, camera, canvas]);

  if (!spec) return null;
  const f = spec.frame;
  const typed = text.trim();
  const last = pts[pts.length - 1];

  // The shape as it stands, with the cursor or the typed size.
  let shape: Vec3[] = [];
  let label = '';
  if (spec.shape === 'rect') {
    const pair = typed ? parsePair(typed, (s) => parseLength(s)) : null;
    shape = pair ? rectTyped(f, pts[0], cursor, pair[0], pair[1]) : cursor ? rectOnFace(f, pts[0], cursor) : [];
    if (shape.length) label = `${formatLength(distance3(shape[0], shape[1]))} × ${formatLength(distance3(shape[1], shape[2]))}`;
  } else if (spec.shape === 'circle') {
    const r = typed ? parseLength(typed) : cursor ? distance3(pts[0], cursor) : null;
    if (r && r > 1e-4) { shape = circleOnFace(f, pts[0], Math.abs(r)); label = `R ${formatLength(Math.abs(r))}`; }
  } else {
    const next = nextPolygonPoint();
    shape = next ? [...pts, next] : [...pts];
    if (next && last) label = formatLength(distance3(last, next));
  }

  const pathOf = (list: Vec3[], close: boolean) => {
    const s = list.map(toScreen);
    if (s.length < 2 || s.some((p) => !p)) return null;
    return s.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('') + (close ? 'Z' : '');
  };
  const facePath = pathOf(spec.outline, true);
  const shapePath = pathOf(shape, spec.shape !== 'polygon' || shape.length >= 3);
  const cur = cursor && toScreen(cursor);
  const labelAt = (spec.shape === 'polygon' ? (shape.length ? toScreen(shape[shape.length - 1]) : null) : cur) ?? toScreen(pts[0]);
  const hint = spec.shape === 'rect' ? 'Click the opposite corner, or type width, height'
    : spec.shape === 'circle' ? 'Click a point on the circle, or type the radius'
      : 'Click the corners · a click on the first or Enter closes it · Backspace takes one back';

  return (
    <>
      <svg className="grip-overlay axis-drag face-sketch">
        {facePath && <path d={facePath} className="face-sketch-face" />}
        {shapePath && <path d={shapePath} className="face-sketch-shape" />}
        {pts.map((p, i) => { const s = toScreen(p); return s && <circle key={i} cx={s.x} cy={s.y} r={3.5} className="axis-origin" />; })}
        {cur && <circle cx={cur.x} cy={cur.y} r={snap ? 6 : 3} className={snap ? 'face-sketch-snap' : 'face-sketch-cursor'} />}
      </svg>
      {cur && snap && <div className="face-sketch-snap-name" style={{ left: cur.x + 10, top: cur.y + 8 }}>{snap}</div>}
      {labelAt && (
        <div className="axis-drag-value" style={{ left: labelAt.x + 18, top: labelAt.y - 14 }}>
          <input ref={input} value={text} placeholder={label || '…'} aria-label="Size on the face" spellCheck={false}
            onChange={(e) => useFaceSketch.setState({ text: e.target.value })}
            onKeyDown={(e) => {
              if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); enter(); }
              else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); e.nativeEvent.stopImmediatePropagation(); endFaceSketch(); }
              else if (e.key === 'Backspace' && !text) { e.preventDefault(); back(); }
            }} />
        </div>
      )}
      <div className="axis-drag-chip mode-chip">
        <span>{spec.shape === 'rect' ? 'Rectangle on face' : spec.shape === 'circle' ? 'Circle on face' : 'Polygon on face'}</span>
        <span className="mode-hint">{hint}</span>
        <span className="mode-hint">Esc cancels</span>
        <button type="button" className="mode-close" title="Cancel (Esc)" aria-label="Cancel" onClick={endFaceSketch}><X size={12} /></button>
      </div>
    </>
  );

  /** A polygon's next corner: the cursor, or the typed length along the way to it. */
  function nextPolygonPoint(): Vec3 | null {
    const st = useFaceSketch.getState();
    const lastPt = st.pts[st.pts.length - 1];
    const len = st.text.trim() ? parseLength(st.text.trim()) : null;
    if (len != null && lastPt && st.cursor) {
      const a = local(f, lastPt), b = local(f, st.cursor);
      const d = Math.hypot(b.x - a.x, b.y - a.y);
      if (d < 1e-9) return null;
      return world(f, a.x + ((b.x - a.x) / d) * len, a.y + ((b.y - a.y) / d) * len);
    }
    return st.cursor;
  }

  function finish(outline: Vec3[], circle?: { center: Vec3; radius: number }) {
    const s = useFaceSketch.getState().spec;
    if (!s) return;
    endFaceSketch();
    s.done(circle ? { ...circle, outline } : { outline });
  }

  function place(at: Vec3, close: boolean) {
    const st = useFaceSketch.getState();
    if (!st.spec) return;
    const first = st.pts[0];
    if (st.spec.shape === 'rect') {
      const r = rectOnFace(f, first, at);
      if (distance3(r[0], r[1]) < 1e-3 || distance3(r[1], r[2]) < 1e-3) return;
      finish(r);
    } else if (st.spec.shape === 'circle') {
      const radius = distance3(first, at);
      if (radius < 1e-3) return;
      finish(circleOnFace(f, first, radius), { center: first, radius });
    } else {
      if (close || (st.pts.length >= 3 && distance3(at, first) < 1e-6)) { finish(st.pts); return; }
      if (distance3(at, st.pts[st.pts.length - 1]) < 1e-6) return;
      useFaceSketch.setState({ pts: [...st.pts, at], text: '' });
    }
  }

  function enter() {
    const st = useFaceSketch.getState();
    if (!st.spec) return;
    const t = st.text.trim();
    if (st.spec.shape === 'rect') {
      const pair = t ? parsePair(t, (s) => parseLength(s)) : null;
      if (pair) finish(rectTyped(f, st.pts[0], st.cursor, pair[0], pair[1]));
    } else if (st.spec.shape === 'circle') {
      const r = t ? parseLength(t) : null;
      if (r && Math.abs(r) > 1e-4) finish(circleOnFace(f, st.pts[0], Math.abs(r)), { center: st.pts[0], radius: Math.abs(r) });
    } else if (t) {
      const next = nextPolygonPoint();
      if (next) useFaceSketch.setState({ pts: [...st.pts, next], text: '' });
    } else if (st.pts.length >= 3) finish(st.pts);
  }

  function back() {
    const st = useFaceSketch.getState();
    if (st.spec?.shape !== 'polygon') return;
    if (st.pts.length <= 1) { endFaceSketch(); return; }
    useFaceSketch.setState({ pts: st.pts.slice(0, -1) });
  }
}

