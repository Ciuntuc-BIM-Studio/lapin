/**
 * A drawing's pens in SVG: fills, hatches, then lines, each pen one path.
 * Widths and dashes are paper millimetres at the view's scale, so a plan at
 * 1:100 zoomed to paper size looks as it prints.
 */

import { memo, useId, useMemo } from 'react';
import { useSession } from '../engine/session';
import { screenWidth, useDisplay } from '../app/display';
import { screenFill, screenInk, type DrawingDto, type PenDto } from './drawing';
import { imageUrl, visibleCrop } from './imageCrop';

type ToScreen = (p: { x: number; y: number }) => { x: number; y: number };

/**
 * `onDark`: drawn straight on a dark background (a plan in the graphite theme), so black pens show in the theme's ink.
 * `minText`: texts smaller on screen (px) are left out; a sheet or schedule keeps its tables' words down to a pixel, so a
 * table seen whole still shows it is filled (report #75: a takeoff on an A1 sheet looked empty at fit).
 */
export const DrawingLayer = memo(function DrawingLayer({ drawing, toScreen, pxPerMetre, onDark = false, minText = 3, screen, turnDeg = 0 }: { drawing: DrawingDto; toScreen: ToScreen; pxPerMetre: number; onDark?: boolean; minText?: number; screen?: { w: number; h: number }; turnDeg?: number }) {
  // Pixels per paper millimetre: a paper mm is scale/1000 m of the model.
  const pxPerMm = pxPerMetre * (drawing.scale ?? 100) / 1000;
  // Thin lines (TL): every pen one pixel on screen, sheets and PDFs keeping theirs.
  const thin = useDisplay((s) => s.thinLines);
  const cap = drawing.lineCap === 'square' ? 'square' : 'round';
  const paths = useMemo(() => drawing.pens.map((p, i) => {
    const stroke = screenInk(p.stroke, onDark), fill = screenFill(p.fill, onDark);
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
    const width = p.stroke ? screenWidth(p.width, pxPerMm, thin) : 0;
    if (p.images?.length) return <PenImages key={i} pen={p} toScreen={toScreen} screen={screen} />;
    if (p.texts?.length) {
      return (
        <g key={i} className="pen-text" fill={stroke ?? '#000'}>
          {p.texts.map((t, k) => {
            const at = toScreen({ x: t.x, y: t.y });
            const size = t.height * pxPerMm;
            if (size < minText) return null;
            return (
              <text key={k} x={at.x} y={at.y} fontSize={size} fontWeight={t.bold ? 700 : 400} fontStyle={t.italic ? 'italic' : undefined}
                fontFamily={t.font === 'serif' ? 'ui-serif, Georgia, serif' : t.font === 'mono' ? 'ui-monospace, Menlo, monospace' : 'ui-sans-serif, system-ui, sans-serif'}
                textAnchor={t.anchor === 'start' ? 'start' : t.anchor === 'end' ? 'end' : 'middle'}
                dominantBaseline={t.baseline === 'middle' ? 'central' : 'alphabetic'}
                transform={t.angle || turnDeg ? `rotate(${-(t.angle ?? 0) - turnDeg} ${at.x} ${at.y})` : undefined}>{t.text}</text>
            );
          })}
        </g>
      );
    }
    return (
      <path key={i} d={d} className={`pen pen-${p.layer.toLowerCase()}`} fill={fill ?? 'none'} fillRule="evenodd"
        stroke={stroke ?? 'none'} strokeWidth={width} strokeLinecap={cap} strokeLinejoin={cap === 'square' ? 'miter' : 'round'}
        strokeDasharray={p.dash ? p.dash.map((v) => Math.max(1, Math.min(v * pxPerMm, v * 8)).toFixed(1)).join(' ') : undefined} />
    );
  }), [drawing, cap, toScreen, pxPerMm, onDark, minText, screen, turnDeg]);
  return <g className="drawing-layer">{paths}</g>;
});

/**
 * A pen's pictures, each its image's unit square laid onto its corners, clipped to the pen's box when it has one. On a
 * view that says its screen, a picture far larger than it is cut to what the screen sees (imageCrop.ts, decision 399).
 */
function PenImages({ pen, toScreen, screen }: { pen: PenDto; toScreen: ToScreen; screen?: { w: number; h: number } }) {
  const nodes = useSession((s) => s.nodes);
  const clipId = `pen-clip${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`;
  // The box's four corners: a plan turned to true north turns it too.
  const clip = pen.clip && pen.clip.length === 4 ? [[0, 1], [2, 1], [2, 3], [0, 3]].map(([i, j]) => toScreen({ x: pen.clip![i], y: pen.clip![j] })) : null;
  return (
    <g className="pen-image" clipPath={clip ? `url(#${clipId})` : undefined}>
      {clip && (
        <clipPath id={clipId}>
          <polygon points={clip.map((q) => `${q.x},${q.y}`).join(' ')} />
        </clipPath>
      )}
      {(pen.images ?? []).map((img, k) => {
        // A point cloud's slice carries its own picture (decision 396); an orthophoto or a traced image is a LapinImage of the model.
        const n = img.data ? undefined : nodes.get(img.id);
        const data = img.data ?? n?.props.data, mime = img.mime ?? n?.props.mime;
        if (typeof data !== 'string' || typeof mime !== 'string' || img.corners.length < 8) return null;
        const c = img.corners;
        const [tl, tr, bl] = [toScreen({ x: c[0], y: c[1] }), toScreen({ x: c[2], y: c[3] }), toScreen({ x: c[6], y: c[7] })];
        const crop = screen ? visibleCrop(tl, tr, bl, screen.w, screen.h) : 'all';
        if (!crop) return null;
        const matrix = `matrix(${tr.x - tl.x} ${tr.y - tl.y} ${bl.x - tl.x} ${bl.y - tl.y} ${tl.x} ${tl.y})`;
        const href = imageUrl(img.id, data, mime);
        const picture = <image href={href} x={0} y={0} width={1} height={1} preserveAspectRatio="none" opacity={img.opacity === 1 ? undefined : img.opacity} className="plan-orthophoto" style={img.data ? { imageRendering: 'pixelated' } : undefined} />;
        if (crop === 'all') return <g key={k} transform={matrix}>{picture}</g>;
        // Only what the screen sees: a viewport over the part, in the picture's own square.
        const { u0, v0, u1, v1 } = crop;
        return (
          <g key={k} transform={matrix} data-cropped="1">
            <svg x={u0} y={v0} width={u1 - u0} height={v1 - v0} viewBox={`${u0} ${v0} ${u1 - u0} ${v1 - v0}`} preserveAspectRatio="none" overflow="hidden">{picture}</svg>
          </g>
        );
      })}
    </g>
  );
}
