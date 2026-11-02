/**
 * A stair in the inspector: its type (the configurator makes them), the width,
 * the turn, the offsets, what it fixes (risers, riser, tread, landing; blank:
 * as the rules say), the numbers that came out with their checks, and the
 * opening it asks of the floor above. Lengths in the project's unit.
 */

import { useEffect, useMemo, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatingOf } from '../../engine/model';
import { useUi } from '../../app/store';
import { formatLength, lengthText } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { levelText } from './stairPlacement';

export interface StairCheck { key: string; label: string; value: number; min: number | null; max: number | null; ok: boolean }
export interface StairNumbers {
  risers: number; riserHeight: number; tread: number; step: number; width: number; height: number; landing: number; flights: number[]; winders: number; run: number; ok: boolean;
  checks: StairCheck[]; outlines: { kind: string; points: number[]; risers?: number }[]; risersLines: number[][]; profile: number[]; top: number[]; predefinedType: string; shape?: string; sweep?: number | null;
  opening?: string | null; topStorey?: string | null; ownRise?: number | null; fromLevel?: number; toLevel?: number;
}

const fmtMm = (v: number | null | undefined) => (v == null ? '—' : lengthText(v));
const mm = (v: number) => formatLength(v);

/** A length of the stair (the shared field); empty frees it (null: as the rules say). */
function MmInput({ value, onCommit, placeholder }: { value: number | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  return <LengthField metres={value} placeholder={placeholder} blank onCommit={onCommit} />;
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
  useEffect(() => {
    let live = true;
    void query<StairNumbers>('architecture.stairCheck', { id: node.id }).then((r) => { if (live) setN(r); });
    return () => { live = false; };
  }, [node.id, nodes]);
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
        <dt>Width</dt><dd><MmInput value={num('width')} placeholder={n ? mm(n.width) : ''} onCommit={(v) => set({ width: v })} /></dd>
        <dt>Turn</dt>
        <dd><select value={(node.props.turn as string | undefined) ?? 'left'} onChange={(e) => set({ turn: e.target.value })}><option value="left">left</option><option value="right">right</option></select></dd>
        <dt>Base offset</dt><dd><MmInput value={num('baseOffset') ?? 0} onCommit={(v) => set({ baseOffset: v ?? 0 })} /></dd>
        <dt>Rise</dt>
        <dd><MmInput value={num('rise')} placeholder={n?.ownRise != null ? mm(n.ownRise) : 'to the storey'} onCommit={(v) => set({ rise: v })} /></dd>
        {n?.ownRise == null && <><dt>Top offset</dt><dd><MmInput value={num('topOffset') ?? 0} onCommit={(v) => set({ topOffset: v ?? 0 })} /></dd></>}
        <dt>Height</dt><dd><span className="num">{n ? fmtMm(n.height) : '—'}</span> <span className="muted small">{topStorey ? `climbs to ${topStorey.name}` : n?.ownRise != null ? 'a rise of its own' : 'no storey above'}</span></dd>
        {n?.fromLevel != null && n.toLevel != null && <><dt>Levels</dt><dd><span className="num">{levelText(n.fromLevel)} → {levelText(n.toLevel)}</span></dd></>}
      </dl>
      <p className="eyebrow">Fixed</p>
      <dl className="fields">
        <dt>Risers</dt>
        <dd><NumberField value={num('risers')} placeholder={n ? String(n.risers) : ''} blank integer min={1} max={60} onCommit={(v) => set({ risers: v })} /> <span className="muted small">{n ? `${n.flights.join(' + ')}${n.winders ? ` + ${n.winders} winders` : ''}` : ''}</span></dd>
        <dt>Riser</dt><dd><MmInput value={num('riserHeight')} placeholder={n ? mm(n.riserHeight) : ''} onCommit={(v) => set({ riserHeight: v })} /></dd>
        <dt>Tread</dt><dd><MmInput value={num('tread')} placeholder={n ? mm(n.tread) : ''} onCommit={(v) => set({ tread: v })} /></dd>
        {n?.sweep == null && <><dt>Landing length</dt><dd><MmInput value={num('landing')} placeholder={n ? mm(n.landing) : ''} onCommit={(v) => set({ landing: v })} /></dd></>}
        {n?.sweep != null && <><dt>Turns through</dt>
          <dd><NumberField testId="sweep" value={num('sweep')} placeholder={String(Math.round(n.sweep))} blank min={1} suffix="°" onCommit={(v) => set({ sweep: v })} /></dd></>}
        {n && <><dt>Run in plan</dt><dd><span className="num">{fmtMm(n.run)}</span></dd></>}
      </dl>
      <dl className="fields">
        <dt>Railing</dt>
        <dd><select data-field="railing-sides" value={(node.props.railingSides as string | undefined) ?? ''} onChange={(e) => set({ railingSides: e.target.value === '' ? null : e.target.value })}>
          <option value="">As the type</option><option value="none">None</option><option value="left">Left</option><option value="right">Right</option><option value="both">Both sides</option>
        </select></dd>
      </dl>
      {n && <><p className="eyebrow">Checks</p><ChecksList checks={n.checks} /></>}
      <label className="row small">
        <input type="checkbox" checked={!node.props.noOpening} disabled={!topStorey} onChange={(e) => set({ noOpening: !e.target.checked })} />
        Opening in the floor above{n?.opening ? '' : topStorey ? ' (no floor found over the top of the stair)' : ''}
      </label>
    </div>
  );
}
