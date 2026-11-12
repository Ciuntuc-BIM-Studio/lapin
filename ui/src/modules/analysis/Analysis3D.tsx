/**
 * The analysis in the 3D view (docs/plan-fem.md §9.6): the diagram the engine drew (analysis.diagram, in world coordinates) as lines
 * over the model: the bar's base line, the curve, hatch ticks between them, the values at the extremes, coloured by sign; and the
 * reactions as arrows with their values. SVG over the canvas, projected by the camera as the guides and reference planes are.
 */

import { Fragment } from 'react';
import * as THREE from 'three';
import { useCamera } from '../../viewport/camera';
import { useResults } from './useResults';
import {
  footOnBase, formatMm, formatValue, hatchIndices, labelIndices, placeLabels, reactionLabel, reactionVector, signColour, SIGN_COLOUR,
  type LabelCandidate, type PolylineDto,
} from './results';

type Px = { x: number; y: number } | null;

export function Analysis3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const { show, stale, diagram, results, reactions } = useResults();
  const cam = camera.current, r = canvas.current?.getBoundingClientRect();
  if (!show || stale || !diagram || !cam || !r) return null;
  cam.updateMatrixWorld();
  const project = (p: number[]): Px => {
    const v = new THREE.Vector3(p[0], p[1], p[2]).project(cam);
    return v.z > 1 || v.z < -1 ? null : { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  const path = (pts: Px[]): string => {
    let d = '', pen = false;
    for (const p of pts) { if (!p) { pen = false; continue; } d += `${pen ? 'L' : 'M'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`; pen = true; }
    return d;
  };
  const deformed = diagram.quantity === 'deformed';
  const text = (v: number) => (deformed ? formatMm(v).replace(' mm', '') : formatValue(v));
  const eps = diagram.max * 1e-4;

  // The values, placed together at the end so that where bars meet they do not print over one another (placeLabels).
  const candidates: (LabelCandidate & { fill: string })[] = [];
  const lines = diagram.polylines.map((pl: PolylineDto, k) => {
    const pts = pl.points.map(project);
    const base = pl.base.map(project);
    const hi = Math.max(...pl.values), lo = Math.min(...pl.values);
    const colour = deformed ? '#2b8a3e' : signColour(Math.abs(hi) >= Math.abs(lo) ? hi : lo, eps);
    const ticks = deformed ? [] : hatchIndices(pl.values);
    const labels = deformed ? labelIndices(pl.values).slice(0, 1) : labelIndices(pl.values);
    for (const i of labels) {
      const a = pts[i];
      if (a) candidates.push({ x: a.x + 4, y: a.y - 4, text: text(pl.values[i]!), weight: Math.abs(pl.values[i]!), fill: deformed ? colour : signColour(pl.values[i]!) });
    }
    return (
      <g key={k} data-bound={pl.bound}>
        <path d={path(base)} className="analysis-base" />
        {ticks.map((i) => {
          const a = pts[i], b = project(footOnBase(pl.base, pl.points[i]!));
          return a && b ? <line key={i} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke={signColour(pl.values[i]!, eps)} className="analysis-hatch" /> : null;
        })}
        <path d={path(pts)} stroke={colour} className="analysis-curve" />
      </g>
    );
  });

  // The reactions: an arrow from the support along the force, the longest a tenth of the model.
  const vs = !reactions || !results ? [] : results.reactions.flatMap((x) => { const f = reactionVector(x); return f ? [{ x, f }] : []; });
  const biggest = Math.max(1e-9, ...vs.map((v) => Math.hypot(v.f[0]!, v.f[1]!, v.f[2]!)));
  const all = diagram.polylines.flatMap((p) => p.base);
  const span = all.length ? Math.max(1, ...[0, 1, 2].map((a) => Math.max(...all.map((p) => p[a]!)) - Math.min(...all.map((p) => p[a]!)))) : 5;
  const arrows = vs.map(({ x, f }, k) => {
    if (Math.hypot(f[0]!, f[1]!, f[2]!) < 1e-6 * biggest) return null;
    const tail = x.at, len = span * 0.1;
    const tip = [tail[0]! + (f[0]! / biggest) * len, tail[1]! + (f[1]! / biggest) * len, tail[2]! + (f[2]! / biggest) * len];
    const a = project(tip), b = project(tail);
    if (!a || !b) return null;
    const ang = Math.atan2(a.y - b.y, a.x - b.x);
    const head = (s: number) => `${(a.x - 9 * Math.cos(ang + s)).toFixed(1)} ${(a.y - 9 * Math.sin(ang + s)).toFixed(1)}`;
    return (
      <Fragment key={`r${k}`}>
        <line x1={b.x} y1={b.y} x2={a.x} y2={a.y} className="analysis-reaction" />
        <path d={`M${head(0.4)}L${a.x.toFixed(1)} ${a.y.toFixed(1)}L${head(-0.4)}`} className="analysis-reaction" />
      </Fragment>
    );
  });
  // The reactions' values go first: a support's force is read before a diagram's value beside it.
  const reactionTexts = vs.flatMap(({ x, f }) => {
    if (Math.hypot(f[0]!, f[1]!, f[2]!) < 1e-6 * biggest) return [];
    const len = span * 0.1;
    const a = project([x.at[0]! + (f[0]! / biggest) * len, x.at[1]! + (f[1]! / biggest) * len, x.at[2]! + (f[2]! / biggest) * len]);
    return a ? [{ x: a.x + 6, y: a.y + 12, text: reactionLabel(f, biggest), weight: Infinity, fill: SIGN_COLOUR.positive }] : [];
  });
  const texts = [...reactionTexts, ...candidates];
  const placed = placeLabels(texts).map((i) => {
    const t = texts[i]!;
    return <text key={`t${i}`} x={t.x} y={t.y} fill={t.fill} data-no-i18n>{t.text}</text>;
  });

  return (
    <svg className="grip-overlay analysis-overlay" data-testid="analysis-overlay" data-quantity={diagram.quantity} data-lines={diagram.polylines.length}>
      {lines}
      {arrows}
      {placed}
      <text className="analysis-unit" x={12} y={22} data-no-i18n>{diagram.quantity} · {deformed ? 'mm' : diagram.unit}{reactions && vs.length > 0 ? ' · kN' : ''}</text>
    </svg>
  );
}
