/**
 * View → Sun and shadows (docs/plan-visualisation.md, stage 1): the 3D view lit by the
 * sun of the project's place on a day at an hour, with its shadows; the hour
 * slid or played through the day.
 */

import { useEffect, useState } from 'react';
import { Pause, Play, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { applySun, hourText, setSun, today, useSun, type SunInfo } from './sun';

const year = new Date().getFullYear();
export const SUN_DAYS: [string, string][] = [[`${year}-03-21`, 'Equinox (21 March)'], [`${year}-06-21`, 'Summer solstice (21 June)'], [`${year}-12-21`, 'Winter solstice (21 December)']];

/** The day and hour, and where the sun is then; shared with the urban module's sun study. */
export function SunFields() {
  const st = useSun();
  const [info, setInfo] = useState<SunInfo | null>(null);
  useEffect(() => { void applySun().then(setInfo); }, [st.on, st.date, st.hour]);
  // Playing: a quarter of an hour every eighth of a second, from 6:00 to 20:00, round again.
  useEffect(() => {
    if (!st.playing) return;
    const t = setInterval(() => { const h = useSun.getState().hour + 0.25; setSun({ hour: h > 20 ? 6 : h }); }, 125);
    return () => clearInterval(t);
  }, [st.playing]);
  return (
    <>
      <label className="row"><input type="checkbox" checked={st.on} onChange={(e) => { setSun({ on: e.target.checked, playing: e.target.checked && st.playing }); if (e.target.checked) useUi.setState({ view: '3d' }); }} /> The sun lights the 3D view, with its shadows</label>
      <dl className="fields" style={{ marginTop: 8 }}>
        <dt>Day</dt>
        <dd className="row" style={{ flexWrap: 'wrap', gap: 6 }}>
          <input type="date" value={st.date} onChange={(e) => setSun({ date: e.target.value || today() })} style={{ width: 150 }} />
          {SUN_DAYS.map(([d, label]) => <button key={d} type="button" className="btn small" title={label} onClick={() => setSun({ date: d })}>{label.split(' (')[0]}</button>)}
        </dd>
        <dt>Hour</dt>
        <dd className="row">
          <input type="range" min={5} max={21} step={0.25} value={st.hour} onChange={(e) => setSun({ hour: Number(e.target.value) })} style={{ flex: 1 }} aria-label="Hour" />
          <span className="num" style={{ width: 48 }}>{hourText(st.hour)}</span>
          <button type="button" className="icon" title={st.playing ? 'Stop' : 'Play the day'} onClick={() => setSun({ playing: !st.playing, on: true })}>{st.playing ? <Pause size={14} /> : <Play size={14} />}</button>
        </dd>
        {info && <>
          <dt>The sun</dt>
          <dd>{info.altitude > 0 ? `${info.altitude.toFixed(1)}° above the horizon, ${info.azimuth.toFixed(0)}° from north` : 'below the horizon'}</dd>
          <dt>Place</dt>
          <dd>{`${info.lat.toFixed(4)}° N, ${info.lon.toFixed(4)}° E`}{!info.placeKnown && <span className="error-text" style={{ display: 'block' }}>Bucharest: the project has no coordinate system and survey point (Manage → Location)</span>}</dd>
          <dt>Time zone</dt>
          <dd>{`UTC${info.utcOffset >= 0 ? '+' : ''}${info.utcOffset}`}</dd>
          {info.summerTime && <><dt>Summer time</dt><dd>the European rule</dd></>}
        </>}
      </dl>
    </>
  );
}

export function SunDialog() {
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Sun and shadows" style={{ width: 'min(620px, 94vw)' }} onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Sun and shadows</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <SunFields />
          <p className="muted small">The medium and realistic views (View → Display quality) always have a sun: this day and hour.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
