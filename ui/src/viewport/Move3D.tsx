/**
 * Move and Copy in the 3D view (docs/plan-3d-modelling.md, stage A; testers'
 * report #104; the state and rules in moveIn3d.ts, the snaps in snap3d.ts):
 * the base point, then where it goes, each snapped to the bodies' endpoints,
 * midpoints, edges and faces, or the red, green and blue axes from the base;
 * the selection is carried on the screen as the cursor moves. A value typed at
 * any time (a length along the cursor's way, or dx,dy[,dz]), Enter applies it;
 * → ← ↑ lock an axis, Shift holds the one inferred; Esc cancels. Pressing on a
 * selected element and dragging moves it the same way (View → Drag to move in
 * 3D), from the snapped point under the press.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { X } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { useDisplay } from '../app/display';
import { claimValue } from '../app/valueOwner';
import { activeStorey, execute, useSession } from '../engine/session';
import { num } from '../engine/model';
import { pickClick } from '../platform/assemblies';
import { shownSelection } from '../platform/groups';
import { useCamera, type Vec3 } from './camera';
import { useCad } from './cad/session';
import { formatLength, parseLength } from './liveInput';
import { beginMove3d, carriedWith, endMove3d, moveArgs, moves, typedTarget, useMove3d } from './moveIn3d';
import { hitAt, insideView, projector, rayAt, snapAt, type ViewRefs } from './scene3d';
import { arrowLock3 } from './snap3d';

const sub = (a: Vec3, b: Vec3): Vec3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });

export function Move3D({ camera, canvas, scene, invalidate }: ViewRefs & { invalidate: React.RefObject<() => void> }) {
  useCamera();
  const st = useMove3d();
  const active = st.ids.length > 0;
  const tool = useUi((s) => s.tool);
  const command = useCad((s) => s.command);
  const dragOn = useDisplay((d) => d.dragMove3d);
  const refs: ViewRefs = { camera, canvas, scene };
  const refsRef = useRef(refs);
  refsRef.current = refs;
  const level = () => num(activeStorey() ?? undefined, 'elevation');
  /** Shift held the lock: letting Shift go lets it go. */
  const shiftLock = useRef(false);

  // What is carried on the screen: the elements moved, their openings and fills, with where they stood.
  const carried = useRef(new Map<THREE.Object3D, THREE.Vector3>());
  const carriedIds = useRef<Set<string>>(new Set());
  const restore = () => {
    for (const [o, p] of carried.current) o.position.copy(p);
    carried.current.clear();
    invalidate.current?.();
  };
  const carry = (by: Vec3 | null) => {
    const s = scene.current;
    if (!s) return;
    if (!carried.current.size) {
      s.traverse((o) => {
        const id = o.userData?.nodeId as string | undefined;
        if (!id || !carriedIds.current.has(id)) return;
        // The outermost of the element's objects only: its parts and levels of detail ride on it.
        for (let p = o.parent; p; p = p.parent) if (carriedIds.current.has(p.userData?.nodeId as string)) return;
        carried.current.set(o, o.position.clone());
      });
    }
    for (const [o, p] of carried.current) o.position.set(p.x + (by?.x ?? 0), p.y + (by?.y ?? 0), p.z + (by?.z ?? 0));
    invalidate.current?.();
  };

  useEffect(() => {
    if (!active) { restore(); return; }
    const { nodes, relations } = useSession.getState();
    carriedIds.current = carriedWith(shownSelection(st.ids, nodes, relations), relations.values());
    return () => restore();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, st.ids]);
  // Another tool or a command of the command line ends it.
  // (The MOVE that began it is still the command for a moment: only another one ends it.)
  const began = useRef<string | null>(null);
  useEffect(() => { began.current = active ? useCad.getState().command : null; }, [active]);
  useEffect(() => { if (active && (tool || (command && command !== began.current))) endMove3d(); }, [tool, command, active]);
  useEffect(() => () => endMove3d(), []);

  /** Where it would go now: the typed value, else the cursor. */
  const target = (): Vec3 | null => {
    const s = useMove3d.getState();
    if (!s.base) return null;
    const typed = s.text ? typedTarget(s.text, s.base, s.at?.point ?? null, (x) => parseLength(x)) : null;
    return typed ?? s.at?.point ?? null;
  };
  const apply = async (to: Vec3 | null) => {
    const s = useMove3d.getState();
    if (!s.base || !to) return;
    const by = sub(to, s.base);
    if (!moves(by)) { if (!s.copy) endMove3d(); return; }
    const ids = s.ids, copy = s.copy;
    if (copy) useMove3d.setState({ text: '' }); else endMove3d();
    const r = await execute('element.transform', moveArgs(ids, copy, by)) as { kept?: string[] } | null;
    restore();
    if (r?.kept?.length) notify('Moved in plan only: what has no height of its own keeps it.');
  };
  const applyRef = useRef(apply);
  applyRef.current = apply;

  // The value's keys are the move's once the base is down (decision 378).
  const waiting = active && !!st.base;
  useEffect(() => {
    if (!waiting) return;
    return claimValue({ text: () => useMove3d.getState().text, set: (text) => useMove3d.setState({ text }), enter: () => void enterRef.current() });
  }, [waiting]);
  const enterRef = useRef<() => void>(() => undefined);
  enterRef.current = () => {
    const s = useMove3d.getState();
    if (s.text.trim()) { void applyRef.current(target()); return; }
    if (s.copy) endMove3d();
  };

  useEffect(() => {
    if (!active) return;
    let frame = 0;
    let last: { x: number; y: number } | null = null;
    const follow = () => {
      frame = 0;
      if (!last) return;
      const s = useMove3d.getState();
      const at = snapAt(refsRef.current, last.x, last.y, { level: level(), base: s.base, lock: s.lock, skip: s.base ? carriedIds.current : undefined });
      useMove3d.setState({ at });
      const to = target();
      carry(s.base && to ? sub(to, s.base) : null);
    };
    const onMove = (e: PointerEvent) => {
      if (!insideView(canvas.current, e.clientX, e.clientY)) return;
      const s = useMove3d.getState();
      if (e.buttons !== 0 && !s.drag) return;
      last = { x: e.clientX, y: e.clientY };
      if (s.drag) e.stopPropagation();
      if (!frame) frame = requestAnimationFrame(follow);
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || !insideView(canvas.current, e.clientX, e.clientY)) return;
      if ((e.target as Element | null)?.closest?.('.mode-chip, .axis-drag-value, input, button')) return;
      e.stopPropagation(); e.preventDefault();
      const s = useMove3d.getState();
      if (s.drag) return;
      if (!s.base) {
        const at = snapAt(refsRef.current, e.clientX, e.clientY, { level: level() });
        if (at) useMove3d.setState({ base: at.point, at, lock: null, text: '' });
        return;
      }
      void applyRef.current(target());
    };
    const onUp = (e: PointerEvent) => {
      if (e.button !== 0) return;
      const s = useMove3d.getState();
      if (!s.drag) { if (insideView(canvas.current, e.clientX, e.clientY)) e.stopPropagation(); return; }
      e.stopPropagation();
      void applyRef.current(target());
    };
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement | null)?.closest?.('input, textarea, select, [contenteditable]') && !(e.target as HTMLElement).closest('[data-move3d]')) return;
      const s = useMove3d.getState();
      if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); if (s.text) useMove3d.setState({ text: '' }); else endMove3d(); return; }
      if (e.key === 'Shift' && !e.repeat && s.base && !s.lock && s.at?.color) { shiftLock.current = true; useMove3d.setState({ lock: { axis: s.at.color } }); return; }
      if (s.base) {
        const l = arrowLock3(e.key, s.lock);
        if (l !== undefined) { e.preventDefault(); e.stopImmediatePropagation(); shiftLock.current = false; useMove3d.setState({ lock: l }); if (!frame && last) frame = requestAnimationFrame(follow); }
      }
    };
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.key === 'Shift' && shiftLock.current) { shiftLock.current = false; useMove3d.setState({ lock: null }); }
    };
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    window.addEventListener('keyup', onKeyUp, true);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointerup', onUp, true);
      window.removeEventListener('keydown', onKey, true);
      window.removeEventListener('keyup', onKeyUp, true);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, canvas]);

  // Dragging a selected element by its body: pressed on it, moved past a few pixels, it moves (letting go applies).
  useEffect(() => {
    if (active || !dragOn) return;
    let press: { x: number; y: number; id: string; pointer: number; mods: { add: boolean; leaf: boolean } } | null = null;
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || e.shiftKey || e.altKey || e.pointerType === 'touch') return;
      if (e.target !== canvas.current?.querySelector('canvas')) return;
      const ui = useUi.getState();
      if (ui.tool || useCad.getState().step || !ui.selected.length) return;
      const ray = rayAt(refsRef.current, e.clientX, e.clientY);
      const hit = ray ? hitAt(scene.current, ray) : null;
      const { nodes, relations } = useSession.getState();
      if (!hit || !shownSelection(ui.selected, nodes, relations).has(hit.nodeId)) return;
      // The orbit does not start: the press is the selection's.
      e.stopPropagation(); e.preventDefault();
      press = { x: e.clientX, y: e.clientY, id: hit.nodeId, pointer: e.pointerId, mods: { add: false, leaf: e.ctrlKey || e.metaKey } };
    };
    const onMove = (e: PointerEvent) => {
      if (!press || e.pointerId !== press.pointer) return;
      if (Math.hypot(e.clientX - press.x, e.clientY - press.y) < 5) return;
      const at = snapAt(refsRef.current, press.x, press.y, { level: level() });
      press = null;
      if (!at) return;
      e.stopPropagation();
      beginMove3d(useUi.getState().selected, false, at.point, true);
    };
    const onUp = (e: PointerEvent) => {
      if (!press || e.pointerId !== press.pointer) return;
      // A click on a selected element (no drag): picked as a click picks.
      const p = press;
      press = null;
      e.stopPropagation();
      pickClick(p.id, p.mods);
    };
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointermove', onMove, true);
    window.addEventListener('pointerup', onUp, true);
    return () => {
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('pointermove', onMove, true);
      window.removeEventListener('pointerup', onUp, true);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, dragOn, canvas, scene]);

  if (!active) return null;
  const project = projector(refs);
  const to = target();
  const at = st.at;
  const cur = at ? project(at.point) : null;
  const base = st.base ? project(st.base) : null;
  const end = to ? project(to) : null;
  const guide = at?.guide ? [project(at.guide.from), project(at.guide.to)] : null;
  const from = at?.from && at.point ? [project(at.from), project(at.point)] : null;
  const by = st.base && to ? sub(to, st.base) : null;
  const label = by ? formatLength(Math.hypot(by.x, by.y, by.z)) : '';
  const markClass = at?.kind === 'Endpoint' ? 'sketch-end' : at?.kind === 'Midpoint' ? 'sketch-mid' : at?.kind === 'On edge' ? 'sketch-edge' : 'face-sketch-cursor';
  const title = st.copy ? 'Copy' : 'Move';
  const hint = !st.base ? 'Click the base point: a corner, an edge, a face' : st.drag ? 'Let go where it goes · → ← ↑ lock an axis · Esc cancels'
    : st.copy ? 'Click where each copy goes, or type a length or dx,dy,dz · Enter or Esc ends' : 'Click where it goes, or type a length or dx,dy,dz · Esc cancels';

  return (
    <>
      <svg className="grip-overlay axis-drag face-sketch" data-move3d="overlay">
        {guide?.[0] && guide[1] && <line x1={guide[0].x} y1={guide[0].y} x2={guide[1].x} y2={guide[1].y} className={`sketch-guide ${at!.guide!.color}`} />}
        {from?.[0] && from[1] && <line x1={from[0].x} y1={from[0].y} x2={from[1].x} y2={from[1].y} className="sketch-guide magenta" strokeDasharray="3 3" />}
        {base && end && <line x1={base.x} y1={base.y} x2={end.x} y2={end.y} className="axis-dim" />}
        {base && <circle cx={base.x} cy={base.y} r={4} className="axis-origin" />}
        {cur && <circle cx={cur.x} cy={cur.y} r={at && at.kind !== 'On face' && at.kind !== 'On level' ? 5 : 3} className={markClass} />}
      </svg>
      {cur && at && <div className={`face-sketch-snap-name ${at.color ?? ''}`} style={{ left: cur.x + 10, top: cur.y + 8 }} data-move3d="inference">{at.kind}</div>}
      {cur && st.base && (
        <div className="axis-drag-value" style={{ left: cur.x + 18, top: cur.y - 30 }} data-move3d="value">
          <input value={st.text} placeholder={label || '…'} aria-label="Measurements" spellCheck={false} data-move3d="vcb"
            className={st.text && !typedTarget(st.text, st.base, at?.point ?? null, (x) => parseLength(x)) ? 'bad' : undefined}
            onChange={(e) => useMove3d.setState({ text: e.target.value })}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); enterRef.current(); } }} />
        </div>
      )}
      <div className="axis-drag-chip mode-chip" data-move3d="chip">
        <span>{title}</span>
        <span className="mode-hint">{hint}</span>
        {st.base && <span className="mode-hint">{st.lock ? 'Locked: the arrow again or Shift lets go' : '→ red · ← green · ↑ blue · Shift holds'}</span>}
        <button type="button" className="mode-close" title="Cancel (Esc)" aria-label="Cancel" onClick={endMove3d}><X size={12} /></button>
      </div>
    </>
  );
}
