/**
 * A drawing's pens in SVG: fills, hatches, then lines, each pen one path.
 * Widths and dashes are paper millimetres at the view's scale, so a plan at
 * 1:100 zoomed to paper size looks as it prints.
 */

import { memo, useId, useMemo } from 'react';
import { useSession } from '../engine/session';
import { screenWidth, useDisplay } from '../app/display';
import { screenFill, screenInk, splitAlpha, type DrawingDto, type PenDto } from './drawing';
import { imageUrl, visibleCrop } from './imageCrop';
import { uprightText } from './orientation';
import { clipSegment, outside, screenRect } from './screenClip';

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
    // A fill with an opacity (#rrggbbaa, a room colour's, forum #79) is drawn as its colour at that opacity.
    const seen = splitAlpha(p.fill);
    // A pen's stroke carries its opacity the same way (a material's hatch opacity, decision 678): its colour alone and strokeOpacity.
    const line = splitAlpha(p.stroke);
    const stroke = screenInk(line.color, onDark), fill = screenFill(seen.color, onDark);
    let d = '';
    const fmt = (q: { x: number; y: number }) => `${q.x.toFixed(1)} ${q.y.toFixed(1)}`;
    // On a view that says its screen, only what it shows (screenClip.ts, forum topic 162): zoomed far in, every line
    // dashed whole past the screen made each frame slower the further one zoomed.
    const rect = screen ? screenRect(screen.w, screen.h) : null;
    const dashes = p.dash ? p.dash.map((v) => Math.round(Math.max(1, Math.min(v * pxPerMm, v * 8)) * 10) / 10) : null;
    const period = dashes ? dashes.reduce((a, b) => a + b, 0) * (dashes.length % 2 ? 2 : 1) : 0;
    for (const loop of p.loops ?? []) {
      const on: number[] = [];
      for (let k = 0; k + 1 < loop.length; k += 2) { const q = toScreen({ x: loop[k], y: loop[k + 1] }); on.push(q.x, q.y); }
      if (rect && outside(on, rect)) continue;
      for (let k = 0; k + 1 < on.length; k += 2) d += `${k === 0 ? 'M' : 'L'}${on[k].toFixed(1)} ${on[k + 1].toFixed(1)}`;
      d += 'Z';
    }
    const s = p.segments ?? [];
    for (let k = 0; k + 3 < s.length; k += 4) {
      let qa = toScreen({ x: s[k], y: s[k + 1] }), qb = toScreen({ x: s[k + 2], y: s[k + 3] });
      if (rect) { const c = clipSegment(qa, qb, rect, period); if (!c) continue; [qa, qb] = c; }
      const a = fmt(qa), b = fmt(qb);
      d += a === b ? `M${a}h0.8` : `M${a}L${b}`;
    }
    // True to paper, within what a screen shows well when zoomed far in or out.
    const width = p.stroke ? screenWidth(p.width, pxPerMm, thin) : 0;
    if (p.images?.length) return <PenImages key={i} pen={p} toScreen={toScreen} screen={screen} />;
    if (p.texts?.length) {
      return (
        <g key={i} className="pen-text" fill={stroke ?? '#000'} fillOpacity={line.opacity < 1 ? line.opacity : undefined}>
          {p.texts.map((t, k) => {
            const size = t.height * pxPerMm;
            if (size < minText) return null;
            // A turned plan (decision 526, or true north up) keeps its texts readable: upright within ±90°, as the engine prints them.
            const u = uprightText(t, turnDeg, (drawing.scale ?? 100) / 1000);
            const at = toScreen({ x: u.x, y: u.y });
            // Far off the screen (by more than the text can reach from its anchor), left out.
            if (rect && outside([at.x, at.y], [rect[0] - size * (t.text.length + 1), rect[1] - size * (t.text.length + 1), rect[2] + size * (t.text.length + 1), rect[3] + size * (t.text.length + 1)])) return null;
            return (
              <text key={k} x={at.x} y={at.y} fontSize={size} fontWeight={t.bold ? 700 : 400} fontStyle={t.italic ? 'italic' : undefined}
                fontFamily={t.font === 'serif' ? 'ui-serif, Georgia, serif' : t.font === 'mono' ? 'ui-monospace, Menlo, monospace' : 'ui-sans-serif, system-ui, sans-serif'}
                textAnchor={u.anchor === 'start' ? 'start' : u.anchor === 'end' ? 'end' : 'middle'}
                dominantBaseline={t.baseline === 'middle' ? 'central' : 'alphabetic'}
                transform={u.angle ? `rotate(${-u.angle} ${at.x} ${at.y})` : undefined}>{t.text}</text>
            );
          })}
        </g>
      );
    }
    return (
      <path key={i} d={d} className={`pen pen-${p.layer.toLowerCase()}`} fill={fill ?? 'none'} fillOpacity={seen.opacity < 1 ? seen.opacity : undefined} fillRule="evenodd"
        stroke={stroke ?? 'none'} strokeOpacity={line.opacity < 1 ? line.opacity : undefined} strokeWidth={width} strokeLinecap={cap} strokeLinejoin={cap === 'square' ? 'miter' : 'round'}
        strokeDasharray={dashes ? dashes.join(' ') : undefined} />
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
