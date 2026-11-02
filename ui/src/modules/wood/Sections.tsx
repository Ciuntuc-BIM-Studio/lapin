/**
 * The wood module in the properties panel: a timber-frame wall's members and warnings; a door's or window's framing (its
 * rough opening, the rule it takes by its width band, what it gets) and its own rule, set here for that opening alone.
 */

import { useEffect, useState } from 'react';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { HEADERS, SECTIONS, type Rule } from './FrameDialog';

type WallInfo = { framed: boolean; counts?: Record<string, number>; warnings?: string[] };
type OpeningInfo = { framed: boolean; own?: boolean; rule?: Rule; roughWidth?: number; roughHeight?: number; members?: { role: string; count: number; section: string }[]; warnings?: string[] };

export function WallWoodSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [info, setInfo] = useState<WallInfo | null>(null);
  useEffect(() => { let live = true; void query<WallInfo>('wood.wall', { id: node.id }).then((r) => { if (live) setInfo(r); }); return () => { live = false; }; }, [node.id, nodes]);
  if (!info?.framed) return <p className="muted small pad">Built of its layers: its type is not a timber frame (Wood → Timber frame).</p>;
  return (
    <div className="layers">
      <dl className="fields">
        {Object.entries(info.counts ?? {}).map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num">{v}</dd></div>)}
      </dl>
      {(info.warnings ?? []).map((w, i) => <p key={i} className="error-text small">{w}</p>)}
    </div>
  );
}

export function OpeningWoodSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [info, setInfo] = useState<OpeningInfo | null>(null);
  useEffect(() => { let live = true; void query<OpeningInfo>('wood.opening', { id: node.id }).then((r) => { if (live) setInfo(r); }); return () => { live = false; }; }, [node.id, nodes]);
  if (!info?.framed || !info.rule) return <p className="muted small pad">Its wall is not a timber frame.</p>;
  const r = info.rule;
  const send = (patch: Partial<Rule>) => void execute('wood.setOpeningFrame', { id: node.id, rule: { ...r, ...patch } });
  const disabled = !info.own || !node.editable;
  return (
    <div className="layers">
      <dl className="fields">
        <dt>Rough opening</dt><dd className="num">{`${Math.round((info.roughWidth ?? 0) * 1000)} × ${Math.round((info.roughHeight ?? 0) * 1000)}`}</dd>
        <dt title="This opening's own rule instead of its band's">Own rule</dt>
        <dd><input type="checkbox" checked={Boolean(info.own)} disabled={!node.editable} data-testid="wood-own"
          onChange={(e) => void execute('wood.setOpeningFrame', { id: node.id, rule: e.target.checked ? r : null })} /></dd>
        <dt>King studs</dt><dd><input className="cell num" type="number" min={0} max={4} value={r.kings} disabled={disabled} onChange={(e) => send({ kings: Number(e.target.value) })} /></dd>
        <dt>Jack studs</dt><dd><input className="cell num" type="number" min={0} max={4} value={r.jacks} disabled={disabled} onChange={(e) => send({ jacks: Number(e.target.value) })} /></dd>
        <dt>Header</dt>
        <dd><select className="cell" value={r.header.kind} disabled={disabled} onChange={(e) => send({ header: { ...r.header, kind: e.target.value } })}>{HEADERS.map((h) => <option key={h}>{h}</option>)}</select></dd>
        <dt>Section</dt>
        <dd><select className="cell" value={r.header.section} disabled={disabled} onChange={(e) => send({ header: { ...r.header, section: e.target.value } })}>{[...new Set([...SECTIONS, r.header.section])].map((s) => <option key={s}>{s}</option>)}</select></dd>
        <dt>Plies</dt><dd><input className="cell num" type="number" min={1} max={4} value={r.header.plies} disabled={disabled} onChange={(e) => send({ header: { ...r.header, plies: Number(e.target.value) } })} /></dd>
        <dt>Sill plies</dt><dd><input className="cell num" type="number" min={0} max={3} value={r.sill} disabled={disabled} onChange={(e) => send({ sill: Number(e.target.value) })} /></dd>
      </dl>
      <p className="muted small pad">{(info.members ?? []).map((m) => `${m.count} ${m.role} ${m.section}`).join(' · ')}</p>
      {(info.warnings ?? []).map((w, i) => <p key={i} className="error-text small">{w}</p>)}
    </div>
  );
}
