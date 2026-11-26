/**
 * The sun study on the CDE server (Atrium docs/atrium-server.md § The sun study on the server; engine/serverAnalysis.ts), shown under
 * Publishing where the server offers `sunStudy`: a day and its hours run on main's latest commit by the server, with the module and
 * the licence it has; each study's picture and its hours (the sun's height and direction, the shadows' area). Only what the server
 * answers: the study itself is the server's. One lazy chunk.
 */

import { useEffect, useState } from 'react';
import { Play, RefreshCw } from 'lucide-react';
import { notify } from '../../app/store';
import { when } from './format';
import { AtriumError } from '../../engine/atrium';
import { nextSolstice, runSunStudy, sunPicture, sunStudies, type SunStudy } from '../../engine/serverAnalysis';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

function Picture({ server, project, id }: { server: string; project: string; id: string }) {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    let made: string | null = null;
    void sunPicture(server, project, id).then((u) => { made = u; if (live) setUrl(u); else URL.revokeObjectURL(u); }).catch(() => { /* the table says it all */ });
    return () => { live = false; if (made) URL.revokeObjectURL(made); };
  }, [server, project, id]);
  return url ? <img className="cde-sun-picture" src={url} alt="" style={{ maxWidth: '100%', background: '#fff' }} /> : null;
}

function StudyView({ server, project, s }: { server: string; project: string; s: SunStudy }) {
  return (
    <section className="cde-checked-file" data-cde-sun-study={s.id}>
      <p className="small cde-checked-name"><b data-no-i18n>{s.params.date}</b> <span className="muted" data-no-i18n>· {s.params.times} · {s.commit.slice(0, 8)} · {s.ranBy?.name ?? ''} · {when(s.ran)}</span></p>
      {s.status !== 'done' && <p className="cde-warn small"><span>The sun study could not run on the server.</span> <span className="muted" data-no-i18n>{s.error ?? ''}</span></p>}
      {s.place && !s.place.known && <p className="muted small">The project has no georeference: Lapin's default place was used.</p>}
      {s.picture && <Picture server={server} project={project} id={s.id} />}
      {s.hours.length > 0 && (
        <table className="cde-members small">
          <thead><tr><th>Hour</th><th>Sun height</th><th>Azimuth</th><th>Shadows (m²)</th></tr></thead>
          <tbody>{s.hours.map((h) => (
            <tr key={h.hour}>
              <td data-no-i18n>{h.label}</td>
              {h.up ? <><td data-no-i18n>{h.altitude}°</td><td data-no-i18n>{h.azimuth}°</td><td data-no-i18n>{h.area}</td></> : <td colSpan={3}>The sun is down.</td>}
            </tr>
          ))}</tbody>
        </table>
      )}
    </section>
  );
}

/** Publishing → Sun study: the studies made on the server, and a new one (editors and owners with a seat). */
export function SunStudyPanel({ server, project }: { server: string; project: string }) {
  const [list, setList] = useState<SunStudy[] | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [date, setDate] = useState(nextSolstice());
  const [times, setTimes] = useState('9, 12, 15');
  const [busy, setBusy] = useState(false);
  const load = () => {
    setFailed(null);
    sunStudies(server, project).then(setList).catch((e) => { setFailed(e instanceof AtriumError && e.code === 'not_licensed' ? (e.serverMessage ?? message(e)) : message(e)); setList([]); });
  };
  useEffect(load, [server, project]);
  const run = async () => {
    setBusy(true);
    try {
      const s = await runSunStudy(server, project, { date, times });
      setList((l) => [s, ...(l ?? []).filter((x) => x.id !== s.id)]);
      notify('The sun study ran on the server.');
    } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  return (
    <div className="cde-register" data-cde-sun={list ? list.length : 'loading'}>
      <p className="muted small">The shadows on the ground at each hour of a day, worked out on the server by Lapin at the project's place, on main's latest commit.</p>
      <div className="cde-actions">
        <label className="small"><span>Day</span> <input type="date" value={date} onChange={(e) => setDate(e.target.value)} data-cde-sun-date /></label>
        <label className="small"><span>Hours</span> <input type="text" value={times} size={12} onChange={(e) => setTimes(e.target.value)} data-cde-sun-times /></label>
        <button type="button" className="btn" disabled={busy || !date} onClick={() => void run()} data-cde-sun-run><Play size={13} />Run the sun study</button>
        <button type="button" className="icon" title="Refresh" onClick={() => { setList(null); load(); }}><RefreshCw size={12} /></button>
      </div>
      {list === null && <p className="muted small">Loading…</p>}
      {failed && <p className="muted small" data-no-i18n>{failed}</p>}
      {list?.length === 0 && !failed && <p className="muted small">No sun study on this project yet.</p>}
      {list?.map((s) => <StudyView key={s.id} server={server} project={project} s={s} />)}
    </div>
  );
}
