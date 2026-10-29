/**
 * A drawing's pens in SVG: fills, hatches, then lines, each pen one path.
 * Widths and dashes are paper millimetres at the view's scale, so a plan at
 * 1:100 zoomed to paper size looks as it prints.
 */

import { memo, useId, useMemo } from 'react';
import { useSession } from '../engine/session';
import { screenInk, type DrawingDto, type PenDto } from './drawing';

type ToScreen = (p: { x: number; y: number }) => { x: number; y: number };

/** `onDark`: drawn straight on a dark background (a plan in the graphite theme), so black pens show in the theme's ink. */
export const DrawingLayer = memo(function DrawingLayer({ drawing, toScreen, pxPerMetre, onDark = false }: { drawing: DrawingDto; toScreen: ToScreen; pxPerMetre: number; onDark?: boolean }) {
  // Pixels per paper millimetre: a paper mm is scale/1000 m of the model.
  const pxPerMm = pxPerMetre * (drawing.scale ?? 100) / 1000;
  const cap = drawing.lineCap === 'square' ? 'square' : 'round';
  const paths = useMemo(() => drawing.pens.map((p, i) => {
    const stroke = screenInk(p.stroke, onDark), fill = screenInk(p.fill, onDark);
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
    if (p.images?.length) return <PenImages key={i} pen={p} toScreen={toScreen} />;
    if (p.texts?.length) {
      return (
        <g key={i} className="pen-text" fill={stroke ?? '#000'}>
          {p.texts.map((t, k) => {
            const at = toScreen({ x: t.x, y: t.y });
            const size = t.height * pxPerMm;
            if (size < 3) return null;
            return (
              <text key={k} x={at.x} y={at.y} fontSize={size} fontWeight={t.bold ? 700 : 400}
                fontFamily={t.font === 'serif' ? 'ui-serif, Georgia, serif' : t.font === 'mono' ? 'ui-monospace, Menlo, monospace' : 'ui-sans-serif, system-ui, sans-serif'}
                textAnchor={t.anchor === 'start' ? 'start' : t.anchor === 'end' ? 'end' : 'middle'}
                dominantBaseline={t.baseline === 'middle' ? 'central' : 'alphabetic'}
                transform={t.angle ? `rotate(${-t.angle} ${at.x} ${at.y})` : undefined}>{t.text}</text>
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
  }), [drawing, toScreen, pxPerMm, onDark]);
  return <g className="drawing-layer">{paths}</g>;
});

/** A pen's pictures, each its image's unit square laid onto its corners, clipped to the pen's box when it has one. */
function PenImages({ pen, toScreen }: { pen: PenDto; toScreen: ToScreen }) {
  const nodes = useSession((s) => s.nodes);
  const clipId = `pen-clip${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`;
  const clip = pen.clip && pen.clip.length === 4 ? [toScreen({ x: pen.clip[0], y: pen.clip[1] }), toScreen({ x: pen.clip[2], y: pen.clip[3] })] : null;
  return (
    <g className="pen-image" clipPath={clip ? `url(#${clipId})` : undefined}>
      {clip && (
        <clipPath id={clipId}>
          <rect x={Math.min(clip[0].x, clip[1].x)} y={Math.min(clip[0].y, clip[1].y)} width={Math.abs(clip[1].x - clip[0].x)} height={Math.abs(clip[1].y - clip[0].y)} />
        </clipPath>
      )}
      {(pen.images ?? []).map((img, k) => {
        const n = nodes.get(img.id);
        const data = n?.props.data, mime = n?.props.mime;
        if (typeof data !== 'string' || typeof mime !== 'string' || img.corners.length < 8) return null;
        const c = img.corners;
        const [tl, tr, bl] = [toScreen({ x: c[0], y: c[1] }), toScreen({ x: c[2], y: c[3] }), toScreen({ x: c[6], y: c[7] })];
        return <image key={k} href={`data:${mime};base64,${data}`} x={0} y={0} width={1} height={1} preserveAspectRatio="none" opacity={img.opacity} className="plan-orthophoto"
          transform={`matrix(${tr.x - tl.x} ${tr.y - tl.y} ${bl.x - tl.x} ${bl.y - tl.y} ${tl.x} ${tl.y})`} />;
      })}
    </g>
  );
}
