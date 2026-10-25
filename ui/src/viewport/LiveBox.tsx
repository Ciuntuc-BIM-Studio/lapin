/**
 * The live input on screen: two cells at the cursor (length, angle) while a
 * segment is drawn or dragged, the focused one marked, a typed value bold;
 * and the bar over the view with the same values and Δx, Δy, x, y, to type
 * into with the mouse elsewhere.
 */

import type { Vec2 } from '../platform/module';
import { formatAngle, formatLength, liveCommit, liveMeasure, useLive, type LiveField } from './liveInput';

type ToScreen = (p: Vec2) => { x: number; y: number };

export function LiveBox({ toScreen }: { toScreen: ToScreen }) {
  const live = useLive();
  const m = live.owner ? liveMeasure(live) : null;
  if (!live.owner || !m || !live.cursor) return null;
  const at = toScreen(m.end);
  const cell = (f: LiveField, label: string, shown: string, unit: string) => (
    <span className={`live-cell${live.focus === f ? ' on' : ''}${live[f] ? ' typed' : ''}`}>
      <span className="live-label">{label}</span>{live[f] || shown}<span className="live-unit">{unit}</span>
    </span>
  );
  return (
    <div className="live-box" style={{ left: at.x + 18, top: at.y + 18 }} aria-live="polite">
      {cell('len', 'L', formatLength(m.len), 'mm')}
      {cell('ang', '∠', formatAngle(m.ang), '°')}
    </div>
  );
}

export function LiveBar() {
  const live = useLive();
  const m = live.owner ? liveMeasure(live) : null;
  if (!live.owner || !m) return null;
  const field = (f: LiveField, label: string, shown: string) => (
    <label className={`live-field${live.focus === f ? ' on' : ''}`}>
      <span>{label}</span>
      <input value={live[f]} placeholder={shown} size={7}
        onFocus={() => useLive.setState({ focus: f })}
        onChange={(e) => useLive.setState({ [f]: e.target.value })}
        onKeyDown={(e) => {
          if (e.key === 'Enter') { e.preventDefault(); liveCommit(); }
          if (e.key === 'Tab') { e.preventDefault(); useLive.setState({ focus: f === 'len' ? 'ang' : 'len' }); }
          if (e.key === 'Escape') (e.target as HTMLInputElement).blur();
        }} />
    </label>
  );
  return (
    <div className="live-bar" role="group" aria-label="Segment">
      {field('len', 'Length (mm)', formatLength(m.len))}
      {field('ang', 'Angle (°)', formatAngle(m.ang))}
      <span className="live-ro"><span>Δx</span>{formatLength(m.dx)}</span>
      <span className="live-ro"><span>Δy</span>{formatLength(m.dy)}</span>
      <span className="live-ro"><span>x</span>{formatLength(m.end.x)}</span>
      <span className="live-ro"><span>y</span>{formatLength(m.end.y)}</span>
    </div>
  );
}
