/**
 * The live input on screen: two cells at the cursor (length, angle; a third, the
 * angle to the previous segment or the element it starts on: decision 738) while a
 * segment is drawn or dragged, the focused one marked, a typed value bold
 * (a move's Δx and Δy before them, unless the prompt is in the status line);
 * and the bar over the view with the same values and Δx, Δy, x, y, to type
 * into with the mouse elsewhere.
 */

import { useLayoutEffect, useRef } from 'react';
import type { Vec2 } from '../platform/module';
import { formatAngle, formatLength, liveAcceptAtCursor, liveCommit, liveFields, liveHintSeen, liveMeasure, liveToggleMode, useLive, type LiveField } from './liveInput';
import { segmentArc } from './angleDims';
import { unitLabel } from '../engine/unitsStore';
import { useInterface } from '../app/interface';
import { t } from '../i18n';
import { autoholdOn, useAutohold } from '../app/autohold';

type ToScreen = (p: Vec2) => { x: number; y: number };

/** The segment's angle to the reference at its start (decision 738: the previous segment, the wall or beam it starts on); null for none (the X axis is ∠). */
function relAngle(live: ReturnType<typeof useLive.getState>, end: Vec2): number | null {
  if (!live.base || !live.ref || live.ref.what === 'x' || live.mode !== 'polar' || live.measure) return null;
  return segmentArc(live.ref, live.base, end)?.value ?? null;
}

export function LiveBox({ toScreen }: { toScreen: ToScreen }) {
  const live = useLive();
  const autohold = useAutohold((s) => s.on);
  const box = useRef<HTMLDivElement>(null);
  // The box stays inside the view it is drawn over: near the right edge it moves left, it never lies over the panels beside it.
  useLayoutEffect(() => {
    const el = box.current, host = el?.offsetParent as HTMLElement | null;
    if (!el || !host) return;
    const over = el.offsetLeft + el.offsetWidth - (host.clientWidth - 4);
    if (over > 0) el.style.left = `${Math.max(4, el.offsetLeft - over)}px`;
  });
  // A move's Δx and Δy at the cursor; in the status line instead when the prompt goes there (Compact): the inline bar shows them.
  const deltaHere = useInterface((s) => s.parts.prompt !== 'commandLine') && live.delta;
  const m = live.owner ? liveMeasure(live) : null;
  if (!live.owner || !m || !live.cursor) return null;
  /** From a temporary reference point (viewport/datum.ts): the two fields are the offsets ΔX and ΔY from it. */
  const datum = !!live.rect?.startsWith('datum:');
  const rel = relAngle(live, m.end);
  const at = toScreen(m.end);
  const cell = (f: LiveField, label: string, shown: string, unit: string) => (
    <span className={`live-cell${live.focus === f ? ' on' : ''}${live[f] ? ' typed' : ''}`}>
      <span className="live-label">{label}</span>{live[f] || shown}<span className="live-unit">{unit}</span>
    </span>
  );
  return (
    <div ref={box} className={`live-box${deltaHere ? ' move' : ''}`} style={{ left: at.x + 18, top: at.y + 18 }} aria-live="polite">
      {deltaHere && <>
        <span className="live-cell ro"><span className="live-label">Δx</span>{formatLength(m.dx)}<span className="live-unit">{unitLabel()}</span></span>
        <span className="live-cell ro"><span className="live-label">Δy</span>{formatLength(m.dy)}<span className="live-unit">{unitLabel()}</span></span>
      </>}
      {live.measure ? <>
        {/* Measuring (Scale image): the distance the two points are apart now, and the true one typed beside it. */}
        <span className="live-cell ro" title={t('Measured')}><span className="live-label">↔</span>{formatLength(m.len)}<span className="live-unit">{unitLabel()}</span></span>
        {cell('len', '=', '?', live.len ? '' : unitLabel())}
      </> : live.mode === 'rect' ? <>
        {cell('len', datum ? 'ΔX' : 'W', formatLength(m.dx), unitLabel())}
        {cell('ang', datum ? 'ΔY' : 'H', formatLength(m.dy), unitLabel())}
      </> : <>
        {cell('len', 'L', formatLength(m.len), unitLabel())}
        {cell('ang', '∠', formatAngle(m.ang), '°')}
        {rel != null && cell('rel', '∡', formatAngle(rel), '°')}
      </>}
      {live.rect && !liveHintSeen() && <span className="live-hint text" data-testid="live-hint">{live.mode === 'rect' ? t('Space: length ∠ angle') : t('Space: width × height')}</span>}
      {autohold && <span className="live-tag" data-testid="autohold-hint" title={t('Autohold: the drag follows the pointer without the button. Click to drop it here')}>{t('Autohold')}</span>}
      {live.rect && <span className="live-hint" title={t('Space switches length and angle with width and height')}>␣</span>}
    </div>
  );
}

/** The bar over the view, or (`inline`) the same fields in the command line when the interface puts the prompt there. */
export function LiveBar({ inline }: { inline?: boolean }) {
  const live = useLive();
  const here = useInterface((s) => (s.parts.prompt === 'commandLine') === !!inline);
  const m = live.owner ? liveMeasure(live) : null;
  if (!live.owner || !m || !here) return null;
  const datum = !!live.rect?.startsWith('datum:');
  const rel = relAngle(live, m.end);
  const field = (f: LiveField, label: string, shown: string) => (
    <label className={`live-field${live.focus === f ? ' on' : ''}`}>
      <span>{label}</span>
      <input value={live[f]} placeholder={shown} size={7}
        onFocus={() => useLive.setState({ focus: f })}
        onChange={(e) => useLive.setState({ [f]: e.target.value })}
        onKeyDown={(e) => {
          if (e.key === ' ' && live.rect) { e.preventDefault(); liveToggleMode(); return; }
          if (e.key === ',' && live.rect && live.mode === 'rect' && f === 'len') { e.preventDefault(); useLive.setState({ focus: 'ang' }); return; }
          if (e.key === 'Enter') { e.preventDefault(); if (!liveCommit() && autoholdOn()) liveAcceptAtCursor(); }
          if (e.key === 'Tab') { e.preventDefault(); if (e.repeat) return; const fs = liveFields(live); useLive.setState({ focus: fs[(fs.indexOf(f) + 1) % fs.length] }); }
          if (e.key === 'Escape') (e.target as HTMLInputElement).blur();
        }} />
    </label>
  );
  return (
    <div className={`live-bar${inline ? ' inline' : ''}`} role="group" aria-label="Segment">
      {live.rect && <button type="button" className="live-mode" title={t('Space switches length and angle with width and height')} onClick={liveToggleMode}>{live.mode === 'rect' ? t('Width × Height') : t('Length ∠ Angle')}</button>}
      {live.measure ? <>
        <span className="live-ro"><span>{t('Measured')}</span>{formatLength(m.len)}</span>
        {field('len', `${t('True distance')}${unitLabel() ? ` (${unitLabel()})` : ''}`, formatLength(m.len))}
      </> : live.mode === 'rect' ? <>
        {field('len', `${datum ? 'ΔX' : t('Width')}${unitLabel() ? ` (${unitLabel()})` : ''}`, formatLength(m.dx))}
        {field('ang', `${datum ? 'ΔY' : t('Height')}${unitLabel() ? ` (${unitLabel()})` : ''}`, formatLength(m.dy))}
      </> : <>
        {field('len', `Length${unitLabel() ? ` (${unitLabel()})` : ''}`, formatLength(m.len))}
        {field('ang', 'Angle (°)', formatAngle(m.ang))}
        {rel != null && field('rel', live.ref?.what === 'previous' ? t('Angle to the previous segment (°)') : t('Angle to the element (°)'), formatAngle(rel))}
      </>}
      <span className="live-ro"><span>Δx</span>{formatLength(m.dx)}</span>
      <span className="live-ro"><span>Δy</span>{formatLength(m.dy)}</span>
      <span className="live-ro"><span>x</span>{formatLength(m.end.x)}</span>
      <span className="live-ro"><span>y</span>{formatLength(m.end.y)}</span>
    </div>
  );
}
