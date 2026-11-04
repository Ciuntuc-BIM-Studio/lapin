/**
 * The 4D simulation (docs/plan-4d5d.md, stage 4): Planning → Simulate shows the model as the schedule has it on a day — not
 * started hidden (or as ghosts), under way in its colour, built as it is, being demolished red then gone, the formwork and
 * scaffolds only while they stand, a layered wall's plaster after its brick. The day is the Gantt's date line: dragged on
 * its time scale, or played (▶, the speed in days a second, a step of a day or a week) from the bar at the Gantt's top; a
 * card over the model view says the day and what the colours mean. The engine sends the timeline once a change; each
 * frame's looks are worked out here (simulation.ts) and only what changed is restyled, the meshes kept, so a large model
 * plays smoothly. A saved 3D view with a State at date shows that day even with the simulation off.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { create } from 'zustand';
import { Clapperboard, LayoutPanelTop, Pause, Play, Settings2, SkipBack, SkipForward, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { setLooks } from '../../platform/looks';
import { dayOf, showDate } from './gantt';
import { openGantt } from './schedule';
import {
  advance, clampDay, counts, DEFAULT_LOOK, isoOf, legend, lookKit, looksAt, prepare, SPEEDS, step, tasksOn, type SimLookDto, type SimTimeline, type TimelineDto,
} from './simulation';

interface SimulationState {
  on: boolean;
  /** The day shown (a day number), the Gantt's date line. */
  day: number | null;
  playing: boolean;
  /** Days a second. */
  speed: number;
  unit: 'day' | 'week';
  timeline: SimTimeline | null;
  version: number;
  /** A video being recorded: the bar waits. */
  recording: boolean;
}

export const useSimulation = create<SimulationState>(() => ({ on: false, day: null, playing: false, speed: 5, unit: 'day', timeline: null, version: -2, recording: false }));

let asking: number | null = null;

/** The timeline asked again after a change to the model (once, whoever needs it). */
async function refresh(version: number): Promise<SimTimeline | null> {
  const s = useSimulation.getState();
  if (s.version === version && s.timeline) return s.timeline;
  if (asking === version) return null;
  asking = version;
  const t = await query<TimelineDto>('planning.timeline');
  if (asking !== version) return null;
  asking = null;
  if (!t) return null;
  const timeline = prepare(t);
  const day = s.day == null ? timeline.start : clampDay(s.day, timeline);
  useSimulation.setState({ timeline, version, day });
  return timeline;
}

/** The timeline as it is now (asked for when it is not yet). */
export async function timelineNow(): Promise<SimTimeline | null> {
  return refresh(useSession.getState().version);
}

/** The simulation on or off; on, the Gantt opens under the view, the day at the schedule's start (or where it was). */
export async function toggleSimulation(on = !useSimulation.getState().on): Promise<void> {
  if (!on) { useSimulation.setState({ on: false, playing: false }); return; }
  openGantt();
  useSimulation.setState({ on: true });
  const t = await timelineNow();
  if (t && !t.elements.length) notify('No element is in a task yet: generate the schedule, or add elements to its tasks, to see them built.');
}

/** The day shown moved (the simulation turned on when it was off). */
export function setDay(day: number): void {
  const s = useSimulation.getState();
  const t = s.timeline;
  useSimulation.setState({ day: t ? clampDay(day, t) : Math.round(day), on: true });
}

/** The State at date of the 3D views the panes show (the first with one), for the 3D when the simulation is off. */
function useViewDate(): number | null {
  const ids = useUi((s) => s.panes.map((p) => (p.view === '3d' ? p.view3dId : null)).filter(Boolean).join(','));
  const nodes = useSession((s) => s.nodes);
  for (const id of ids.split(',')) {
    const d = id ? nodes.get(id)?.props.stateDate : null;
    if (typeof d === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(d)) return dayOf(d);
  }
  return null;
}

/** Mounted while the workspace is open: the timeline kept, the looks of the day given to the views, playback run. */
export function SimulationLooks() {
  const on = useSimulation((s) => s.on);
  const day = useSimulation((s) => s.day);
  const playing = useSimulation((s) => s.playing);
  const timeline = useSimulation((s) => s.timeline);
  const version = useSession((s) => s.version);
  const render = useSession((s) => s.render);
  const viewDate = useViewDate();
  const shown = on ? day : viewDate;
  const need = on || viewDate != null;
  useEffect(() => { if (need) void refresh(version); }, [need, version]);
  const kit = useMemo(() => lookKit(timeline?.look ?? DEFAULT_LOOK), [timeline]);
  const last = useRef('');
  useEffect(() => {
    if (shown == null || !timeline) {
      if (last.current) { setLooks('planning', {}); last.current = ''; }
      return;
    }
    const { looks, signature } = looksAt(timeline, shown, kit, kit.look.unlinked === 'hide' ? render.keys() : []);
    if (signature === last.current) return;
    last.current = signature;
    setLooks('planning', looks);
  }, [shown, timeline, kit, render]);
  // Playing: the day moves on with time, at the speed, until the finish.
  useEffect(() => {
    if (!playing || !timeline) return;
    let pos = useSimulation.getState().day ?? timeline.start;
    if (pos >= timeline.finish) pos = timeline.start;
    let at = performance.now();
    let frame = 0;
    const tick = (now: number) => {
      const s = useSimulation.getState();
      const r = advance(pos, now - at, s.speed, timeline);
      at = now;
      pos = r.pos;
      if (Math.floor(pos) !== s.day) useSimulation.setState({ day: Math.floor(pos) });
      if (!r.playing) { useSimulation.setState({ playing: false }); return; }
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [playing, timeline]);
  return null;
}

/** The colours and what each means, the day's counts beside. */
function Legend({ look, tally, baseline }: { look: SimLookDto; tally?: Partial<Record<string, number>>; baseline: boolean }) {
  return (
    <span className="sim-legend" data-testid="sim-legend">
      {legend(look, baseline).map((x) => (
        <span key={x.state} className="sim-key" data-state={x.state}>
          <i style={{ background: x.color }} aria-hidden />
          <span>{x.label}</span>
          {tally?.[x.state] ? <b data-no-i18n>{tally[x.state]}</b> : null}
        </span>
      ))}
    </span>
  );
}

/** The simulation's settings: the colours, what is not started and what no task takes, layers apart. */
function SimulationSettings({ look, onClose }: { look: SimLookDto; onClose: () => void }) {
  const set = (args: Record<string, unknown>) => void execute('planning.setSimulation', args);
  const colour = (key: 'active' | 'demolishing' | 'temporary' | 'planned' | 'late' | 'ahead', label: string) => (
    <label className="sim-colour"><input type="color" value={look[key]} onChange={(e) => set({ [key]: e.target.value })} data-sim-colour={key} /><span>{label}</span></label>
  );
  return (
    <div className="sim-settings" role="dialog" aria-label="Simulation settings" data-testid="sim-settings" onPointerDown={(e) => e.stopPropagation()}>
      <div className="gantt-link-title"><span>Simulation settings</span><button type="button" className="icon mini" title="Close" onClick={onClose}><X size={12} /></button></div>
      {colour('active', 'Under way')}
      {colour('demolishing', 'Being demolished')}
      {colour('temporary', 'Formwork and scaffolding')}
      {colour('planned', 'Ghosts')}
      <label>Not started <select value={look.plannedMode} onChange={(e) => set({ plannedMode: e.target.value })} data-testid="sim-planned">
        <option value="hide">hidden</option><option value="ghost">as ghosts</option>
      </select></label>
      <label>Elements without a task <select value={look.unlinked} onChange={(e) => set({ unlinked: e.target.value })}>
        <option value="show">shown</option><option value="hide">hidden</option>
      </select></label>
      <label className="check"><input type="checkbox" checked={look.layers} onChange={(e) => set({ layers: e.target.checked })} />Layers built apart (the plaster after the brick)</label>
      <label className="check" title="With a baseline saved: what is behind it and what is ahead of it, each in its colour"><input type="checkbox" checked={look.compare} onChange={(e) => set({ compare: e.target.checked })} data-testid="sim-compare" />Compared with the baseline</label>
      {look.compare && <>{colour('late', 'Late on the baseline')}{colour('ahead', 'Ahead of the baseline')}</>}
      <button type="button" className="link" onClick={() => set({ reset: true })}>Default colours</button>
    </div>
  );
}

/** The player at the Gantt's top: ▶, a step back and forward by a day or a week, the speed, the time line, the day and its tasks. */
export function SimulationBar() {
  const { on, day, playing, speed, unit, timeline, recording } = useSimulation();
  const [settings, setSettings] = useState(false);
  if (!on) return null;
  if (!timeline || day == null) return <div className="sim-bar" data-testid="sim-bar"><span className="muted">Reading the schedule…</span></div>;
  const under = tasksOn(timeline, day);
  const play = () => {
    if (playing) { useSimulation.setState({ playing: false }); return; }
    if (day >= timeline.finish) useSimulation.setState({ day: timeline.start });
    useSimulation.setState({ playing: true });
  };
  return (
    <div className="sim-bar" data-testid="sim-bar">
      <button type="button" className="icon" title={unit === 'day' ? 'A day back' : 'A week back'} disabled={recording} onClick={() => useSimulation.setState({ day: step(day, unit, -1, timeline), playing: false })} data-testid="sim-back"><SkipBack size={14} /></button>
      <button type="button" className={`icon${playing ? ' on' : ''}`} title={playing ? 'Pause' : 'Play'} disabled={recording} onClick={play} data-testid="sim-play">{playing ? <Pause size={14} /> : <Play size={14} />}</button>
      <button type="button" className="icon" title={unit === 'day' ? 'A day on' : 'A week on'} disabled={recording} onClick={() => useSimulation.setState({ day: step(day, unit, 1, timeline), playing: false })} data-testid="sim-forward"><SkipForward size={14} /></button>
      <select value={unit} title="What a step moves" onChange={(e) => useSimulation.setState({ unit: e.target.value as 'day' | 'week' })} data-testid="sim-unit">
        <option value="day">a day</option><option value="week">a week</option>
      </select>
      <select value={speed} title="How fast it plays" data-no-i18n onChange={(e) => useSimulation.setState({ speed: Number(e.target.value) })} data-testid="sim-speed">
        {SPEEDS.map((v) => <option key={v} value={v}>{`${v} d/s`}</option>)}
      </select>
      <input type="range" className="sim-scrub" min={timeline.start} max={timeline.finish} step={1} value={day} disabled={recording}
        onChange={(e) => useSimulation.setState({ day: Number(e.target.value), playing: false })} aria-label="Date" data-testid="sim-scrub" />
      <input type="date" className="sim-date" value={isoOf(day)} min={isoOf(timeline.start)} max={isoOf(timeline.finish)} disabled={recording}
        onChange={(e) => e.target.value && setDay(dayOf(e.target.value))} data-testid="sim-date" />
      <span className="sim-tasks muted small" title={under.map((t) => t.name).join(', ')}>
        {under.length > 0 && <b data-no-i18n>{`${under.length} `}</b>}
        <span>{under.length === 1 ? 'task under way' : under.length ? 'tasks under way' : 'no task under way'}</span>
        {under.length > 0 && <span data-no-i18n>{`: ${under.map((t) => t.name).join(', ')}`}</span>}
      </span>
      <Legend look={timeline.look} baseline={timeline.baseline} />
      <button type="button" className={`icon${settings ? ' on' : ''}`} title="Simulation settings: the colours, what is not started" onClick={() => setSettings(!settings)} data-testid="sim-settings-open"><Settings2 size={14} /></button>
      <button type="button" className="icon" title="A sheet of the model at several dates" onClick={() => useUi.setState({ dialog: 'planning.stages' })} data-testid="sim-stages"><LayoutPanelTop size={14} /><span className="small">Stages sheet…</span></button>
      <button type="button" className="icon" title="The simulation as a video file" onClick={() => useUi.setState({ dialog: 'planning.video' })} data-testid="sim-video"><Clapperboard size={14} /><span className="small">Export video…</span></button>
      <button type="button" className="icon" title="Close the simulation: the model as it is" onClick={() => void toggleSimulation(false)} data-testid="sim-close"><X size={14} /></button>
      {settings && <SimulationSettings look={timeline.look} onClose={() => setSettings(false)} />}
    </div>
  );
}

/** Over the model view while the simulation runs: the day, its counts and what the colours mean. */
export function SimulationCard() {
  const { on, day, timeline } = useSimulation();
  const view = useUi((s) => s.view);
  if (!on || !timeline || day == null || view === 'sheet' || view === 'schedule') return null;
  const tally = counts(timeline, day);
  return (
    <div className="sel-card sim-card" role="status" aria-label="4D simulation" data-testid="sim-card">
      <b data-no-i18n>{showDate(isoOf(day))}</b>
      <Legend look={timeline.look} tally={tally} baseline={timeline.baseline} />
    </div>
  );
}

/** A view's State at date in its properties: the day it draws (a plan, a section, a 3D view; printed so on its sheets). */
export function ViewDateSection({ node }: { node: NodeDto }) {
  const day = useSimulation((s) => (s.on ? s.day : null));
  const date = typeof node.props.stateDate === 'string' ? node.props.stateDate : '';
  const set = (d: string | null) => void execute('planning.viewDate', { id: node.id, date: d });
  return (
    <dl className="fields" data-testid="view-date">
      <dt title="The model as the work schedule has it on a day: what is not started left out, what is under way in its colour">State at date</dt>
      <dd className="row">
        <input type="date" value={date} onChange={(e) => set(e.target.value || null)} data-testid="view-date-input" />
        {day != null && <button type="button" className="link" title="The simulation's day" onClick={() => set(isoOf(day))} data-testid="view-date-cursor">the cursor's date</button>}
        {date && <button type="button" className="icon mini" title="The model as it is" onClick={() => set(null)}><X size={12} /></button>}
      </dd>
    </dl>
  );
}

/** The Gantt on sheets: a sheet a page, opened. */
export async function ganttOnSheet(): Promise<void> {
  const r = await execute('planning.ganttSheet', {}) as { ids: string[] } | null;
  if (r?.ids?.length) {
    useUi.setState({ view: 'sheet', sheetId: r.ids[0] });
    notify(r.ids.length === 1 ? 'The Gantt is on a new sheet.' : 'The Gantt is on new sheets, a sheet a page.');
  }
}
