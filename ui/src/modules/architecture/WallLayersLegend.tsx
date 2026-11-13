/**
 * The Wall types dialog's legend (tester's report #79): the wall in plan as a
 * selected one shows it, its axis with an arrowhead the way it was drawn and
 * its faces' letters (R on the right, L on the left: viewport/faceMarks.ts),
 * and its layers as bands numbered and ordered as the table's rows (forum #45:
 * 1 on top, on the R face, as the list reads top-down; the drawn direction
 * points left so the right-hand face is the top one, truthfully), the
 * core (which carries the axis) in the accent colour. R is the exterior and L
 * the interior of a wall drawn anticlockwise. Not to scale: each band is at
 * least tall enough for its number.
 */

import { t, useLang } from '../../i18n';
import { faceLetters } from '../../viewport/faceMarks';

export interface LegendLayer { thickness: number; core: boolean }

// Its bands are "layer-band": the ribbon's ".band" (height 86px) would size an SVG rect too and run it over the table.
const W = 300, X0 = 34, X1 = 250, MIN = 11, MAX = 64;

/** The bands' heights (px, top to bottom: layer 1 first) scaled to the wall's thickness, each at least {@link MIN}. */
export function legendBands(layers: LegendLayer[]): number[] {
  const total = layers.reduce((s, l) => s + Math.max(0, l.thickness), 0);
  const k = total > 0 ? Math.max(0, MAX - MIN * layers.length) / total : 0;
  return layers.map((l) => MIN + Math.max(0, l.thickness) * k);
}

export function WallLayersLegend({ layers }: { layers: LegendLayer[] }) {
  const letters = faceLetters(useLang((s) => s.lang));
  if (layers.length === 0) return null;
  const heights = legendBands(layers);
  const top = 18, depth = heights.reduce((a, b) => a + b, 0), h = top + depth + 18;
  // The bands from the top (the R face) down: band j is layer j + 1, as the table's rows.
  let y = top;
  const bands = heights.map((bh, j) => { const b = { y, h: bh, i: j }; y += bh; return b; });
  const core = bands.find((b) => layers[b.i].core);
  const axisY = core ? core.y + core.h / 2 : top + depth / 2;
  const ax = X0 + (X1 - X0) * 0.75, fx = X0 + (X1 - X0) * 0.25;
  return (
    <svg className="wall-legend" viewBox={`0 0 ${W} ${h}`} width={W} height={h} role="img" aria-label="Wall layers legend" data-testid="wall-legend">
      {bands.map((b) => (
        <g key={b.i} data-layer={b.i + 1}>
          <rect x={X0} y={b.y} width={X1 - X0} height={b.h} className={layers[b.i].core ? 'layer-band core' : 'layer-band'} />
          <text x={X1 + 6} y={b.y + b.h / 2} className="band-no">{b.i + 1}</text>
        </g>
      ))}
      <line x1={X0 - 14} y1={axisY} x2={X1 + 2} y2={axisY} className="axis" />
      <path d={`M${ax - 7} ${axisY}L${ax + 4} ${axisY - 4.5}L${ax + 4} ${axisY + 4.5}Z`} className="ref-arrow" />
      <path d={`M${X0 - 14} ${axisY}l7 -3.5v7z`} className="ref-arrow" />
      <text x={fx} y={top - 9} className="ref-face" data-face="right">{`${letters.right} · ${t('exterior')}`}</text>
      <text x={fx} y={top + depth + 10} className="ref-face" data-face="left">{`${letters.left} · ${t('interior')}`}</text>
    </svg>
  );
}
