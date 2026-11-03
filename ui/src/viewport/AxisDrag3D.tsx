/**
 * The on-canvas handles of axisDrag.ts over the 3D view: what the operation
 * will make drawn on the model as it goes (a face swept out in green, or in
 * red where it takes away; a plane where it cuts; a box where it moves), the
 * arrow it goes along, and the distance beside it, which can be typed.
 *
 * With one axis (a face's normal) the distance follows the mouse from the
 * moment the face is picked, and a click applies it; with several (a move's
 * x, y, z) an arrow is grabbed and dragged, and letting go applies it. Typing
 * a value (the project's unit, 250, 25cm, 0.25m, -100) holds it; Enter
 * applies, Esc drops it, Tab goes to the next axis; Shift keeps a drag to
 * 10 cm. The right and middle buttons still turn and move the view.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Check, X } from 'lucide-react';
import { useUi } from '../app/store';
import { useCamera, type Vec3 } from './camera';
import { add, alongLine, boxEdges3, cancelAxisDrag, commitAxisDrag, endAxisDrag, snapDistance, useAxisDrag } from './axisDrag';
import { formatLength, parseLength } from './liveInput';

type Pt = { x: number; y: number };
const ADD = '#2f9e6a', CUT = '#d33f2f', PLANE = '#d99a1e';

export function AxisDrag3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const { spec, axis, distance, text, option } = useAxisDrag();
  const input = useRef<HTMLInputElement>(null);
  const grab = useRef(false);
  const swallowUp = useRef(false);
  const tool = useUi((s) => s.tool);
  const startTool = useRef(tool);

  // The tool that began it put away (another tool, Esc in the view): the drag goes too.
  useEffect(() => { if (spec) startTool.current = useUi.getState().tool; }, [spec]);
  useEffect(() => { if (spec && tool !== startTool.current) endAxisDrag(); }, [tool, spec]);
  useEffect(() => () => endAxisDrag(), []);
  useEffect(() => { if (spec) requestAnimationFrame(() => input.current?.focus()); }, [spec, axis]);

  useEffect(() => {
    if (!spec) return;
    // A drag begins clean: the release of the click that applied the last one may never have come here.
    grab.current = false;
    swallowUp.current = false;
    const inside = (e: PointerEvent) => {
      const r = canvas.current?.getBoundingClientRect();
      return !!r && e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    };
    const rayOf = (e: PointerEvent) => {
      const c = camera.current, r = canvas.current?.getBoundingClientRect();
      if (!c || !r) return null;
      c.updateMatrixWorld();
      const ray = new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), c);
      return ray.ray;
    };
    const follow = (e: PointerEvent) => {
      const st = useAxisDrag.getState();
      if (!st.spec || (st.text && parseLength(st.text) != null)) return;
      const ray = rayOf(e);
      if (!ray) return;
      const t = alongLine(st.spec.at, st.spec.axes[st.axis].dir, ray.origin, ray.direction);
      if (t != null) useAxisDrag.setState({ distance: snapDistance(t, e.shiftKey, st.spec.min) });
    };
    const single = spec.axes.length === 1;
    const onMove = (e: PointerEvent) => {
      if (single ? e.buttons !== 0 : !grab.current) return;
      follow(e);
      // The view's own hover (the face under the cursor) waits: the face picked stays lit.
      if (inside(e)) e.stopPropagation();
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !inside(e)) return;
      const target = e.target as Element | null;
      if (target?.closest?.('.axis-drag-chip, .axis-drag-value')) return;
      const handle = target?.closest?.('[data-axis]');
      if (single) {
        e.stopPropagation(); e.preventDefault();
        swallowUp.current = true;
        void commitAxisDrag(typed());
        return;
      }
      if (handle) {
        e.stopPropagation(); e.preventDefault();
        grab.current = true;
        useAxisDrag.setState({ axis: Number(handle.getAttribute('data-axis')), text: '', distance: 0 });
      }
    };
    const onUp = (e: PointerEvent) => {
      if (swallowUp.current) { swallowUp.current = false; e.stopPropagation(); return; }
      if (!grab.current) return;
      grab.current = false;
      e.stopPropagation();
      void commitAxisDrag(typed());
    };
    // Keys typed while the value is not focused (after a click on the chip): a value's characters go to it, Enter applies, Esc drops.
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement | null;
      if (el === input.current || (el?.closest?.('input, textarea, select, [contenteditable]') && !el.closest('.axis-drag-chip'))) return;
      if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); void commitAxisDrag(typed()); }
      else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cancelAxisDrag(); }
      else if (/^[0-9.,+\-'"a-z]$/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey) input.current?.focus();
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
  const rect = canvas.current?.getBoundingClientRect();
  const toScreen = (p: Vec3): Pt | null => {
    const c = camera.current;
    if (!c || !rect) return null;
    c.updateMatrixWorld();
    const v = new THREE.Vector3(p.x, p.y, p.z).project(c);
    if (v.z > 1 || v.z < -1) return null;
    return { x: ((v.x + 1) / 2) * rect.width, y: ((1 - v.y) / 2) * rect.height };
  };
  const typedValue = text ? parseLength(text) : null;
  const d = typedValue ?? distance;
  const along = spec.axes[axis].dir;
  const by = add({ x: 0, y: 0, z: 0 }, along, d);
  const pathOf = (pts: Vec3[], close = true) => {
    const s = pts.map(toScreen);
    if (s.some((p) => !p)) return null;
    return s.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('') + (close ? 'Z' : '');
  };

  // What it will make, on the model.
  const ghost: React.ReactNode[] = [];
  const pv = spec.preview;
  if (pv.kind === 'prism' && Math.abs(d) > 1e-6) {
    const color = d > 0 ? ADD : CUT;
    const top = pv.outline.map((p) => add(p, by));
    pv.outline.forEach((p, i) => {
      const q = pv.outline[(i + 1) % pv.outline.length];
      const side = pathOf([p, q, add(q, by), add(p, by)]);
      if (side) ghost.push(<path key={`s${i}`} d={side} fill={color} fillOpacity={0.14} stroke={color} strokeWidth={1} strokeOpacity={0.7} />);
    });
    const cap = pathOf(top);
    if (cap) ghost.push(<path key="cap" d={cap} fill={color} fillOpacity={0.32} stroke={color} strokeWidth={2} />);
  }
  if (pv.kind === 'prism' || pv.kind === 'plane') {
    const base = pathOf(pv.outline);
    if (base) ghost.push(<path key="base" d={base} fill="none" stroke="var(--accent, #b5532d)" strokeWidth={1.5} strokeDasharray={pv.kind === 'plane' ? '5 4' : undefined} />);
  }
  if (pv.kind === 'plane') {
    const cut = pathOf(pv.outline.map((p) => add(p, by)));
    if (cut) ghost.push(<path key="plane" d={cut} fill={PLANE} fillOpacity={0.3} stroke={PLANE} strokeWidth={2} />);
  }
  if (pv.kind === 'box') {
    boxEdges3(pv.min, pv.max).forEach(([a, b], i) => { const l = pathOf([a, b], false); if (l) ghost.push(<path key={`o${i}`} d={l} stroke="var(--accent, #b5532d)" strokeOpacity={0.45} strokeDasharray="4 4" />); });
    if (Math.abs(d) > 1e-6) boxEdges3(pv.min, pv.max, by).forEach(([a, b], i) => { const l = pathOf([a, b], false); if (l) ghost.push(<path key={`m${i}`} d={l} stroke={ADD} strokeWidth={2} />); });
  }

  // The arrows: from the handles' place carried by the distance, each the way its axis grows.
  const base = add(spec.at, by);
  const from = toScreen(spec.at), at = toScreen(base);
  const arrows = spec.axes.map((ax, i) => {
    const o = at;
    const tip = o && toScreen(add(base, ax.dir, 0.01));
    if (!o || !tip) return null;
    let dx = tip.x - o.x, dy = tip.y - o.y;
    const len = Math.hypot(dx, dy);
    // An axis seen end on has no arrow to drag.
    if (len < 1e-6) return null;
    dx /= len; dy /= len;
    const L = 58;
    const p = (s: number, n: number) => `${(o.x + dx * s - dy * n).toFixed(1)},${(o.y + dy * s + dx * n).toFixed(1)}`;
    const color = ax.color ?? 'var(--accent, #b5532d)';
    const on = axis === i;
    return (
      <g key={i} data-axis={i} className={`axis-handle${on ? ' active' : ''}`} style={{ color }}>
        <line x1={o.x} y1={o.y} x2={o.x + dx * L} y2={o.y + dy * L} className="axis-hit" />
        <line x1={o.x} y1={o.y} x2={o.x + dx * (L - 12)} y2={o.y + dy * (L - 12)} stroke="currentColor" strokeWidth={on ? 3 : 2.2} />
        <polygon points={`${p(L, 0)} ${p(L - 14, 6.5)} ${p(L - 14, -6.5)}`} fill="currentColor" />
        {ax.name && <text x={o.x + dx * (L + 10)} y={o.y + dy * (L + 10)} className="axis-name" fill="currentColor">{ax.name}</text>}
      </g>
    );
  });

  // The distance: a dimension line from where it started to where it is, and the value at its end.
  const dim = from && at && Math.hypot(at.x - from.x, at.y - from.y) > 2;
  const shown = `${d > 0 && pv.kind === 'prism' ? '+' : ''}${formatLength(d)}`;
  const labelAt = at ?? from;
  const commitTyped = () => void commitAxisDrag(typed());

  return (
    <>
      <svg className="grip-overlay axis-drag">
        {ghost}
        {dim && <line x1={from!.x} y1={from!.y} x2={at!.x} y2={at!.y} className="axis-dim" />}
        {from && <circle cx={from.x} cy={from.y} r={3.5} className="axis-origin" />}
        {arrows}
      </svg>
      {labelAt && (
        <div className="axis-drag-value" style={{ left: labelAt.x + 18, top: labelAt.y - 14 }}>
          <input ref={input} value={text} placeholder={shown} aria-label={`${spec.title}: distance`} spellCheck={false}
            className={typedValue == null && text ? 'bad' : undefined}
            onChange={(e) => useAxisDrag.setState({ text: e.target.value })}
            onKeyDown={(e) => {
              if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); commitTyped(); }
              else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); e.nativeEvent.stopImmediatePropagation(); cancelAxisDrag(); }
              else if (e.key === 'Tab' && spec.axes.length > 1) { e.preventDefault(); useAxisDrag.setState((s) => ({ axis: (s.axis + 1) % spec.axes.length, distance: 0 })); }
            }} />
          {spec.axes[axis]?.name && <span className="axis-drag-axis" style={{ color: spec.axes[axis].color }}>{spec.axes[axis].name}</span>}
        </div>
      )}
      <div className="axis-drag-chip mode-chip">
        <span>{spec.title}</span>
        {spec.hint && <span className="mode-hint">{spec.hint}</span>}
        <span className="mode-hint">{spec.axes.length === 1 ? 'Move the mouse or type a distance · click or Enter applies · Esc cancels' : 'Drag an arrow or type a distance · Tab: next axis · Enter applies · Esc cancels'}</span>
        {spec.options && (
          <span className="axis-drag-options" title={spec.options.label}>
            {spec.options.values.map((v) => (
              <button key={v.value} type="button" className="mode-toggle" aria-pressed={option === v.value} onClick={() => { useAxisDrag.setState({ option: v.value }); input.current?.focus(); }}>{v.label}</button>
            ))}
          </span>
        )}
        <button type="button" onClick={commitTyped} title="Apply (Enter)"><Check size={12} />Apply</button>
        <button type="button" className="mode-close" title="Cancel (Esc)" aria-label="Cancel" onClick={cancelAxisDrag}><X size={12} /></button>
      </div>
    </>
  );

  function typed(): number | null {
    const t = useAxisDrag.getState().text;
    return t ? parseLength(t) : null;
  }
}
