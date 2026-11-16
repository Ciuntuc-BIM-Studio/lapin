/**
 * Temporary dimensions as drawn over a view (decision 511): each a line with its witness lines and ticks, its value (a
 * click types it; Enter commits, Escape leaves), optional clickable ends and a padlock. Used by a door's, a window's and
 * an opening's temporary dimensions (modules/architecture/TempDimensions.tsx) and by a grid axis's (forum #78,
 * modules/drafting/GridAxisDims.tsx). `pt(t, out)` places a dimension: t along what it measures, `out` px off it.
 */

import { useState } from 'react';
import type { Vec2 } from '../platform/module';
import { useSession } from '../engine/session';
import { formatLength, parseLength } from '../engine/unitsStore';
import { t } from '../i18n';

/** How far out from the wall's face (or the opening's head or side) the dimension lines run, px: the nearest wall's first, a neighbouring opening's further out. */
export const ROW_PX = [22, 42];

/** One dimension as drawn: from `a` (its reference) to `b` (the opening's end), `pt(t, out)` the screen point at t, `out` px off the measured line. */
export interface Shown {
  key: string;
  row: 0 | 1;
  a: number;
  b: number;
  pt: (t: number, out: number) => Vec2;
  /** Pieces the line is drawn in (an arc's many). */
  steps: number;
  value: number;
  data: Record<string, string>;
  refTitle?: string;
  cycleRef?: () => void;
  cycleOwn?: () => void;
  commit: (value: number) => Promise<unknown>;
  permanent?: () => void;
  /** How the value is written and read back (a length in the project's unit unless given: an angle, say). */
  format?: (v: number) => string;
  parse?: (text: string) => number | null;
  /** What a click on the value does, said on hover; the own end's click likewise. */
  valueTitle?: string;
  ownTitle?: string;
}

/** The dimensions, their ends and values, an input for the one being typed. */
export function Marks({ items, live }: { items: Shown[]; live: boolean }) {
  const [editing, setEditing] = useState<{ key: string; text: string } | null>(null);
  const hit = live ? { 'data-overlay-hit': true, style: { pointerEvents: 'all' as const, cursor: 'pointer' } } : {};
  const commit = async (d: Shown, text: string) => {
    const v = d.parse ? d.parse(text) : parseLength(text);
    if (v == null || !(v >= 0)) { useSession.setState({ error: d.parse ? 'Type an angle of zero or more.' : 'Type a distance of zero or more.' }); return; }
    setEditing(null);
    await d.commit(v);
  };
  return (
    <>
      {items.map((d) => {
        const line = ROW_PX[d.row];
        const A = d.pt(d.a, line), B = d.pt(d.b, line);
        const pts = Array.from({ length: d.steps + 1 }, (_, i) => d.pt(d.a + ((d.b - d.a) * i) / d.steps, line));
        const mt = (d.a + d.b) / 2, eps = Math.abs(d.b - d.a) / 50 || 1e-3;
        const p0 = d.pt(mt - eps, line), p1 = d.pt(mt + eps, line), pm = d.pt(mt, line);
        const ul = Math.hypot(p1.x - p0.x, p1.y - p0.y) || 1;
        const u = { x: (p1.x - p0.x) / ul, y: (p1.y - p0.y) / ul };
        const po = d.pt(mt, line + 10);
        const ol = Math.hypot(po.x - pm.x, po.y - pm.y) || 1;
        const n = { x: (po.x - pm.x) / ol, y: (po.y - pm.y) / ol };
        const witness = (s: number) => { const p = d.pt(s, 3), q = d.pt(s, line + 4); return `M${p.x} ${p.y}L${q.x} ${q.y}`; };
        // A tick across each end, slanting the same way along the line there.
        const tick = (s: number, p: Vec2) => {
          const q = d.pt(s + eps, line), l = Math.hypot(q.x - p.x, q.y - p.y) || 1, v = { x: (q.x - p.x) / l, y: (q.y - p.y) / l };
          const w = d.pt(s, line + 10), wl = Math.hypot(w.x - p.x, w.y - p.y) || 1, m = { x: (w.x - p.x) / wl, y: (w.y - p.y) / wl };
          return `M${p.x - (v.x + m.x) * 3.5} ${p.y - (v.y + m.y) * 3.5}L${p.x + (v.x + m.x) * 3.5} ${p.y + (v.y + m.y) * 3.5}`;
        };
        const mid = { x: pm.x + n.x * 8, y: pm.y + n.y * 8 };
        let angle = (Math.atan2(u.y, u.x) * 180) / Math.PI;
        if (angle > 90) angle -= 180; else if (angle < -90) angle += 180;
        const value = d.format ? d.format(d.value) : formatLength(d.value);
        const half = value.length * 3.4 + 4;
        // The padlock after the value, along the line the way the text reads.
        const rad = (angle * Math.PI) / 180;
        const lock = { x: mid.x + Math.cos(rad) * (half + 9), y: mid.y + Math.sin(rad) * (half + 9) };
        const editingThis = editing?.key === d.key;
        return (
          <g key={d.key} className={`temp-dim row${d.row}`} data-dim={d.key} {...Object.fromEntries(Object.entries(d.data).map(([k, v]) => [`data-${k}`, v]))} data-value={d.value.toFixed(4)}>
            <path className="temp-dim-line" d={`${witness(d.a)}${witness(d.b)}M${pts.map((p) => `${p.x} ${p.y}`).join('L')}${tick(d.a, A)}${tick(d.b, B)}`} />
            {live && d.cycleRef && (
              <g {...hit} data-end="ref" onClick={d.cycleRef}>
                <circle cx={A.x} cy={A.y} r={8} className="temp-dim-hit" /><circle cx={A.x} cy={A.y} r={3} className="temp-dim-end" />
                <title>{d.refTitle}</title>
              </g>
            )}
            {live && d.cycleOwn && (
              <g {...hit} data-end="own" onClick={d.cycleOwn}>
                <circle cx={B.x} cy={B.y} r={8} className="temp-dim-hit" /><circle cx={B.x} cy={B.y} r={3} className="temp-dim-end own" />
                <title>{d.ownTitle ?? t('Click: measure from the opening\'s edge or its centre')}</title>
              </g>
            )}
            {!editingThis && (
              <g {...hit} data-value-hit onClick={live ? () => setEditing({ key: d.key, text: value }) : undefined}>
                <rect x={mid.x - half} y={mid.y - 8} width={half * 2} height={16} className="temp-dim-hit" transform={`rotate(${angle} ${mid.x} ${mid.y})`} />
                <text x={mid.x} y={mid.y} className="temp-dim-text" transform={`rotate(${angle} ${mid.x} ${mid.y})`}>{value}</text>
                {live && <title>{d.valueTitle ?? t('Click to type the distance: the opening moves to match')}</title>}
              </g>
            )}
            {live && !editingThis && d.permanent && (
              <g {...hit} data-lock onClick={d.permanent} transform={`translate(${lock.x} ${lock.y})`}>
                <rect x={-7} y={-7} width={14} height={14} className="temp-dim-hit" />
                <path className="temp-dim-lock" d="M-3.5 -0.5V-3a3.5 3.5 0 0 1 7 0V-0.5M-4.5 -0.5H4.5V5H-4.5Z" />
                <title>{t('Make this dimension permanent')}</title>
              </g>
            )}
            {editingThis && (
              <foreignObject x={mid.x - 45} y={mid.y - 12} width={90} height={24} data-overlay-hit style={{ pointerEvents: 'all', overflow: 'visible' }}>
                <input className="temp-dim-input" data-field="temp-dim" autoFocus value={editing.text}
                  onFocus={(e) => e.currentTarget.select()}
                  onChange={(e) => setEditing({ key: d.key, text: e.target.value })}
                  onBlur={() => setEditing(null)}
                  onKeyDown={(e) => {
                    e.stopPropagation();
                    if (e.key === 'Enter') { e.preventDefault(); void commit(d, editing.text); }
                    else if (e.key === 'Escape') { e.preventDefault(); setEditing(null); }
                  }} />
              </foreignObject>
            )}
          </g>
        );
      })}
    </>
  );
}
