/**
 * A view's sun shadows in its properties (docs/plan-visualisation.md, vector shadows): an elevation, a section or a
 * site plan draws the shadows the sun casts on what it sees, filled under its lines, so they print. The sun is none,
 * the project's on the view's own day and hour, or the view's own (azimuth from the way it looks, altitude), by
 * default the drawing convention: over the left shoulder, along a cube's diagonal.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { hourText, useSun } from './sun';

/** The convention's altitude: the rays along a cube's diagonal (ViewShadows.ConventionAltitude). */
export const CONVENTION_ALTITUDE = (Math.atan(1 / Math.SQRT2) * 180) / Math.PI;

export type ShadowMode = 'none' | 'project' | 'view';

export function shadowModeOf(view: NodeDto): ShadowMode {
  const m = view.props.shadows;
  return m === 'project' || m === 'view' ? m : 'none';
}

/** Whether a view of this kind draws shadows: an elevation, a section, a site plan. */
export const shadowsAllowed = (view: NodeDto) => view.props.kind === 'elevation' || view.props.kind === 'section' || (view.props.kind === 'plan' && typeof view.props.site === 'string');

export function ViewShadowFields({ view }: { view: NodeDto }) {
  const mode = shadowModeOf(view);
  const site = view.props.kind === 'plan';
  const num = (k: string, d: number) => (typeof view.props[k] === 'number' ? (view.props[k] as number) : d);
  const [azimuth, setAzimuth] = useState('');
  const [altitude, setAltitude] = useState('');
  const [hour, setHour] = useState('');
  useEffect(() => {
    setAzimuth(String(Math.round(num('shadowAzimuth', site ? 135 : 45) * 10) / 10));
    setAltitude(String(Math.round(num('shadowAltitude', CONVENTION_ALTITUDE) * 100) / 100));
    setHour(hourText(num('shadowHour', 14)));
  }, [view.id, view.props.shadowAzimuth, view.props.shadowAltitude, view.props.shadowHour]); // eslint-disable-line react-hooks/exhaustive-deps
  const set = (patch: Record<string, unknown>) => void execute('view.setShadows', { id: view.id, ...patch });
  const parse = (s: string) => { const v = Number(s.replace(',', '.')); return Number.isFinite(v) ? v : null; };
  const parseHour = (s: string) => {
    const m = /^(\d{1,2})(?::(\d{2}))?$/.exec(s.trim());
    return m ? Number(m[1]) + (m[2] ? Number(m[2]) / 60 : 0) : parse(s);
  };
  const date = typeof view.props.shadowDate === 'string' ? view.props.shadowDate : `${new Date().getFullYear()}-03-21`;
  return (
    <>
      <dt title="The shadows the sun casts on what the view sees, filled under its lines; they print on sheets, in PDF and DXF">Sun shadows</dt>
      <dd>
        <select value={mode} data-testid="view-shadows" onChange={(e) => set({ mode: e.target.value })}>
          <option value="none">none</option>
          <option value="view">own sun</option>
          <option value="project">project sun</option>
        </select>
      </dd>
      {mode === 'view' && (
        <>
          <dt title={site ? 'Where the sun comes from, from the sheet\'s bottom (0°), turning left; 135°: from the top left' : 'Where the sun comes from, from behind you (0°), turning left; 45°: over your left shoulder'}>Sun from (°)</dt>
          <dd><input inputMode="decimal" data-testid="view-shadow-azimuth" value={azimuth} onChange={(e) => setAzimuth(e.target.value)} onBlur={() => { const v = parse(azimuth); if (v != null) set({ azimuth: v }); }} /></dd>
          <dt title="How high the sun stands; 35.26°: its rays along a cube's diagonal, so a shadow is as deep as what casts it stands out">Sun altitude (°)</dt>
          <dd>
            <input inputMode="decimal" value={altitude} onChange={(e) => setAltitude(e.target.value)} onBlur={() => { const v = parse(altitude); if (v != null) set({ altitude: v }); }} />
            <button type="button" className="link small" onClick={() => set({ reset: true })}>Drawing convention</button>
          </dd>
        </>
      )}
      {mode === 'project' && (
        <>
          <dt>Day</dt>
          <dd><input type="date" value={date} onChange={(e) => e.target.value && set({ date: e.target.value })} /></dd>
          <dt>Hour</dt>
          <dd>
            <input value={hour} onChange={(e) => setHour(e.target.value)} onBlur={() => { const v = parseHour(hour); if (v != null) set({ hour: v }); }} style={{ width: 80 }} />
            <button type="button" className="link small" title="The day and hour of View → Sun and shadows" onClick={() => { const s = useSun.getState(); set({ date: s.date, hour: s.hour }); }}>As in Sun and shadows</button>
          </dd>
        </>
      )}
      {mode !== 'none' && (
        <>
          <dt>Shadow colour</dt>
          <dd><input type="color" value={typeof view.props.shadowColor === 'string' ? view.props.shadowColor : '#cfcac0'} onChange={(e) => set({ color: e.target.value })} /></dd>
        </>
      )}
    </>
  );
}
