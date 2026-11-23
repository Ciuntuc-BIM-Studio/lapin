/**
 * The systems of the building: one per service, each with the defaults its
 * nodes and runs take (size, plane and elevations, slope, pair, material…).
 * One is current: new nodes and runs go in it. New runs may also be drawn
 * paired or single, in a plane, over the system's defaults.
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialOf, materialsFor, services, systemsQuery, type Defaults, type LineLook, type SystemInfo } from './network';
import { COVERAGE_MODES, LINE_TYPES } from './planGraphics';
import { front2dView } from '../../viewport/viewTemporary';
import { mepHooks } from './hooks';
import { setMep, useMep, type Plane } from './prefs';
import { Cell, LengthField, NumberField } from '../../shell/cells';

export function SystemsDialog() {
  const version = useSession((s) => s.version);
  const error = useSession((s) => s.error);
  const prefs = useMep();
  const [systems, setSystems] = useState<SystemInfo[]>([]);
  const [service, setService] = useState('heating');
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void systemsQuery().then((r) => { if (live && r) setSystems(r); });
    return () => { live = false; };
  }, [version]);
  const current = systems.find((s) => s.id === prefs.systemId) ?? systems[0] ?? null;
  const update = (id: string, patch: Partial<Defaults>) => void execute('mep.updateSystem', { id, defaults: patch });
  const rename = (id: string, name: string) => void execute('mep.updateSystem', { id, name });
  const create = async () => {
    const r = await execute('mep.createSystem', { service }) as { id: string } | null;
    if (r?.id) setMep({ systemId: r.id });
  };
  const d = current?.defaults;
  const style = (id: string, line: 'line' | 'return', patch: Partial<LineLook>) => void execute('mep.updateSystem', { id, style: { [line]: patch } });
  const planId = front2dView();
  const plan = planId && useUi.getState().view === 'plan' ? planId : null;
  const planNode = plan ? useSession.getState().nodes.get(plan) : undefined;
  const covered = current && (current.service === 'fireAlarm' || current.service === 'fireWater');
  // The material's laying rule in a few words, when the product knows the rules (mep-pro).
  const rule = current && d ? mepHooks.ruleText(current.service, materialOf(current.service, d.material)?.id ?? null) : null;
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="Systems" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Systems</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <table className="grid">
            <thead><tr><th>Current</th><th>Name</th><th>Service</th><th>Nodes</th><th>Runs</th></tr></thead>
            <tbody>
              {systems.map((s) => (
                <tr key={s.id} className={s.id === current?.id ? 'selected' : ''}>
                  <td><input type="radio" name="mep-system" checked={s.id === prefs.systemId} onChange={() => setMep({ systemId: s.id })} /></td>
                  <td><Cell value={s.name} onCommit={(t) => { const v = t.trim(); if (!v) return false; if (v !== s.name) rename(s.id, v); }} /></td>
                  <td>{services().find((x) => x.id === s.service)?.label ?? s.service}</td>
                  <td className="num">{s.nodes}</td>
                  <td className="num">{s.runs}</td>
                </tr>
              ))}
              {systems.length === 0 && <tr><td colSpan={5} className="muted">No system yet: make one below, or place a node and its system is made for it.</td></tr>}
            </tbody>
          </table>
          <div className="row" style={{ marginTop: 8 }}>
            <select value={service} onChange={(e) => setService(e.target.value)}>
              {services().map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
            </select>
            <button type="button" className="btn" onClick={() => void create()}>New system</button>
          </div>
          {current && d && (
            <>
              <h4 className="eyebrow" style={{ marginTop: 14 }}>Defaults · {current.name}</h4>
              <dl className="fields">
                <dt>Size (diameter)</dt><dd><LengthField metres={d.size} min={0.004} onCommit={(v) => { if (v != null) update(current.id, { size: v }); }} /></dd>
                <dt>Plane</dt><dd><select value={d.plane} onChange={(e) => update(current.id, { plane: e.target.value as Plane })}><option value="floor">floor</option><option value="ceiling">ceiling</option><option value="wall">wall</option></select></dd>
                <dt>Floor elevation</dt><dd><LengthField metres={d.floorElevation} onCommit={(v) => { if (v != null) update(current.id, { floorElevation: v }); }} /></dd>
                <dt>Ceiling elevation</dt><dd><LengthField metres={d.ceilingElevation} onCommit={(v) => { if (v != null) update(current.id, { ceilingElevation: v }); }} /></dd>
                <dt>Wall elevation</dt><dd><LengthField metres={d.wallElevation} onCommit={(v) => { if (v != null) update(current.id, { wallElevation: v }); }} /></dd>
                <dt>Slope</dt><dd><NumberField value={d.slope} min={0} max={0.5} suffix="m/m" onCommit={(v) => { if (v != null) update(current.id, { slope: v }); }} /></dd>
                <dt>Pair (supply and return)</dt><dd><input type="checkbox" checked={d.pair} onChange={(e) => update(current.id, { pair: e.target.checked })} /></dd>
                <dt>Separation</dt><dd><LengthField metres={d.separation} min={0} onCommit={(v) => { if (v != null) update(current.id, { separation: v }); }} /></dd>
                <dt>Material</dt><dd>
                  <select value={materialOf(current.service, d.material)?.id ?? ''} onChange={(e) => update(current.id, { material: e.target.value })}>
                    {materialsFor(current.service).map((m) => <option key={m.id} value={m.id}>{m.label}</option>)}
                  </select>
                  {rule && <span className="muted small"> {rule}</span>}
                </dd>
                {current.service === 'gas' && (
                  <>
                    <dt>Gas</dt><dd><select data-gas-kind value={d.gas ?? 'natural'} onChange={(e) => update(current.id, { gas: e.target.value as 'natural' | 'lpg' })}><option value="natural">Natural gas</option><option value="lpg">LPG</option></select></dd>
                    <dt>Pressure class</dt><dd><select data-gas-pressure value={d.pressure ?? 'low'} onChange={(e) => update(current.id, { pressure: e.target.value as 'low' | 'medium' })}><option value="low">Low pressure</option><option value="medium">Medium pressure</option></select></dd>
                  </>
                )}
                <dt title="Whether its runs cut their own openings in the walls, floors and roofs they cross (Manage → Cut order)">Runs cut walls, floors and roofs</dt>
                <dd><select data-system-cut value={current.cutHosts == null ? '' : current.cutHosts ? 'yes' : 'no'} onChange={(e) => void execute('mep.updateSystem', { id: current.id, cutHosts: e.target.value === '' ? null : e.target.value === 'yes' })}><option value="">as the cut order</option><option value="yes">yes</option><option value="no">no</option></select></dd>
                {current.style && (
                  <>
                    <dt title="How its runs and symbols draw in plan: colour and line type; over the plan's cut (on the ceiling) the line type they take there">{d.pair ? 'Supply line' : 'Line'}</dt>
                    <dd><LookFields look={current.style.line} onChange={(p) => style(current.id, 'line', p)} testId="mep-style-line" /></dd>
                    {d.pair && (
                      <>
                        <dt title="A paired run's return line">Return line</dt>
                        <dd><LookFields look={current.style.return} onChange={(p) => style(current.id, 'return', p)} testId="mep-style-return" /></dd>
                      </>
                    )}
                    <dt />
                    <dd><button type="button" className="btn small" onClick={() => void execute('mep.updateSystem', { id: current.id, style: null })}>Service's colours</button></dd>
                  </>
                )}
                {covered && (
                  <>
                    <dt title="Where the plans draw its devices' coverage circles: on the selected devices (on the screen only), on every device (printed too), or none">Coverage circles</dt>
                    <dd><select data-mep-coverage value={current.coverage ?? 'selected'} onChange={(e) => void execute('mep.updateSystem', { id: current.id, coverage: e.target.value === 'selected' ? null : e.target.value })}>
                      {COVERAGE_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
                    </select></dd>
                  </>
                )}
                <dt>Insulation</dt><dd><LengthField metres={d.insulation} min={0} onCommit={(v) => { if (v != null) update(current.id, { insulation: v }); }} /></dd>
                <dt>Bend radius</dt><dd><LengthField metres={d.bendRadius} min={0} onCommit={(v) => { if (v != null) update(current.id, { bendRadius: v }); }} /></dd>
              </dl>
            </>
          )}
          {plan && (
            <>
              <h4 className="eyebrow" style={{ marginTop: 14 }}>This plan</h4>
              <dl className="fields">
                <dt title="This plan's coverage circles, over every system's own setting">Coverage circles</dt>
                <dd><select data-mep-view-coverage value={typeof planNode?.props.mepCoverage === 'string' ? planNode.props.mepCoverage : ''} onChange={(e) => void execute('mep.setCoverage', { viewId: plan, mode: e.target.value || null })}>
                  <option value="">as each system</option>
                  {COVERAGE_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
                </select></dd>
              </dl>
            </>
          )}
          <h4 className="eyebrow" style={{ marginTop: 14 }}>New runs</h4>
          <dl className="fields">
            <dt>Pair (supply and return)</dt><dd><select value={prefs.pair == null ? '' : prefs.pair ? 'yes' : 'no'} onChange={(e) => setMep({ pair: e.target.value === '' ? null : e.target.value === 'yes' })}><option value="">as the system</option><option value="yes">paired</option><option value="no">single</option></select></dd>
            <dt>Plane</dt><dd><select value={prefs.plane ?? ''} onChange={(e) => setMep({ plane: (e.target.value || null) as Plane | null })}><option value="">as the system</option><option value="floor">floor</option><option value="ceiling">ceiling</option><option value="wall">wall</option></select></dd>
          </dl>
          <p className="muted small">Nodes and runs take the system's defaults unless a value is set on them.</p>
        </div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** A line's colour, line type and line type over the cut. */
function LookFields({ look, onChange, testId }: { look: LineLook; onChange: (patch: Partial<LineLook>) => void; testId: string }) {
  return (
    <span className="row" data-testid={testId}>
      <input type="color" value={look.color} title="Colour" onChange={(e) => onChange({ color: e.target.value })} />
      <select value={look.pattern} title="Line type" onChange={(e) => onChange({ pattern: e.target.value })}>
        {[...new Set([...LINE_TYPES, look.pattern])].map((p) => <option key={p} value={p}>{p}</option>)}
      </select>
      <span className="muted small">over the cut</span>
      <select value={look.above ?? 'overhead'} title="Line type over the plan's cut" onChange={(e) => onChange({ above: e.target.value })}>
        <option value="overhead">overhead dashes</option>
        {[...new Set([...LINE_TYPES.filter((p) => p !== 'solid'), ...(look.above && look.above !== 'overhead' ? [look.above] : [])])].map((p) => <option key={p} value={p}>{p}</option>)}
      </select>
    </span>
  );
}
