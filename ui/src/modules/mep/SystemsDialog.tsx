/**
 * The systems of the building: one per service, each with the defaults its
 * nodes and runs take (size, plane and elevations, slope, pair, material…).
 * One is current: new nodes and runs go in it. New runs may also be drawn
 * paired or single, in a plane, over the system's defaults.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialOf, materialsFor, services, systemsQuery, type Defaults, type SystemInfo } from './network';
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
  // The material's laying rule in a few words, when the product knows the rules (mep-pro).
  const rule = current && d ? mepHooks.ruleText(current.service, materialOf(current.service, d.material)?.id ?? null) : null;
  return (
    <div className="dialog-backdrop" onClick={close}>
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
                <dt title="Whether its runs cut their own openings in the walls and floors they cross (Manage → Cut order)">Runs cut walls and floors</dt>
                <dd><select data-system-cut value={current.cutHosts == null ? '' : current.cutHosts ? 'yes' : 'no'} onChange={(e) => void execute('mep.updateSystem', { id: current.id, cutHosts: e.target.value === '' ? null : e.target.value === 'yes' })}><option value="">as the cut order</option><option value="yes">yes</option><option value="no">no</option></select></dd>
                <dt>Insulation</dt><dd><LengthField metres={d.insulation} min={0} onCommit={(v) => { if (v != null) update(current.id, { insulation: v }); }} /></dd>
                <dt>Bend radius</dt><dd><LengthField metres={d.bendRadius} min={0} onCommit={(v) => { if (v != null) update(current.id, { bendRadius: v }); }} /></dd>
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
