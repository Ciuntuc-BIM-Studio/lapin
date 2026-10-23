/**
 * A drawing's pens in SVG: fills, hatches, then lines, each pen one path.
 * Widths and dashes are paper millimetres at the view's scale, so a plan at
 * 1:100 zoomed to paper size looks as it prints.
 */

import { memo, useMemo } from 'react';
import type { DrawingDto } from './drawing';

type ToScreen = (p: { x: number; y: number }) => { x: number; y: number };

export const DrawingLayer = memo(function DrawingLayer({ drawing, toScreen, pxPerMetre }: { drawing: DrawingDto; toScreen: ToScreen; pxPerMetre: number }) {
  // Pixels per paper millimetre: a paper mm is scale/1000 m of the model.
  const pxPerMm = pxPerMetre * (drawing.scale ?? 100) / 1000;
  const paths = useMemo(() => drawing.pens.map((p, i) => {
    let d = '';
    const pt = (x: number, y: number) => { const s = toScreen({ x, y }); return `${s.x.toFixed(1)} ${s.y.toFixed(1)}`; };
    for (const loop of p.loops ?? []) {
      for (let k = 0; k + 1 < loop.length; k += 2) d += `${k === 0 ? 'M' : 'L'}${pt(loop[k], loop[k + 1])}`;
      d += 'Z';
    }
    const s = p.segments ?? [];
    for (let k = 0; k + 3 < s.length; k += 4) {
      const a = pt(s[k], s[k + 1]), b = pt(s[k + 2], s[k + 3]);
      d += a === b ? `M${a}h0.8` : `M${a}L${b}`;
    }
    // True to paper, within what a screen shows well when zoomed far in or out.
    const width = p.stroke ? Math.min(4, Math.max(0.5, p.width * pxPerMm)) : 0;
    return (
      <path key={i} d={d} className={`pen pen-${p.layer.toLowerCase()}`} fill={p.fill ?? 'none'} fillRule="evenodd"
        stroke={p.stroke ?? 'none'} strokeWidth={width} strokeLinecap="round"
        strokeDasharray={p.dash ? p.dash.map((v) => Math.max(1, Math.min(v * pxPerMm, v * 8)).toFixed(1)).join(' ') : undefined} />
    );
  }), [drawing, toScreen, pxPerMm]);
  return <g className="drawing-layer">{paths}</g>;
});
