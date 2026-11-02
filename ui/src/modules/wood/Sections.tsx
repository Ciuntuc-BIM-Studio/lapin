/**
 * The wood module in the properties panel: a timber-frame wall's members and warnings; a door's or window's framing (its
 * rough opening, the rule it takes by its width band, what it gets) and its own rule, set here for that opening alone.
 */

import { useEffect, useState } from 'react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { CLT_FLOOR, GRADES, HEADERS, SECTIONS, type CltSpec, type Rule } from './FrameDialog';

type WallInfo = { framed: boolean; clt?: boolean; label?: string; counts?: Record<string, number>; warnings?: string[]; panels?: [number, number][] | number };
type OpeningInfo = { framed: boolean; own?: boolean; rule?: Rule; roughWidth?: number; roughHeight?: number; members?: { role: string; count: number; section: string }[]; warnings?: string[] };

export function WallWoodSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [info, setInfo] = useState<WallInfo | null>(null);
  useEffect(() => { let live = true; void query<WallInfo>('wood.wall', { id: node.id }).then((r) => { if (live) setInfo(r); }); return () => { live = false; }; }, [node.id, nodes]);
  const elevation = (
    <button type="button" className="btn compact" data-testid="wood-frame-view" title="A section along the wall through its core, at 1:20, dimensioned: place it on a sheet"
      onClick={() => void execute('wood.frameView', { id: node.id }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) useUi.setState({ view: 'drawing', drawingViewId: id }); })}>{info?.clt ? 'Panel elevation' : 'Frame elevation'}</button>
  );
  if (info?.clt) return (
    <div className="layers">
      <dl className="fields"><dt>CLT</dt><dd>{info.label}</dd><dt>Panels</dt><dd className="num">{String(info.panels ?? 0)}</dd></dl>
      {elevation}
    </div>
  );
  if (!info?.framed) return <p className="muted small pad">Built of its layers: its type is not a timber frame (Wood → Timber frame).</p>;
  const panels = Array.isArray(info.panels) ? info.panels : [];
  return (
    <div className="layers">
      <dl className="fields">
        {Object.entries(info.counts ?? {}).map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num">{v}</dd></div>)}
        {panels.length > 0 && <div className="contents"><dt title="Prefabricated panels, first to last along the wall">Panels</dt><dd className="num">{panels.map(([a, b]) => Math.round((b - a) * 1000)).join(' + ')}</dd></div>}
      </dl>
      {(info.warnings ?? []).map((w, i) => <p key={i} className="error-text small">{w}</p>)}
      {elevation}
    </div>
  );
}

const ROOF_PLIES: Record<string, number[]> = { '3 × 40 = 120': [40, 40, 40], '5 plies = 160': [40, 20, 40, 20, 40], '5 × 40 = 200': [40, 40, 40, 40, 40], '7 plies = 240': [40, 30, 30, 40, 30, 30, 40] };

/** A roof of CLT (decision 246): its planes as panels in place of its rafters or trusses. */
export function RoofWoodSection({ node }: { node: NodeDto }) {
  const shape = node.props.shape as string | undefined;
  if (shape === 'flat' || shape === 'barrel') return <p className="muted small pad">A flat roof takes a CLT floor type (Wood → Timber frame and CLT); a vault is not made of plates.</p>;
  const spec = (node.props.woodClt as unknown as CltSpec | undefined) ?? null;
  const send = (s: CltSpec | null) => void execute('wood.setRoofClt', { id: node.id, spec: s });
  const s = spec ?? CLT_FLOOR;
  const set = (patch: Partial<CltSpec>) => send({ ...s, ...patch });
  const plies = Object.entries(ROOF_PLIES).find(([, v]) => v.join('/') === s.plies.join('/'))?.[0] ?? '';
  return (
    <div className="layers">
      <dl className="fields">
        <dt title="Its planes as CLT panels in place of its rafters or trusses; the wall plates and the covering stay">CLT panels</dt>
        <dd><input type="checkbox" data-testid="wood-roof-clt" checked={spec != null} disabled={!node.editable} onChange={(e) => send(e.target.checked ? CLT_FLOOR : null)} /></dd>
        {spec && (
          <>
            <dt>Plies</dt>
            <dd><select className="cell" value={plies} disabled={!node.editable} onChange={(e) => set({ plies: ROOF_PLIES[e.target.value] })}>{plies === '' && <option value="">{s.plies.join('/')}</option>}{Object.keys(ROOF_PLIES).map((k) => <option key={k}>{k}</option>)}</select></dd>
            <dt>Grade</dt>
            <dd><select className="cell" value={s.grade} disabled={!node.editable} onChange={(e) => set({ grade: e.target.value })}>{[...GRADES, 'T14', 'T22'].map((g) => <option key={g}>{g}</option>)}</select></dd>
            <dt>Widest panel</dt>
            <dd><input className="cell num" type="number" step={100} min={1000} max={4000} value={Math.round(s.maxWidth * 1000)} disabled={!node.editable} onChange={(e) => set({ maxWidth: Number(e.target.value) / 1000 })} /></dd>
            <dt>Longest panel</dt>
            <dd><input className="cell num" type="number" step={500} min={2000} max={30000} value={Math.round(s.maxLength * 1000)} disabled={!node.editable} onChange={(e) => set({ maxLength: Number(e.target.value) / 1000 })} /></dd>
            <dt>Joint</dt>
            <dd><select className="cell" value={s.joint} disabled={!node.editable} onChange={(e) => set({ joint: e.target.value })}><option value="butt">butt</option><option value="halfLap">half lap</option><option value="spline">spline</option></select></dd>
          </>
        )}
      </dl>
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
