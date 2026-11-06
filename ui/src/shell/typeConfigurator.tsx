/**
 * What a parametric type's configurator is made of (exhaust fans, report #55; gates, report #57): the engine's catalogue
 * of parameters, each with when it applies (`showIf`: the spec's keys and the values they may have), a field per
 * parameter, and the engine's preview of the draft — its plan symbol (y up) above its front (z up).
 */

import { useEffect, useState } from 'react';
import { query, useSession } from '../engine/session';
import { LengthField, NumberField } from './cells';

export interface ParamDef {
  key: string; label: string; kind: 'length' | 'number' | 'choice' | 'bool';
  min?: number | null; max?: number | null; choices?: string[] | null; showIf?: Record<string, string[]> | null; group?: string;
}

export type Spec = Record<string, unknown> & { kind?: string; materials?: Record<string, string> };

/** A spec's value as a condition reads it: text, a number as written, a switch as true or false; missing: ''. */
export const valueOf = (spec: Spec, defaults: Record<string, unknown>, key: string): string => {
  const v = spec[key] ?? defaults[key];
  return v == null ? '' : String(v);
};

/** Whether a parameter applies to a spec (every condition holds). */
export function applies(p: ParamDef, spec: Spec, defaults: Record<string, unknown> = {}): boolean {
  if (!p.showIf) return true;
  return Object.entries(p.showIf).every(([k, values]) => values.includes(valueOf(spec, defaults, k)));
}

/** A choice as the interface shows it: a key in words (verticalBars: vertical bars). */
export const choiceLabel = (c: string) => c.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();

/** One parameter, edited as its kind says. */
export function ParamField({ p, value, onCommit }: { p: ParamDef; value: unknown; onCommit: (v: unknown) => void }) {
  switch (p.kind) {
    case 'length': return <LengthField metres={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'number': return <NumberField value={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'choice': return <select value={String(value ?? '')} onChange={(e) => onCommit(e.target.value)}>{(p.choices ?? []).map((c) => <option key={c} value={c}>{choiceLabel(c)}</option>)}</select>;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    default: return null;
  }
}

/** The engine's preview of a draft: plan items in the type's frame and front outlines by role, with its colour. */
export interface PreviewDto {
  plan: { kind: string; closed: boolean; points: number[] }[];
  front: { role: string; color: string; points: number[]; holes?: number[][] | null }[];
  width: number; height: number;
  [more: string]: unknown;
}

/** An SVG path through x, y pairs; `flip` turns y up (a plan, a front). */
export function pathOf(points: number[], closed: boolean, flip = true): string {
  const parts: string[] = [];
  for (let k = 0; k + 1 < points.length; k += 2) parts.push(`${k ? 'L' : 'M'}${points[k].toFixed(4)} ${(flip ? -points[k + 1] : points[k + 1]).toFixed(4)}`);
  return parts.join(' ') + (closed ? ' Z' : '');
}

/** The box (x, y, w, h in SVG terms, y down) round some point lists, with a margin. */
export function boxOf(lists: number[][], margin: number): [number, number, number, number] {
  let [x0, y0, x1, y1] = [Infinity, Infinity, -Infinity, -Infinity];
  for (const pts of lists) for (let k = 0; k + 1 < pts.length; k += 2) {
    x0 = Math.min(x0, pts[k]); x1 = Math.max(x1, pts[k]);
    y0 = Math.min(y0, -pts[k + 1]); y1 = Math.max(y1, -pts[k + 1]);
  }
  if (!Number.isFinite(x0)) return [-1, -1, 2, 2];
  return [x0 - margin, y0 - margin, x1 - x0 + 2 * margin, y1 - y0 + 2 * margin];
}

/** The engine's picture of a draft, asked again as it changes: its plan, its front, and a line under them. */
export function TypePreview({ queryName, args, testId, caption }: { queryName: string; args: Record<string, unknown>; testId: string; caption?: (p: PreviewDto) => string }) {
  const nodes = useSession((s) => s.nodes);
  const [p, setP] = useState<PreviewDto | null>(null);
  const key = JSON.stringify(args);
  useEffect(() => {
    let live = true;
    void query<PreviewDto>(queryName, args).then((r) => { if (live) setP(r); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queryName, key, nodes]);
  if (!p) return <div className="type-preview muted small">…</div>;
  const [px, py, pw, ph] = boxOf(p.plan.map((i) => i.points), 0.08);
  const [fx, fy, fw, fh] = boxOf(p.front.map((i) => i.points), 0.06);
  const ps = Math.max(pw, ph) / 280;
  const fs = Math.max(fw, fh) / 400;
  return (
    <div className="type-preview" data-testid={testId}>
      <svg viewBox={`${px} ${py} ${pw} ${ph}`} className="type-preview-plan" role="img" aria-label="Plan">
        {p.plan.map((x, i) => (
          <path key={i} d={pathOf(x.points, x.closed)} fill="none" stroke="currentColor" strokeWidth={x.kind === 'heavy' ? ps * 2.2 : ps}
            strokeDasharray={x.kind === 'dashed' || x.kind === 'overhead' ? `${ps * 6} ${ps * 4}` : undefined} />
        ))}
      </svg>
      <svg viewBox={`${fx} ${fy} ${fw} ${fh}`} className="type-preview-front" role="img" aria-label="Front">
        {p.front.map((x, i) => <path key={i} d={[x.points, ...(x.holes ?? [])].map((pts) => pathOf(pts, true)).join(' ')} fillRule="evenodd" fill={x.color} stroke="rgba(0,0,0,.45)" strokeWidth={fs} />)}
      </svg>
      {caption && <p className="muted small" data-no-i18n>{caption(p)}</p>}
    </div>
  );
}
