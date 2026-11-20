/**
 * The design checks' utilisation drawn on the model (docs/plan-fem.md §12.7): each checked bar a thick line along its analytical
 * axis, each slab and wall a surface laid a hair off its faces, coloured from green (0) through orange (1) to red (over 1), grey
 * where nothing was checked. Screen only, derived from analysis.checks; it takes no ray. Beside it, the legend and the plan overlay.
 */

import { useEffect, useMemo, useState } from 'react';
import * as THREE from 'three';
import { useCamera } from '../../viewport/camera';
import type { Overlay3DProps, PlanOverlayProps } from '../../platform/module';
import { useResults } from './useResults';
import { useChecks } from './useChecks';
import { utilisationBySource, utilisationColour, formatUtilisation, UTILISATION_STEPS } from './checks';
import { triangles } from './shells';

/** The utilisation of each element, only when the checks are of the run shown and the colouring is on. */
function useColours(): Map<string, number | null> | null {
  const colour = useChecks((s) => s.colour);
  const checks = useChecks((s) => s.checks);
  const forRun = useChecks((s) => s.forRun);
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const by = useMemo(() => (checks ? utilisationBySource(checks) : null), [checks]);
  return colour && !stale && by && forRun === run ? by : null;
}

export function Utilisation3D({ camera, canvas, scene, invalidate }: Overlay3DProps) {
  useCamera();
  const by = useColours();
  const diagram = useResults((s) => s.diagram);
  const surfaces = useChecks((s) => s.surfaces);
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (ready) return;
    let frame = 0;
    const look = () => { if (scene?.current) setReady(true); else frame = requestAnimationFrame(look); };
    look();
    return () => cancelAnimationFrame(frame);
  }, [scene, ready]);
  // The slabs and walls, as meshes of the scene.
  useEffect(() => {
    const s = scene?.current;
    if (!ready || !s || !by || !surfaces || surfaces.surfaces.length === 0) return;
    const group = new THREE.Group();
    group.name = 'analysis-utilisation';
    const materials: THREE.Material[] = [];
    for (const surf of surfaces.surfaces) {
      const n = surf.positions.length / 3;
      const off = (surf.thickness ?? 0.2) / 2 + 0.006;
      const [nx, ny, nz] = surf.normal as [number, number, number];
      const pos = new Float32Array(n * 2 * 3);
      for (let i = 0; i < n; i++) {
        for (const [side, k] of [[1, i], [-1, i + n]] as const) {
          pos[k * 3] = surf.positions[i * 3]! + nx * off * side;
          pos[k * 3 + 1] = surf.positions[i * 3 + 1]! + ny * off * side;
          pos[k * 3 + 2] = surf.positions[i * 3 + 2]! + nz * off * side;
        }
      }
      const index: number[] = [];
      for (const [a, b, d] of triangles(surf.faces)) index.push(a, b, d, a + n, d + n, b + n);
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      g.setIndex(index);
      const material = new THREE.MeshBasicMaterial({ color: new THREE.Color(utilisationColour(by.get(surf.sourceId) ?? null)), side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2, toneMapped: false });
      materials.push(material);
      const mesh = new THREE.Mesh(g, material);
      mesh.raycast = () => undefined;
      mesh.renderOrder = 2;
      group.add(mesh);
    }
    s.add(group);
    const host = canvas.current;
    if (host) host.dataset.utilisation = String(group.children.length);
    invalidate?.current?.();
    return () => {
      s.remove(group);
      if (host) delete host.dataset.utilisation;
      group.traverse((o) => { if (o instanceof THREE.Mesh) o.geometry.dispose(); });
      for (const m of materials) m.dispose();
      invalidate?.current?.();
    };
  }, [ready, canvas, scene, invalidate, by, surfaces]);

  // The bars, as lines over the view (the camera projects them as the diagrams are).
  const cam = camera.current, r = canvas.current?.getBoundingClientRect();
  if (!by || !diagram || !cam || !r) return null;
  cam.updateMatrixWorld();
  const project = (p: number[]) => {
    const v = new THREE.Vector3(p[0], p[1], p[2]).project(cam);
    return v.z > 1 || v.z < -1 ? null : { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  return (
    <svg className="grip-overlay analysis-overlay" data-testid="utilisation-overlay" data-bars={diagram.polylines.length}>
      {diagram.polylines.map((pl, k) => {
        const pts = pl.base.map(project);
        if (pts.some((p) => !p)) return null;
        const u = by.get(pl.sourceId) ?? null;
        return <path key={k} d={pts.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('')} className="utilisation-bar" stroke={utilisationColour(u)} data-u={u === null ? '' : u.toFixed(3)} />;
      })}
    </svg>
  );
}

/** The colours' key: green to orange from 0 to 1, red over 1, grey for what was not checked. */
export function UtilisationLegend() {
  const by = useColours();
  if (!by) return null;
  return (
    <div className="contour-legend" data-testid="utilisation-legend">
      <div className="small"><b>Utilisation</b></div>
      <div className="contour-bands">
        <div className="row small contour-band"><span className="contour-swatch" style={{ background: utilisationColour(1.2) }} /><span data-no-i18n>&gt; 1</span></div>
        {[...UTILISATION_STEPS].reverse().map((u) => (
          <div key={u} className="row small contour-band"><span className="contour-swatch" style={{ background: utilisationColour(u) }} /><span data-no-i18n>{formatUtilisation(u)}</span></div>
        ))}
        <div className="row small contour-band"><span className="contour-swatch" style={{ background: utilisationColour(null) }} /><span>Not checked</span></div>
      </div>
      <p className="muted small">Indicative: the checks are yours to verify.</p>
    </div>
  );
}

/** The slabs and bars of a plan's storey, coloured by utilisation (screen only). */
export function UtilisationPlan({ toScreen, view }: PlanOverlayProps) {
  const by = useColours();
  const checks = useChecks((s) => s.checks);
  const surfaces = useChecks((s) => s.surfaces);
  const diagram = useResults((s) => s.diagram);
  if (!by || !checks || !view.storeyId) return null;
  const storeyOf = new Map<string, string | null>();
  for (const m of [...checks.members, ...checks.surfaces, ...checks.walls]) storeyOf.set(m.sourceId, m.storeyId ?? null);
  const slabs = (surfaces?.surfaces ?? []).filter((s) => s.kind === 'slab' && storeyOf.get(s.sourceId) === view.storeyId);
  const bars = (diagram?.polylines ?? []).filter((p) => storeyOf.get(p.sourceId) === view.storeyId);
  if (slabs.length === 0 && bars.length === 0) return null;
  return (
    <g className="analysis-utilisation-plan" data-testid="utilisation-plan">
      {slabs.map((s) => {
        const colour = utilisationColour(by.get(s.sourceId) ?? null);
        const pts = Array.from({ length: s.positions.length / 3 }, (_, i) => { const p = toScreen({ x: s.positions[i * 3]!, y: s.positions[i * 3 + 1]! }); return `${p.x.toFixed(1)} ${p.y.toFixed(1)}`; });
        const d = triangles(s.faces).map(([a, b, c]) => `M${pts[a]}L${pts[b]}L${pts[c]}Z`).join('');
        return <path key={s.id} d={d} fill={colour} stroke={colour} strokeWidth={0.5} opacity={0.6} />;
      })}
      {bars.map((p, k) => {
        const d = p.base.map((q, i) => { const s = toScreen({ x: q[0]!, y: q[1]! }); return `${i ? 'L' : 'M'}${s.x.toFixed(1)} ${s.y.toFixed(1)}`; }).join('');
        return <path key={k} d={d} fill="none" stroke={utilisationColour(by.get(p.sourceId) ?? null)} strokeWidth={5} strokeLinecap="round" />;
      })}
    </g>
  );
}
