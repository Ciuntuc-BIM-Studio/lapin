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

const num = (t: string) => Number(t.replace(',', '.'));

export function SystemsDialog() {
  const version = useSession((s) => s.version);
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
                  <td><input className="cell" defaultValue={s.name} key={s.id + s.name} onBlur={(e) => { if (e.target.value.trim() && e.target.value.trim() !== s.name) rename(s.id, e.target.value.trim()); }} /></td>
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
                <dt>Size (diameter)</dt><dd><input className="num" type="number" step={0.001} min={0.004} value={d.size} onChange={(e) => update(current.id, { size: Math.max(0.004, num(e.target.value) || 0.004) })} /> <span className="muted">m</span></dd>
                <dt>Plane</dt><dd><select value={d.plane} onChange={(e) => update(current.id, { plane: e.target.value as Plane })}><option value="floor">floor</option><option value="ceiling">ceiling</option><option value="wall">wall</option></select></dd>
                <dt>Floor elevation</dt><dd><input className="num" type="number" step={0.05} value={d.floorElevation} onChange={(e) => update(current.id, { floorElevation: num(e.target.value) || 0 })} /> <span className="muted">m</span></dd>
                <dt>Ceiling elevation</dt><dd><input className="num" type="number" step={0.05} value={d.ceilingElevation} onChange={(e) => update(current.id, { ceilingElevation: num(e.target.value) || 0 })} /> <span className="muted">m</span></dd>
                <dt>Wall elevation</dt><dd><input className="num" type="number" step={0.05} value={d.wallElevation} onChange={(e) => update(current.id, { wallElevation: num(e.target.value) || 0 })} /> <span className="muted">m</span></dd>
                <dt>Slope</dt><dd><input className="num" type="number" step={0.005} min={0} max={0.5} value={d.slope} onChange={(e) => update(current.id, { slope: Math.max(0, num(e.target.value) || 0) })} /> <span className="muted">m/m</span></dd>
                <dt>Pair (supply and return)</dt><dd><input type="checkbox" checked={d.pair} onChange={(e) => update(current.id, { pair: e.target.checked })} /></dd>
                <dt>Separation</dt><dd><input className="num" type="number" step={0.01} min={0} value={d.separation} onChange={(e) => update(current.id, { separation: Math.max(0, num(e.target.value) || 0) })} /> <span className="muted">m</span></dd>
                <dt>Material</dt><dd>
                  <select value={materialOf(current.service, d.material)?.id ?? ''} onChange={(e) => update(current.id, { material: e.target.value })}>
                    {materialsFor(current.service).map((m) => <option key={m.id} value={m.id}>{m.label}</option>)}
                  </select>
                  {rule && <span className="muted small"> {rule}</span>}
                </dd>
                <dt>Insulation</dt><dd><input className="num" type="number" step={0.005} min={0} value={d.insulation} onChange={(e) => update(current.id, { insulation: Math.max(0, num(e.target.value) || 0) })} /> <span className="muted">m</span></dd>
                <dt>Bend radius</dt><dd><input className="num" type="number" step={0.01} min={0} value={d.bendRadius} onChange={(e) => update(current.id, { bendRadius: Math.max(0, num(e.target.value) || 0) })} /> <span className="muted">m</span></dd>
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
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
