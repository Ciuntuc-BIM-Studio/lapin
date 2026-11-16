/** The project's location: the base point (its elevation, the angle to true north), the survey point (its site coordinates) and the coordinate system they are in, shown in plans or not. */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { PendingDialog } from './PendingDialog';

type P = Record<string, number>;
interface Loc { base?: P; survey?: P; tie?: P; show?: boolean; crs?: string }
interface CrsInfo { id: string; name: string }

export function LocationDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [l, setL] = useState<Loc | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void query<Loc>('project.location').then((r) => { if (live) setL(r ?? {}); });
    return () => { live = false; };
  }, [nodes]);
  const systems = (useSession.getState().info?.catalogs['core.crs'] as unknown as CrsInfo[] | undefined) ?? [];
  if (!l) return <PendingDialog title="Location" />;
  // A length in the project's unit; an easting or northing in the coordinate system's metres; an angle in degrees. Sent only when it changes.
  const field = (point: 'base' | 'survey', key: string, label: string, kind: 'length' | 'map' | 'angle' = 'length') => {
    const value = l[point]?.[key] ?? 0;
    const put = (v: number | null) => { if (v != null) void execute('project.location', { [point]: { [key]: v } }); };
    return (
      <label key={key} className="row"><span className="muted small loc-label">{label}</span>
        {kind === 'length' ? <LengthField metres={value} onCommit={put} /> : <NumberField value={value} suffix={kind === 'angle' ? '°' : 'm'} onCommit={put} />}
      </label>
    );
  };
  // The base point's latitude and longitude (WGS 84, degrees): empty when unknown; with a coordinate system and no survey point they tie the project to the map.
  const place = (key: 'latitude' | 'longitude', label: string) => (
    <label key={key} className="row"><span className="muted small loc-label">{label}</span>
      <NumberField value={l.base?.[key] ?? null} suffix="°" blank title="WGS 84, in degrees; empty when unknown" onCommit={(v) => void execute('project.location', { base: { [key]: v } })} />
    </label>
  );
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Location" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Location</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body loc">
          <label className="row"><input type="checkbox" checked={!!l.show} onChange={(e) => void execute('project.location', { show: e.target.checked })} /> Show the internal origin, the base point and the survey point in plans and 3D</label>
          <label className="row"><span className="muted small loc-label">Coordinate system</span>
            <select value={l.crs ?? ''} title="What the survey point's eastings and northings are in; imports in another system are converted to it"
              onChange={(e) => void execute('project.location', { crs: e.target.value || null })}>
              <option value="">None (local)</option>
              {systems.map((c) => <option key={c.id} value={c.id}>{c.id} · {c.name}</option>)}
            </select>
          </label>
          <h4>Base point</h4>
          {field('base', 'x', 'x')}{field('base', 'y', 'y')}{field('base', 'elevation', '±0.00 at')}{field('base', 'northAngle', 'True north', 'angle')}
          {place('latitude', 'Latitude')}{place('longitude', 'Longitude')}
          {!l.survey && l.tie && <p className="muted small"><span>The latitude and longitude tie the project to the map at the base point</span> {`E ${(l.tie.easting ?? 0).toFixed(3)} N ${(l.tie.northing ?? 0).toFixed(3)}`}</p>}
          <h4>Survey point</h4>
          {l.survey ? <>
            {field('survey', 'x', 'x')}{field('survey', 'y', 'y')}{field('survey', 'easting', 'Easting', 'map')}{field('survey', 'northing', 'Northing', 'map')}{field('survey', 'elevation', 'Elevation')}
            <button type="button" className="btn" onClick={() => void execute('project.location', { survey: null })}>Remove the survey point</button>
          </> : <button type="button" className="btn" onClick={() => void execute('project.location', { survey: { x: 0, y: 0, easting: 0, northing: 0, elevation: 0 } })}>Add a survey point</button>}
        </div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
