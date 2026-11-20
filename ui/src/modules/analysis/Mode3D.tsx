/**
 * A mode shape in the 3D view (docs/plan-fem.md §11.5): the bars' displaced axes and the shells' displaced meshes the engine gave
 * (analysis.mode, already scaled, in world coordinates), swung between their rest and displaced positions by sin(ωt) in a loop of
 * a second and a half (not real time). It takes no ray, as the shells' contours do. With reduced motion asked for, the displaced
 * shape is shown still.
 */

import { Square } from 'lucide-react';
import { useEffect, useState } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { stopMode, useMode } from './useMode';
import { blend, formatHz, formatPercent, formatPeriod, oscillation, restPoints, type ModeShapeDto } from './seismic';
import { triangles } from './shells';

const COLOUR = 0xe8590c;

interface Built { group: THREE.Group; apply: (k: number) => void; dispose: () => void }

/** The mode's objects of the scene and the function that sets them at a moment of the swing (k from −1 to 1). */
export function modeGroup(shape: ModeShapeDto): Built {
  const group = new THREE.Group();
  group.name = 'analysis-mode';
  const lineMat = new THREE.LineBasicMaterial({ color: COLOUR });
  const shellMat = new THREE.MeshBasicMaterial({ color: COLOUR, transparent: true, opacity: 0.45, side: THREE.DoubleSide, depthWrite: false });
  const updates: ((k: number) => void)[] = [];
  const geometries: THREE.BufferGeometry[] = [];
  for (const pl of shape.polylines) {
    const displaced = pl.points.flat();
    const rest = restPoints(pl.base, pl.points.length);
    const g = new THREE.BufferGeometry();
    const pos = new THREE.BufferAttribute(new Float32Array(rest.length), 3);
    g.setAttribute('position', pos);
    const line = new THREE.Line(g, lineMat);
    line.raycast = () => undefined;
    line.frustumCulled = false;
    line.renderOrder = 3;
    group.add(line);
    geometries.push(g);
    updates.push((k) => { blend(rest, displaced, k, pos.array as Float32Array); pos.needsUpdate = true; });
  }
  for (const s of shape.shells) {
    const g = new THREE.BufferGeometry();
    const pos = new THREE.BufferAttribute(new Float32Array(s.positions.length), 3);
    g.setAttribute('position', pos);
    g.setIndex(triangles(s.faces).flat());
    const mesh = new THREE.Mesh(g, shellMat);
    mesh.raycast = () => undefined;
    mesh.frustumCulled = false;
    mesh.renderOrder = 3;
    group.add(mesh);
    geometries.push(g);
    updates.push((k) => { blend(s.positions, s.displaced, k, pos.array as Float32Array); pos.needsUpdate = true; });
  }
  const apply = (k: number) => { for (const u of updates) u(k); };
  apply(0);
  return { group, apply, dispose: () => { for (const g of geometries) g.dispose(); lineMat.dispose(); shellMat.dispose(); } };
}

export function Mode3D({ canvas, scene, invalidate }: Overlay3DProps) {
  const shape = useMode((s) => s.shape);
  // The view makes its scene after its overlays first mount: wait for it.
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (ready) return;
    let frame = 0;
    const look = () => { if (scene?.current) setReady(true); else frame = requestAnimationFrame(look); };
    look();
    return () => cancelAnimationFrame(frame);
  }, [scene, ready]);
  useEffect(() => {
    const s = scene?.current;
    if (!ready || !s || !shape) return;
    const built = modeGroup(shape);
    s.add(built.group);
    const host = canvas.current;
    if (host) host.dataset.mode = String(shape.mode);
    const still = typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let frame = 0;
    const start = performance.now();
    const tick = (now: number) => {
      built.apply(oscillation(now - start));
      invalidate?.current?.();
      frame = requestAnimationFrame(tick);
    };
    if (still) { built.apply(1); invalidate?.current?.(); } else frame = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(frame);
      s.remove(built.group);
      if (host) delete host.dataset.mode;
      built.dispose();
      invalidate?.current?.();
    };
  }, [ready, canvas, scene, invalidate, shape]);
  return null;
}

/** Over the model view while a mode is shown: which mode, its period and masses, and Stop. */
export function ModeCard() {
  const view = useUi((s) => s.view);
  const shape = useMode((s) => s.shape);
  if (!shape || view === 'sheet' || view === 'schedule') return null;
  return (
    <div className="sel-card analysis-card" role="region" aria-label="Mode shape" data-testid="mode-card">
      <b>Mode shape</b>
      <p className="small" data-no-i18n>{shape.mode} · T = {formatPeriod(shape.T)} s · f = {formatHz(shape.f)} Hz</p>
      <p className="muted small"><span>Effective mass</span> <span data-no-i18n>X {formatPercent(shape.ratio[0]!)} · Y {formatPercent(shape.ratio[1]!)}</span></p>
      <p className="muted small">The shape is scaled to be seen: it shows how the structure moves, not by how much.</p>
      <div className="row">
        <button type="button" className="btn mini" data-testid="mode-stop" onClick={stopMode}><Square size={12} />Stop</button>
        <button type="button" className="btn mini" onClick={() => useUi.setState({ dialog: 'analysisResults' })}>Results…</button>
      </div>
    </div>
  );
}
