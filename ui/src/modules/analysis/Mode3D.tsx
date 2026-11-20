/**
 * A mode shape in the 3D view (docs/plan-fem.md §11.5, §11.10): the shells' displaced meshes as shaded surfaces and the bars'
 * displaced axes as shaded tubes, from the engine's shape (analysis.mode, world coordinates), swung between rest and displaced by
 * sin(ωt) in a loop of a second and a half (not real time). The swing is sized here: its largest displacement a share of the
 * model's size (modeAmplitude), times the card's scale. The model at rest is drawn see-through behind it, or hidden. It takes no
 * ray, as the shells' contours do. With reduced motion asked for, the displaced shape is shown still.
 */

import { Square } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { setLooks } from '../../platform/looks';
import { setModeMult, setShowUndeformed, stopMode, useMode } from './useMode';
import {
  blend, formatHz, formatPercent, formatPeriod, ghostLooks, modeAmplitude, oscillation, restPoints, ringAxes, shapeDiagonal, tubeIndex, tubeRings,
  type ModeShapeDto,
} from './seismic';
import { triangles } from './shells';
import { formatValue } from './results';

const COLOUR = 0xe8590c;
const SIDES = 8;

interface Built { group: THREE.Group; apply: (k: number) => void; dispose: () => void }

/** The mode's objects of the scene and the function that sets them at a moment of the swing (k: the multiple of the engine's displacement, −amplitude to amplitude). */
export function modeGroup(shape: ModeShapeDto): Built {
  const group = new THREE.Group();
  group.name = 'analysis-mode';
  const solid = new THREE.MeshLambertMaterial({ color: COLOUR, side: THREE.DoubleSide, flatShading: true, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
  const edges = new THREE.MeshBasicMaterial({ color: 0x7a2e06, wireframe: true, transparent: true, opacity: 0.35, depthWrite: false });
  const updates: ((k: number) => void)[] = [];
  const geometries: THREE.BufferGeometry[] = [];
  const add = (o: THREE.Object3D) => { o.raycast = () => undefined; o.frustumCulled = false; o.renderOrder = 3; group.add(o); };
  const radius = Math.max(0.025, 0.004 * shapeDiagonal(shape));
  for (const pl of shape.polylines) {
    const displaced = pl.points.flat();
    const rest = restPoints(pl.base, pl.points.length);
    const [a, b] = pl.base as [number[], number[]];
    const [u, v] = ringAxes(b[0]! - a[0]!, b[1]! - a[1]!, b[2]! - a[2]!);
    const at = new Float32Array(rest.length);
    const g = new THREE.BufferGeometry();
    const pos = new THREE.BufferAttribute(new Float32Array(pl.points.length * SIDES * 3), 3);
    g.setAttribute('position', pos);
    g.setIndex(tubeIndex(pl.points.length, SIDES));
    add(new THREE.Mesh(g, solid));
    geometries.push(g);
    updates.push((k) => { blend(rest, displaced, k, at); tubeRings(at, u, v, radius, SIDES, pos.array as Float32Array); pos.needsUpdate = true; });
  }
  for (const s of shape.shells) {
    const g = new THREE.BufferGeometry();
    const pos = new THREE.BufferAttribute(new Float32Array(s.positions.length), 3);
    g.setAttribute('position', pos);
    g.setIndex(triangles(s.faces).flat());
    add(new THREE.Mesh(g, solid));
    add(new THREE.Mesh(g, edges));
    geometries.push(g);
    updates.push((k) => { blend(s.positions, s.displaced, k, pos.array as Float32Array); pos.needsUpdate = true; g.computeBoundingSphere(); });
  }
  const apply = (k: number) => { for (const u of updates) u(k); };
  apply(0);
  return { group, apply, dispose: () => { for (const g of geometries) g.dispose(); solid.dispose(); edges.dispose(); } };
}

export function Mode3D({ canvas, scene, invalidate }: Overlay3DProps) {
  const shape = useMode((s) => s.shape);
  const mult = useMode((s) => s.mult);
  const undeformed = useMode((s) => s.undeformed);
  const render = useSession((s) => s.render);
  // The swing's size, read by the running loop (the scale slider does not rebuild the shape).
  const amplitude = useRef(1);
  amplitude.current = shape ? modeAmplitude(shape, mult) : 1;
  // The view makes its scene after its overlays first mount: wait for it.
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (ready) return;
    let frame = 0;
    const look = () => { if (scene?.current) setReady(true); else frame = requestAnimationFrame(look); };
    look();
    return () => cancelAnimationFrame(frame);
  }, [scene, ready]);
  // The model at rest: see-through behind the shape, or hidden.
  const shown = !!shape;
  useEffect(() => {
    if (!shown) return;
    setLooks('analysis-mode', ghostLooks(render.keys(), undeformed));
    const host = canvas.current;
    if (host) host.dataset.modeUndeformed = undeformed ? 'ghost' : 'hidden';
    return () => { setLooks('analysis-mode', {}); if (host) delete host.dataset.modeUndeformed; };
  }, [shown, undeformed, render, canvas]);
  const still = typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const built = useRef<Built | null>(null);
  // Still: the displaced shape at the slider's size, set again when the slider moves.
  useEffect(() => {
    if (still && built.current) { built.current.apply(amplitude.current); invalidate?.current?.(); }
  }, [still, mult, invalidate]);
  useEffect(() => {
    const s = scene?.current;
    if (!ready || !s || !shape) return;
    const b = modeGroup(shape);
    built.current = b;
    s.add(b.group);
    const host = canvas.current;
    if (host) host.dataset.mode = String(shape.mode);
    let frame = 0;
    const start = performance.now();
    const tick = (now: number) => {
      b.apply(oscillation(now - start) * amplitude.current);
      invalidate?.current?.();
      frame = requestAnimationFrame(tick);
    };
    if (still) { b.apply(amplitude.current); invalidate?.current?.(); } else frame = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(frame);
      s.remove(b.group);
      if (host) delete host.dataset.mode;
      b.dispose();
      built.current = null;
      invalidate?.current?.();
    };
  }, [ready, canvas, scene, invalidate, shape, still]);
  return null;
}

/** Over the model view while a mode is shown: which mode, its period and masses, and Stop. */
export function ModeCard() {
  const view = useUi((s) => s.view);
  const shape = useMode((s) => s.shape);
  const mult = useMode((s) => s.mult);
  const undeformed = useMode((s) => s.undeformed);
  if (!shape || view === 'sheet' || view === 'schedule') return null;
  return (
    <div className="sel-card analysis-card" role="region" aria-label="Mode shape" data-testid="mode-card">
      <b>Mode shape</b>
      <p className="small" data-no-i18n>{shape.mode} · T = {formatPeriod(shape.T)} s · f = {formatHz(shape.f)} Hz</p>
      <p className="muted small"><span>Effective mass</span> <span data-no-i18n>X {formatPercent(shape.ratio[0]!)} · Y {formatPercent(shape.ratio[1]!)}</span></p>
      <p className="muted small">The shape is scaled to be seen: it shows how the structure moves, not by how much.</p>
      <label className="row small" title="The swing's size on the view; the model is not changed">
        <span>Scale</span>
        <input type="range" min={-2} max={2} step={0.1} value={Math.log2(mult)} aria-label="Mode shape scale" data-testid="mode-scale" onChange={(e) => setModeMult(2 ** Number(e.target.value))} />
        <span data-no-i18n>×{formatValue(mult)}</span>
      </label>
      <label className="row small" title="The model at rest drawn see-through behind the shape; off: hidden">
        <input type="checkbox" checked={undeformed} data-testid="mode-undeformed" onChange={(e) => setShowUndeformed(e.target.checked)} />Show undeformed
      </label>
      <div className="row">
        <button type="button" className="btn mini" data-testid="mode-stop" onClick={stopMode}><Square size={12} />Stop</button>
        <button type="button" className="btn mini" onClick={() => useUi.setState({ dialog: 'analysisResults' })}>Results…</button>
      </div>
    </div>
  );
}
