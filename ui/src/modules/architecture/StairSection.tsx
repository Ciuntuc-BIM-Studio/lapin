/**
 * A stair in the inspector: its type (the configurator makes them), the width,
 * the turn, the offsets, what it fixes (risers, riser, tread, landing; blank:
 * as the rules say), the numbers that came out with their checks, and the
 * opening it asks of the floor above. Lengths in mm.
 */

import { useEffect, useMemo, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatingOf } from '../../engine/model';
import { useUi } from '../../app/store';
import { lengthText } from '../../engine/unitsStore';

export interface StairCheck { key: string; label: string; value: number; min: number | null; max: number | null; ok: boolean }
export interface StairNumbers {
  risers: number; riserHeight: number; tread: number; step: number; width: number; height: number; landing: number; flights: number[]; winders: number; run: number; ok: boolean;
  checks: StairCheck[]; outlines: { kind: string; points: number[]; risers?: number }[]; risersLines: number[][]; profile: number[]; top: number[]; predefinedType: string; shape?: string;
  opening?: string | null; topStorey?: string | null;
}

const mm = (v: number) => Math.round(v * 1000);
const fmtMm = (v: number | null | undefined) => (v == null ? '—' : lengthText(v));

/** A length typed in mm, committed on Enter or blur; empty frees the value (null). */
function MmInput({ value, onCommit, placeholder }: { value: number | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  const [text, setText] = useState(value == null ? '' : String(mm(value)));
  useEffect(() => setText(value == null ? '' : String(mm(value))), [value]);
  const done = () => {
    const t = text.trim();
    if (t === '') { if (value != null) onCommit(null); return; }
    const n = Number(t.replace(',', '.'));
    if (Number.isFinite(n) && (value == null || Math.abs(n - mm(value)) > 0.5)) onCommit(n / 1000);
  };
  return <input className="num" value={text} placeholder={placeholder} onChange={(e) => setText(e.target.value)} onBlur={done}
    onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(value == null ? '' : String(mm(value))); }} />;
}

export function ChecksList({ checks }: { checks: StairCheck[] }) {
  return (
    <ul className="stair-checks">
      {checks.map((c) => (
        <li key={c.key} className={c.ok ? 'ok' : 'bad'}>
          <span>{c.label}</span>
          <span className="num">{c.key === 'flight' || c.key === 'width' && c.value > 50 ? c.value : fmtMm(c.value)}</span>
          <span className="muted small">{c.min != null && c.max != null ? `${c.key === 'flight' ? c.min : mm(c.min)} … ${c.key === 'flight' ? c.max : mm(c.max)}` : c.min != null ? `≥ ${c.key === 'flight' ? c.min : mm(c.min)}` : c.max != null ? `≤ ${mm(c.max)}` : ''}</span>
        </li>
      ))}
    </ul>
  );
}

export function StairSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcStairType');
  const type = relatingOf(model, node.id, 'IfcRelDefinesByType');
  const [n, setN] = useState<StairNumbers | null>(null);
  useEffect(() => { void query<StairNumbers>('architecture.stairCheck', { id: node.id }).then(setN); }, [node.id, nodes]);
  const set = (args: Record<string, unknown>) => void execute('architecture.updateStair', { id: node.id, ...args });
  const num = (key: string) => (typeof node.props[key] === 'number' ? (node.props[key] as number) : undefined);
  const topStorey = n?.topStorey ? nodes.get(n.topStorey) : undefined;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Stair type</span>
        <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => set({ typeId: e.target.value })}>
          {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
        <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'stairTypes' })}>Stair types…</button>
      </label>
      <dl className="fields">
        <dt>Width</dt><dd><MmInput value={num('width')} placeholder={n ? String(mm(n.width)) : ''} onCommit={(v) => set({ width: v })} /> <span className="muted">mm</span></dd>
        <dt>Turn</dt>
        <dd><select value={(node.props.turn as string | undefined) ?? 'left'} onChange={(e) => set({ turn: e.target.value })}><option value="left">left</option><option value="right">right</option></select></dd>
        <dt>Base offset</dt><dd><MmInput value={num('baseOffset') ?? 0} onCommit={(v) => set({ baseOffset: v ?? 0 })} /> <span className="muted">mm</span></dd>
        <dt>Top offset</dt><dd><MmInput value={num('topOffset') ?? 0} onCommit={(v) => set({ topOffset: v ?? 0 })} /> <span className="muted">mm</span></dd>
        <dt>Height</dt><dd><span className="num">{n ? fmtMm(n.height) : '—'}</span> <span className="muted small">{topStorey ? `climbs to ${topStorey.name}` : 'no storey above'}</span></dd>
      </dl>
      <p className="eyebrow">Fixed</p>
      <dl className="fields">
        <dt>Risers</dt>
        <dd><input className="num" type="number" min={2} max={60} value={num('risers') ?? ''} placeholder={n ? String(n.risers) : ''} onChange={(e) => set({ risers: e.target.value === '' ? null : Number(e.target.value) })} /> <span className="muted small">{n ? `${n.flights.join(' + ')}${n.winders ? ` + ${n.winders} winders` : ''}` : ''}</span></dd>
        <dt>Riser</dt><dd><MmInput value={num('riserHeight')} placeholder={n ? String(mm(n.riserHeight)) : ''} onCommit={(v) => set({ riserHeight: v })} /> <span className="muted">mm</span></dd>
        <dt>Tread</dt><dd><MmInput value={num('tread')} placeholder={n ? String(mm(n.tread)) : ''} onCommit={(v) => set({ tread: v })} /> <span className="muted">mm</span></dd>
        <dt>Landing length</dt><dd><MmInput value={num('landing')} placeholder={n ? String(mm(n.landing)) : ''} onCommit={(v) => set({ landing: v })} /> <span className="muted">mm</span></dd>
        {n && <><dt>Run in plan</dt><dd><span className="num">{fmtMm(n.run)}</span></dd></>}
      </dl>
      {n && <><p className="eyebrow">Checks</p><ChecksList checks={n.checks} /></>}
      <label className="row small">
        <input type="checkbox" checked={!node.props.noOpening} disabled={!topStorey} onChange={(e) => set({ noOpening: !e.target.checked })} />
        Opening in the floor above{n?.opening ? '' : topStorey ? ' (no floor found over the top of the stair)' : ''}
      </label>
    </div>
  );
}
