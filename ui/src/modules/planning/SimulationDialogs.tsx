/**
 * The 4D simulation's outputs (docs/plan-4d5d.md, stage 4). Stages sheet…: a sheet of the model at several dates — a
 * plan, a section or an elevation drawn by the engine at each (printed as the screen shows it), or the 3D view's pictures
 * taken at each — the dates at the end of each chapter of the WBS, each month, or chosen. Export video…: the 3D view's
 * frames from one day to another, a day or a week a frame, recorded as a video the system can play (WebM, or MP4 where
 * only that is offered: the macOS app), or, where no video can be recorded, the frames as PNG files in a ZIP.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Clapperboard, LayoutPanelTop, Plus, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { projectTitle, safeFileName, saveFile } from '../../engine/files';
import { useCamera } from '../../viewport/camera';
import { PendingDialog } from '../core/PendingDialog';
import { dayOf, showDate } from './gantt';
import { frameDays, isoOf, tasksOn, videoType, zipStore } from './simulation';
import { timelineNow, useSimulation } from './SimulationView';

/** A few frames drawn, so the 3D shows the looks of the day just set. */
const frames = (n: number) => new Promise<void>((resolve) => {
  let k = 0;
  const f = () => { if (++k >= n) resolve(); else requestAnimationFrame(f); };
  requestAnimationFrame(f);
});

/** The 3D as drawn now, copied onto a canvas at most `width` wide (null: no 3D view is shown). */
function grab(width: number): HTMLCanvasElement | null {
  const src = useCamera.getState().api?.canvas?.();
  if (!src || !src.width) return null;
  const k = Math.min(1, width / src.width);
  const c = document.createElement('canvas');
  // Even sides: the video encoders want them.
  c.width = Math.max(2, Math.round((src.width * k) / 2) * 2);
  c.height = Math.max(2, Math.round((src.height * k) / 2) * 2);
  c.getContext('2d')?.drawImage(src, 0, 0, c.width, c.height);
  return c;
}

const base64Of = (url: string) => url.slice(url.indexOf(',') + 1);

/** Whether a 3D view is shown in a pane (what the pictures and the video are taken from). */
const has3d = () => useUi.getState().panes.some((p) => p.view === '3d') && !!useCamera.getState().api?.canvas;

export function StagesDialog() {
  const timeline = useSimulation((s) => s.timeline);
  const cursor = useSimulation((s) => (s.on ? s.day : null));
  const nodes = useSession((s) => s.nodes);
  useEffect(() => { void timelineNow(); }, []);
  const views = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinView' && ['plan', 'section', 'elevation'].includes(String(n.props.kind))), [nodes]);
  const [source, setSource] = useState('3d');
  const [mode, setMode] = useState<'chapters' | 'months' | 'own'>('chapters');
  const [own, setOwn] = useState<number[]>([]);
  const [paper, setPaper] = useState('A1');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  if (!timeline) return <PendingDialog title="Stages sheet" />;
  const dates = mode === 'own' ? [...own].sort((a, b) => a - b) : mode === 'months' ? timeline.stages.months : timeline.stages.chapters;
  const make = async () => {
    if (!dates.length) { notify('Choose a date at least.'); return; }
    setBusy(true);
    try {
      let r: { sheetId: string } | null;
      if (source === '3d') {
        if (!has3d()) { notify('Show the 3D view in a pane: the stages are its pictures, as it looks now.'); return; }
        const before = useSimulation.getState();
        useSimulation.setState({ on: true, playing: false });
        const snapshots: { date: string; data: string }[] = [];
        for (const d of dates) {
          useSimulation.setState({ day: d });
          await frames(4);
          const c = grab(1400);
          if (c) snapshots.push({ date: isoOf(d), data: base64Of(c.toDataURL('image/jpeg', 0.88)) });
        }
        useSimulation.setState({ on: before.on, day: before.day });
        r = await execute('planning.stagesSheet', { snapshots, paper }) as { sheetId: string } | null;
      } else r = await execute('planning.stagesSheet', { viewId: source, dates: dates.map(isoOf), paper }) as { sheetId: string } | null;
      if (r?.sheetId) { close(); useUi.setState({ view: 'sheet', sheetId: r.sheetId }); }
    } finally { setBusy(false); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Stages sheet">
      <div className="dialog" data-testid="stages-dialog">
        <header className="dialog-head">
          <span className="panel-title">Stages sheet</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">The model as the schedule has it at each date, side by side on a new sheet, each labelled with its date and the tasks under way.</p>
          <dl className="fields">
            <dt>Drawn from</dt>
            <dd><select value={source} onChange={(e) => setSource(e.target.value)} data-testid="stages-source">
              <option value="3d">the 3D view, as it looks now</option>
              {views.map((v) => <option key={v.id} value={v.id} data-no-i18n>{v.name}</option>)}
            </select></dd>
            <dt>Dates</dt>
            <dd><select value={mode} onChange={(e) => setMode(e.target.value as typeof mode)} data-testid="stages-mode">
              <option value="chapters">the end of each chapter</option>
              <option value="months">the end of each month</option>
              <option value="own">chosen</option>
            </select></dd>
            <dt>Paper</dt>
            <dd><select value={paper} onChange={(e) => setPaper(e.target.value)} data-no-i18n>{['A0', 'A1', 'A2', 'A3'].map((p) => <option key={p}>{p}</option>)}</select></dd>
          </dl>
          {mode === 'own' && (
            <div className="row">
              <input type="date" min={isoOf(timeline.start)} max={isoOf(timeline.finish)} onChange={(e) => { const v = e.target.value; if (v && !own.includes(dayOf(v))) setOwn([...own, dayOf(v)]); }} data-testid="stages-add-date" />
              {cursor != null && <button type="button" className="link" onClick={() => !own.includes(cursor) && setOwn([...own, cursor])}><Plus size={12} />the cursor's date</button>}
            </div>
          )}
          <div className="stages-dates" data-testid="stages-dates">
            {dates.map((d, i) => (
              <span key={d} className="chip" title={tasksOn(timeline, d).map((t) => t.name).join(', ')} data-no-i18n>
                {`${i + 1}. ${showDate(isoOf(d))}`}
                {mode === 'own' && <button type="button" className="icon mini" onClick={() => setOwn(own.filter((x) => x !== d))}><X size={10} /></button>}
              </span>
            ))}
          </div>
          <div className="row">
            <button type="button" className="btn primary" disabled={busy || !dates.length} onClick={() => void make()} data-testid="stages-make"><LayoutPanelTop size={14} />Make the sheet</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export function VideoDialog() {
  const timeline = useSimulation((s) => s.timeline);
  useEffect(() => { void timelineNow(); }, []);
  const [from, setFrom] = useState<number | null>(null);
  const [to, setTo] = useState<number | null>(null);
  const [every, setEvery] = useState(1);
  const [fps, setFps] = useState(10);
  const [width, setWidth] = useState(1280);
  const [done, setDone] = useState<number | null>(null);
  const cancel = useRef(false);
  const close = () => { cancel.current = true; useUi.setState({ dialog: null }); };
  if (!timeline) return <PendingDialog title="Export video" />;
  const a = from ?? timeline.start, b = to ?? timeline.finish;
  const days = frameDays(a, Math.max(a, b), every);
  const kind = typeof MediaRecorder !== 'undefined' ? videoType((t) => MediaRecorder.isTypeSupported(t)) : null;
  const record = async () => {
    if (!has3d()) { notify('Show the 3D view in a pane: the video is what it shows, from where it looks now.'); return; }
    cancel.current = false;
    const before = useSimulation.getState();
    useSimulation.setState({ on: true, playing: false, recording: true });
    const title = safeFileName(`${projectTitle(useSession.getState().doc?.path)} 4D`);
    setDone(0);
    try {
      const first = grab(width);
      if (!first) return;
      const out = document.createElement('canvas');
      out.width = first.width; out.height = first.height;
      const g = out.getContext('2d')!;
      const label = (d: number) => {
        const under = tasksOn(timeline, d).map((t) => t.name).join(', ');
        const h = Math.max(18, Math.round(out.height / 30));
        g.font = `600 ${h}px system-ui, sans-serif`;
        g.fillStyle = 'rgba(255,255,255,0.82)';
        g.fillRect(0, out.height - h * 1.9, out.width, h * 1.9);
        g.fillStyle = '#1e2a3a';
        g.fillText(`${showDate(isoOf(d))}${under ? `  ·  ${under}` : ''}`, h * 0.6, out.height - h * 0.6, out.width - h * 1.2);
      };
      const draw = async (d: number) => {
        useSimulation.setState({ day: d });
        await frames(3);
        const c = grab(width);
        if (c) g.drawImage(c, 0, 0, out.width, out.height);
        label(d);
      };
      if (kind) {
        const stream = out.captureStream(fps);
        const rec = new MediaRecorder(stream, { mimeType: kind.mime, videoBitsPerSecond: 6_000_000 });
        const chunks: Blob[] = [];
        rec.ondataavailable = (e) => { if (e.data.size) chunks.push(e.data); };
        const stopped = new Promise<void>((resolve) => { rec.onstop = () => resolve(); });
        await draw(days[0]);
        rec.start(1000);
        const frameMs = 1000 / fps;
        for (let i = 0; i < days.length && !cancel.current; i++) {
          const t0 = performance.now();
          if (i > 0) await draw(days[i]);
          (stream.getVideoTracks()[0] as unknown as { requestFrame?: () => void }).requestFrame?.();
          setDone(i + 1);
          const left = frameMs - (performance.now() - t0);
          if (left > 0) await new Promise((r) => setTimeout(r, left));
        }
        // The last frame held a moment.
        await new Promise((r) => setTimeout(r, 600));
        rec.stop();
        await stopped;
        if (cancel.current) return;
        const bytes = new Uint8Array(await new Blob(chunks, { type: kind.mime }).arrayBuffer());
        const saved = await saveFile(bytes, `${title}.${kind.ext}`, kind.mime.split(';')[0]);
        if (saved) notify(`Saved ${saved}.`);
      } else {
        // No video recorder here: the frames as PNG files in a ZIP.
        const files: { name: string; bytes: Uint8Array }[] = [];
        for (let i = 0; i < days.length && !cancel.current; i++) {
          await draw(days[i]);
          const blob = await new Promise<Blob | null>((r) => out.toBlob(r, 'image/png'));
          if (blob) files.push({ name: `${title}-${String(i + 1).padStart(4, '0')}-${isoOf(days[i])}.png`, bytes: new Uint8Array(await blob.arrayBuffer()) });
          setDone(i + 1);
        }
        if (cancel.current) return;
        const saved = await saveFile(zipStore(files), `${title} frames.zip`, 'application/zip');
        if (saved) notify(`Saved ${saved}.`);
      }
      useUi.setState({ dialog: null });
    } finally {
      useSimulation.setState({ recording: false, on: before.on, day: before.day });
      setDone(null);
    }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Export video">
      <div className="dialog" data-testid="video-dialog">
        <header className="dialog-head">
          <span className="panel-title">Export video</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">The 3D view from where it looks now, the model built day by day, the date and the tasks under way written under it.</p>
          <dl className="fields" data-i18n-ctx="period">
            <dt>From</dt>
            <dd><input type="date" value={isoOf(a)} min={isoOf(timeline.start)} max={isoOf(timeline.finish)} onChange={(e) => e.target.value && setFrom(dayOf(e.target.value))} /></dd>
            <dt>To</dt>
            <dd><input type="date" value={isoOf(b)} min={isoOf(timeline.start)} max={isoOf(timeline.finish)} onChange={(e) => e.target.value && setTo(dayOf(e.target.value))} /></dd>
            <dt>A frame</dt>
            <dd><select value={every} onChange={(e) => setEvery(Number(e.target.value))} data-testid="video-every">
              <option value={1}>a day</option><option value={7}>a week</option>
            </select></dd>
            <dt>Frames a second</dt>
            <dd><select value={fps} onChange={(e) => setFps(Number(e.target.value))} data-no-i18n>{[5, 10, 15, 24].map((v) => <option key={v} value={v}>{v}</option>)}</select></dd>
            <dt>Width</dt>
            <dd><select value={width} onChange={(e) => setWidth(Number(e.target.value))} data-no-i18n>{[960, 1280, 1920].map((v) => <option key={v} value={v}>{`${v} px`}</option>)}</select></dd>
          </dl>
          <p className="muted small">
            <b data-no-i18n>{`${days.length} · ${(days.length / fps).toFixed(1)} s · ${kind ? kind.ext.toUpperCase() : 'PNG + ZIP'}`}</b>
          </p>
          {!kind && <p className="muted small">This browser records no video: the frames are saved as PNG files in a ZIP.</p>}
          {done != null && <progress className="video-progress" max={days.length} value={done} data-testid="video-progress" />}
          <div className="row">
            <button type="button" className="btn primary" disabled={done != null} onClick={() => void record()} data-testid="video-record"><Clapperboard size={14} />Record</button>
            {done != null && <button type="button" className="btn" onClick={() => { cancel.current = true; }}>Cancel</button>}
          </div>
        </div>
      </div>
    </div>
  );
}
