/** The project's location: the base point (its elevation, the angle to true north), the survey point (its site coordinates) and the coordinate system they are in, shown in plans or not. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

type P = Record<string, number>;
interface Loc { base?: P; survey?: P; show?: boolean; crs?: string }
interface CrsInfo { id: string; name: string }

export function LocationDialog() {
  const nodes = useSession((s) => s.nodes);
  const [l, setL] = useState<Loc | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<Loc>('project.location').then((r) => setL(r ?? {})); }, [nodes]);
  const systems = (useSession.getState().info?.catalogs['core.crs'] as unknown as CrsInfo[] | undefined) ?? [];
  if (!l) return null;
  const field = (point: 'base' | 'survey', key: string, label: string) => (
    <label key={key} className="row"><span className="muted small loc-label">{label}</span>
      <input type="number" step={0.001} defaultValue={l[point]?.[key] ?? 0} key={`${point}:${key}:${l[point]?.[key] ?? 0}`}
        onBlur={(e) => void execute('project.location', { [point]: { [key]: Number(e.target.value) || 0 } })} />
    </label>
  );
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Location" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Location</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body loc">
          <label className="row"><input type="checkbox" checked={!!l.show} onChange={(e) => void execute('project.location', { show: e.target.checked })} /> Show the base and survey points in plans</label>
          <label className="row"><span className="muted small loc-label">Coordinate system</span>
            <select value={l.crs ?? ''} title="What the survey point's eastings and northings are in; imports in another system are converted to it"
              onChange={(e) => void execute('project.location', { crs: e.target.value || null })}>
              <option value="">None (local)</option>
              {systems.map((c) => <option key={c.id} value={c.id}>{c.id} · {c.name}</option>)}
            </select>
          </label>
          <h4>Base point</h4>
          {field('base', 'x', 'x (m)')}{field('base', 'y', 'y (m)')}{field('base', 'elevation', '±0.00 at (m)')}{field('base', 'northAngle', 'True north (°)')}
          <h4>Survey point</h4>
          {l.survey ? <>
            {field('survey', 'x', 'x (m)')}{field('survey', 'y', 'y (m)')}{field('survey', 'easting', 'Easting (m)')}{field('survey', 'northing', 'Northing (m)')}{field('survey', 'elevation', 'Elevation (m)')}
            <button type="button" className="btn" onClick={() => void execute('project.location', { survey: null })}>Remove the survey point</button>
          </> : <button type="button" className="btn" onClick={() => void execute('project.location', { survey: { x: 0, y: 0, easting: 0, northing: 0, elevation: 0 } })}>Add a survey point</button>}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
