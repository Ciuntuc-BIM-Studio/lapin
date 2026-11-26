/**
 * The location-based schedule (a time–location or flow-line chart; planning.lbs): a view like the Gantt, in a pane under the
 * model or a window of its own. Time across (days, weeks, months, with their grid), the model's locations up (its buildings,
 * storeys at their elevations, the zones of a storey by a field), folded level by level; a segment a task and a location,
 * coloured by its trade (the tasks' names without the locations'), the critical ones emphasised, today and the status date.
 * A click selects a segment, with its handles: the bottom one moves the task's start (it starts no earlier than that day),
 * the top one its finish (its duration); a second click selects the task across all its locations (its trade on every
 * floor), whose duration and constraint are changed at once. The selection is the Gantt's and the 3D's. Labels dragged
 * (Alt: turned) and notes pinned to a date and a location are kept in the view. Ctrl+wheel zooms the time, Shift+wheel
 * the locations, a double-click on the background fits it all again. Every change one undo step.
 */

import { useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { ChevronDown, ChevronRight, Crosshair, Maximize, MessageSquarePlus, Route, Sheet } from 'lucide-react';
import { create } from 'zustand';
import { select, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { useLang } from '../../i18n';
import { promptText } from '../../shell/prompts';
import { FieldPicker } from '../../shell/FieldPicker';
import { dateOf, dayOf, namesOf, showDate, ticks, today } from './gantt';
import { lbsOnSheet } from './schedule';
import {
  atOf, axisRows, clickPick, dayAt, dragArgs, fitLbs, geomOf, handleAt, handlesOf, hit, labelOf, movedLabel, noteAt, pickFromSelection, picked, rowAt, toggleFold, turnedLabel, wheelZoom, xOf, yOf,
  type Geom, type HandleKind, type LbsDto, type LbsLabel, type LbsLevel, type LbsPick, type LbsSegment, type Scale,
} from './lbs';

const AXIS_W = 190;
const HEAD_H = 40;

const useLbsData = create<{ data: LbsDto | null; version: number }>(() => ({ data: null, version: -2 }));
let asking: number | null = null;

/** The chart, asked again after every change to the model. */
function useLbs(): LbsDto | null {
  const version = useSession((s) => s.version);
  const data = useLbsData((s) => s.data);
  useEffect(() => {
    if (useLbsData.getState().version === version || asking === version) return;
    asking = version;
    void query<LbsDto>('planning.lbs').then((d) => {
      if (asking !== version) return;
      asking = null;
      if (d) useLbsData.setState({ data: d, version });
    });
  }, [version]);
  return data;
}

type Drag =
  | { kind: 'handle'; seg: LbsSegment; handle: HandleKind; x0: number; dx: number }
  | { kind: 'label'; seg: LbsSegment; turn: boolean; x0: number; y0: number; dx: number; dy: number }
  | { kind: 'pan'; x0: number; y0: number; left: number; top: number };

const setView = (view: string | null, args: Record<string, unknown>) => execute('planning.lbsView', { ...(view ? { id: view } : {}), ...args });

export function LbsView() {
  const data = useLbs();
  const selected = useUi((s) => s.selected);
  const lang = useLang((s) => s.lang);
  const names = useMemo(() => namesOf(lang), [lang]);
  const scroller = useRef<HTMLDivElement>(null);
  const [size, setSize] = useState({ w: 800, h: 300 });
  const [scroll, setScroll] = useState({ left: 0, top: 0 });
  const [scale, setScale] = useState<Scale | null>(null);
  const [pick, setPick] = useState<LbsPick | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [critical, setCritical] = useState(true);
  const [noting, setNoting] = useState(false);

  useEffect(() => {
    const el = scroller.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const viewW = Math.max(0, size.w - AXIS_W);
  const viewH = Math.max(0, size.h - HEAD_H);
  const fit = () => { if (data) { setScale(fitLbs(data, viewW, viewH)); requestAnimationFrame(() => scroller.current?.scrollTo({ left: 0, top: 0 })); } };
  // Fitted the first time it shows; the axis' height follows the model (a storey added, a level folded).
  useEffect(() => { if (data && viewW > 50 && !scale) fit(); }, [data, viewW]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { if (data && scale && scale.height !== Math.max(1, data.height)) setScale({ ...scale, height: Math.max(1, data.height) }); }, [data, scale]);

  // The selection made elsewhere (the Gantt, the 3D) picked here too.
  const segments = data?.segments ?? [];
  useEffect(() => { setPick((p) => pickFromSelection(p, selected, segments)); }, [selected, data]); // eslint-disable-line react-hooks/exhaustive-deps

  // Ctrl + wheel zooms the time, Shift + wheel the locations, about the pointer.
  useEffect(() => {
    const el = scroller.current;
    if (!el || !scale) return;
    const wheel = (e: WheelEvent) => {
      const r = el.getBoundingClientRect();
      const z = wheelZoom(scale, { deltaY: e.deltaY || e.deltaX, ctrl: e.ctrlKey || e.metaKey, shift: e.shiftKey }, Math.max(0, e.clientX - r.left - AXIS_W), Math.max(0, e.clientY - r.top - HEAD_H), { left: el.scrollLeft, top: el.scrollTop });
      if (!z) return;
      e.preventDefault();
      setScale(z.scale);
      requestAnimationFrame(() => el.scrollTo(z.scroll));
    };
    el.addEventListener('wheel', wheel, { passive: false });
    return () => el.removeEventListener('wheel', wheel);
  });

  const on = useMemo(() => picked(pick, segments), [pick, segments]);
  const labels = data?.settings.labels ?? {};

  if (!data || !scale) return <div className="gantt-pane lbs-pane" data-testid="lbs"><p className="muted gantt-empty">Reading the schedule…</p></div>;

  const s = scale;
  const chartW = Math.max(viewW, (dayOf(data.finish) - s.origin + 60) * s.ppd);
  const chartH = Math.max(viewH, s.height * s.ppm + 12);
  const at = (e: { clientX: number; clientY: number }) => {
    const r = scroller.current!.getBoundingClientRect();
    return { x: e.clientX - r.left - AXIS_W + scroll.left, y: e.clientY - r.top - HEAD_H + scroll.top };
  };
  const one = pick && !pick.whole ? segments.find((x) => x.id === pick.segment) ?? null : null;

  const choose = (p: LbsPick | null) => {
    setPick(p);
    if (!p) { select(null); return; }
    useUi.setState({ selected: p.tasks, selection: p.tasks[p.tasks.length - 1] ?? null });
  };

  const onDown = (e: ReactPointerEvent) => {
    if (e.button !== 0 && e.button !== 1) return;
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const p = at(e);
    if (e.button === 1) { setDrag({ kind: 'pan', x0: e.clientX, y0: e.clientY, left: scroll.left, top: scroll.top }); return; }
    if (noting) {
      setNoting(false);
      const loc = rowAt(data.locations, atOf(s, p.y));
      if (!loc) return;
      const text = promptText('Note');
      if (text?.trim()) void setView(data.view, { note: { date: dateOf(dayAt(s, p.x)), location: loc.id, text: text.trim() } });
      return;
    }
    // A handle of the segment selected: its start or its finish dragged.
    if (one) {
      const h = handleAt(geomOf(one, s), p.x, p.y);
      if (h) { setDrag({ kind: 'handle', seg: one, handle: h, x0: p.x, dx: 0 }); return; }
    }
    const seg = hit(segments, s, p.x, p.y);
    if (!seg) { choose(null); setDrag({ kind: 'pan', x0: e.clientX, y0: e.clientY, left: scroll.left, top: scroll.top }); return; }
    choose(clickPick(pick, seg, segments));
  };
  const onMove = (e: ReactPointerEvent) => {
    if (!drag) return;
    if (drag.kind === 'pan') { scroller.current?.scrollTo({ left: drag.left - (e.clientX - drag.x0), top: drag.top - (e.clientY - drag.y0) }); return; }
    const p = at(e);
    if (drag.kind === 'handle') setDrag({ ...drag, dx: p.x - drag.x0 });
    else setDrag({ ...drag, dx: p.x - drag.x0, dy: p.y - drag.y0 });
  };
  const onUp = () => {
    const d = drag;
    setDrag(null);
    if (!d || d.kind === 'pan') return;
    if (d.kind === 'handle') {
      const args = dragArgs(d.seg, d.handle, d.dx, s.ppd);
      if (args) void execute('planning.updateTask', args);
      return;
    }
    // A label clicked, not dragged: its segment picked as a click on it would.
    if (Math.abs(d.dx) < 2 && Math.abs(d.dy) < 2) { choose(clickPick(pick, d.seg, segments)); return; }
    void setView(data.view, { label: { id: d.seg.id, ...labelDragged(d) } });
  };
  const labelDragged = (d: Extract<Drag, { kind: 'label' }>): LbsLabel => {
    const g = geomOf(d.seg, s);
    const kept = labels[d.seg.id];
    return d.turn ? turnedLabel(kept, labelOf(g, s).angle, d.dx) : movedLabel(kept, g, s, d.dx, d.dy);
  };

  // ── What is in view ─────────────────────────────────────────────────────
  const day0 = dayAt(s, scroll.left) - 1;
  const day1 = dayAt(s, scroll.left + viewW) + 1;
  const scaleTicks = ticks(s.ppd, day0 - 40, day1 + 40, names);
  const now = today();
  const rows = data.locations.filter((l) => l.row >= 0);
  const axis = axisRows(data.locations);
  const ghost = drag?.kind === 'handle' ? (() => {
    const args = dragArgs(drag.seg, drag.handle, drag.dx, s.ppd);
    if (!args) return null;
    const moved = drag.handle === 'start'
      ? { ...drag.seg, start: args.start as string, finish: dateOf(dayOf(drag.seg.finish) + (dayOf(args.start as string) - dayOf(drag.seg.start))) }
      : { ...drag.seg, finish: args.finish as string };
    return { g: geomOf(moved, s), label: drag.handle === 'start' ? showDate(moved.start) : showDate(moved.finish) };
  })() : null;
  const level = data.level;

  return (
    <div className="gantt-pane lbs-pane" data-testid="lbs" tabIndex={0} onKeyDown={(e) => { if (e.key === 'Escape') { setDrag(null); setNoting(false); choose(null); } }}>
      <div className="gantt-top">
        <div className="view-toolbar gantt-toolbar">
          <label title="The locations the chart is drawn by: the model's buildings, its storeys, or the zones of each storey by a field of the elements">Locations{' '}
            <select value={data.settings.level} onChange={(e) => void setView(data.view, { level: e.target.value as LbsLevel })} data-testid="lbs-level">
              <option value="building">Buildings</option><option value="storey">Storeys</option><option value="zone">Zones</option>
            </select>
          </label>
          {(level === 'zone' || data.settings.level === 'zone') && (
            <span className="lbs-zone-field" title="The field (or formula) of the elements whose values are a storey's zones">
              <span className="small">Zones by</span>{' '}
              <FieldPicker value={data.zoneField ?? data.settings.zoneField ?? ''} onPick={(t) => void setView(data.view, { level: 'zone', zoneField: t })} placeholder="Pick a field…" testId="lbs-zone-field" />
            </span>
          )}
          <span className="group-rule" aria-hidden />
          <button type="button" className="icon" title="The whole chart in view (or double-click its background)" onClick={fit} data-testid="lbs-fit"><Maximize size={14} /></button>
          <button type="button" className="icon" title="Go to today" onClick={() => scroller.current?.scrollTo({ left: Math.max(0, xOf(s, now) - viewW / 3) })}><Crosshair size={14} /><span className="small">Today</span></button>
          <button type="button" className={`icon${critical ? ' on' : ''}`} title="The critical tasks emphasised" onClick={() => setCritical(!critical)} data-testid="lbs-critical"><Route size={14} /><span className="small">Critical path</span></button>
          <button type="button" className={`icon${noting ? ' on' : ''}`} title="A note pinned to a date and a location: click where it goes" onClick={() => setNoting(!noting)} data-testid="lbs-note"><MessageSquarePlus size={14} /><span className="small">Note</span></button>
          <button type="button" className="icon" title="The chart on a new sheet, drawn by the engine (it prints in the PDF)" onClick={() => void lbsOnSheet()} data-testid="lbs-sheet"><Sheet size={14} /><span className="small">On sheet</span></button>
          <span className="gantt-facts">
            <span>{`${data.segments.length} segments · ${rows.length} locations · finish ${showDate(data.finish)}`}</span>
            {data.unlocated.length > 0 && <span className="muted" title={data.unlocated.map((u) => u.name).join(', ')} data-testid="lbs-unlocated">{`${data.unlocated.length} tasks without a location`}</span>}
          </span>
        </div>
        {data.groups.length > 0 && (
          <div className="gantt-legend" data-testid="lbs-legend">
            {data.groups.map((g) => <span key={g.key} data-no-i18n><i style={{ background: g.colour }} />{g.key}</span>)}
          </div>
        )}
        {pick?.whole && <WholeTask tasks={pick.tasks} segments={segments} />}
      </div>
      <div className="gantt-scroll" ref={scroller} onScroll={(e) => { const el = e.currentTarget; setScroll({ left: el.scrollLeft, top: el.scrollTop }); }}>
        <div className="gantt-canvas" style={{ width: AXIS_W + chartW, height: HEAD_H + chartH }}>
          <div className="gantt-view" style={{ width: size.w, height: size.h }}>
            {/* The time scale */}
            <svg className="gantt-head-time" style={{ left: AXIS_W, width: viewW, height: HEAD_H }} aria-hidden data-testid="lbs-time">
              <g transform={`translate(${-scroll.left},0)`}>
                {scaleTicks.top.map((k) => <g key={`t${k.from}`}><line x1={xOf(s, k.from)} x2={xOf(s, k.from)} y1={0} y2={HEAD_H} className="gantt-tick major" /><text x={(xOf(s, k.to) - scroll.left > 90 ? Math.max(xOf(s, k.from), scroll.left) : xOf(s, k.from)) + 4} y={14} className="gantt-tick-label">{k.label}</text></g>)}
                {scaleTicks.bottom.map((k) => (k.to - k.from) * s.ppd >= 14 && <g key={`b${k.from}`}><line x1={xOf(s, k.from)} x2={xOf(s, k.from)} y1={20} y2={HEAD_H} className="gantt-tick" /><text x={xOf(s, k.from) + 3} y={34} className="gantt-tick-label minor">{k.label}</text></g>)}
                <line x1={xOf(s, now)} x2={xOf(s, now)} y1={20} y2={HEAD_H} className="gantt-today" />
              </g>
            </svg>
            {/* The locations: the tree folded level by level, the storeys' elevations */}
            <div className="lbs-axis" style={{ width: AXIS_W, top: HEAD_H, height: viewH }} data-testid="lbs-axis">
              {axis.map(({ loc, depth }) => {
                const y0 = yOf(s, loc.at + loc.extent) - scroll.top, y1 = yOf(s, loc.at) - scroll.top;
                if (y1 < -20 || y0 > viewH + 20) return null;
                const folds = (loc.children?.length ?? 0) > 0;
                return (
                  <div key={loc.id} className={`lbs-loc lv-${loc.level}${loc.row >= 0 ? ' row' : ' parent'}`} style={{ top: y0, height: Math.max(0, y1 - y0), left: depth * 18 }} data-location={loc.id} title={loc.name}>
                    {folds
                      ? <button type="button" className="gantt-fold" onClick={() => void setView(data.view, { collapsed: toggleFold(data.settings.collapsed, loc.id) })} title={loc.folded ? 'Show the locations under it' : 'Fold the locations under it into one'}>{loc.folded ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</button>
                      : <span className="gantt-fold-space" />}
                    <span className="lbs-loc-name" data-no-i18n>{loc.name}</span>
                    {loc.elevation != null && <span className="lbs-elev" data-no-i18n>{elevationText(loc.elevation)}</span>}
                  </div>
                );
              })}
            </div>
            {/* The chart */}
            <svg className={`gantt-chart lbs-chart${noting ? ' noting' : ''}`} style={{ left: AXIS_W, top: HEAD_H, width: viewW, height: viewH }}
              onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerCancel={() => setDrag(null)}
              onDoubleClick={(e) => { if (!hit(segments, s, at(e).x, at(e).y)) fit(); }} data-testid="lbs-chart">
              <g transform={`translate(${-scroll.left},${-scroll.top})`}>
                {scaleTicks.bottom.map((k) => (k.to - k.from) * s.ppd >= 14 && <line key={k.from} x1={xOf(s, k.from)} x2={xOf(s, k.from)} y1={scroll.top} y2={scroll.top + viewH} className="gantt-grid" />)}
                {rows.map((l) => <line key={l.id} x1={scroll.left} x2={scroll.left + viewW} y1={yOf(s, l.at)} y2={yOf(s, l.at)} className={`lbs-band lv-${l.level}`} />)}
                <line x1={scroll.left} x2={scroll.left + viewW} y1={yOf(s, s.height)} y2={yOf(s, s.height)} className="lbs-band" />
                <line x1={xOf(s, now)} x2={xOf(s, now)} y1={scroll.top} y2={scroll.top + viewH} className="gantt-today" />
                {data.dataDate && <line x1={xOf(s, dayOf(data.dataDate) + 1)} x2={xOf(s, dayOf(data.dataDate) + 1)} y1={scroll.top} y2={scroll.top + viewH} className="gantt-data-date" />}
                {segments.map((x) => <SegmentMark key={x.id} seg={x} g={geomOf(x, s)} on={on.has(x.id)} red={critical && x.critical} />)}
                {segments.map((x) => {
                  const g = geomOf(x, s);
                  const d = drag?.kind === 'label' && drag.seg.id === x.id ? drag : null;
                  const l = labelOf(g, s, d ? labelDragged(d) : labels[x.id]);
                  return (
                    <text key={`l${x.id}`} x={l.x} y={l.y} transform={`rotate(${-l.angle},${l.x},${l.y})`} className={`lbs-label${on.has(x.id) ? ' on' : ''}`} textAnchor="middle" data-no-i18n
                      onPointerDown={(e) => { if (e.button !== 0 || noting) return; e.stopPropagation(); (e.target as Element).setPointerCapture?.(e.pointerId); const p = at(e); setDrag({ kind: 'label', seg: x, turn: e.altKey, x0: p.x, y0: p.y, dx: 0, dy: 0 }); }}
                      onDoubleClick={(e) => { e.stopPropagation(); if (labels[x.id]) void setView(data.view, { label: { id: x.id } }); }}>
                      <title>{`${x.name} · ${showDate(x.start)} – ${showDate(x.finish)}`}</title>{x.group}
                    </text>
                  );
                })}
                {(data.settings.notes ?? []).map((n) => {
                  const p = noteAt(n, data.locations, s);
                  if (!p) return null;
                  return (
                    <text key={n.id} x={p.x} y={p.y} className="lbs-note" data-no-i18n
                      onPointerDown={(e) => e.stopPropagation()}
                      onDoubleClick={(e) => { e.stopPropagation(); const t = promptText('Note', n.text); if (t !== null) void setView(data.view, t.trim() ? { note: { id: n.id, text: t.trim() } } : { removeNote: n.id }); }}>
                      {n.text}
                    </text>
                  );
                })}
                {one && <Handles g={geomOf(one, s)} />}
                {ghost && <><GhostMark g={ghost.g} /><text x={ghost.g.x1 + 6} y={ghost.g.y1 - 4} className="gantt-drag-label" data-no-i18n>{ghost.label}</text></>}
              </g>
            </svg>
            {segments.length === 0 && (
              <div className="gantt-empty" style={{ top: HEAD_H + 24, left: AXIS_W + 12 }}>
                <p>No task works in a location yet: give the tasks elements (a rule, or the selection dragged onto them in the Gantt), or generate the schedule from the model.</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

const elevationText = (e: number) => (Math.abs(e) < 0.0005 ? '±0.00' : `${e > 0 ? '+' : '−'}${Math.abs(e).toFixed(2)}`);

function SegmentMark({ seg, g, on, red }: { seg: LbsSegment; g: Geom; on: boolean; red: boolean }) {
  const cls = `lbs-seg${on ? ' on' : ''}${red ? ' critical' : ''}${seg.status === 'done' ? ' done' : ''}`;
  if (g.kind === 'block') {
    return <rect x={g.x0} y={g.y1} width={Math.max(2, g.x1 - g.x0)} height={Math.max(2, g.y0 - g.y1)} className={`${cls} block`} style={{ fill: seg.colour, stroke: red || on ? undefined : seg.colour }} data-segment={seg.id} />;
  }
  if (g.kind === 'milestone') return <path d={`M${g.x0},${g.y0} L${g.x0},${g.y1} M${g.x0 - 5},${g.y1} l5,5 l5,-5`} className={`${cls} milestone`} style={{ stroke: seg.colour }} data-segment={seg.id} />;
  return <line x1={g.x0} y1={g.y0} x2={g.x1} y2={g.y1} className={`${cls} line`} style={{ stroke: red ? undefined : seg.colour }} data-segment={seg.id} />;
}

function GhostMark({ g }: { g: Geom }) {
  return g.kind === 'block'
    ? <rect x={g.x0} y={g.y1} width={Math.max(2, g.x1 - g.x0)} height={Math.max(2, g.y0 - g.y1)} className="gantt-ghost" />
    : <line x1={g.x0} y1={g.y0} x2={g.x1} y2={g.y1} className="gantt-ghost" />;
}

function Handles({ g }: { g: Geom }) {
  const h = handlesOf(g);
  return <>
    <circle cx={h.start[0]} cy={h.start[1]} r={5} className="gantt-handle" data-testid="lbs-handle-start"><title>Drag: the task starts no earlier than that day</title></circle>
    {g.kind !== 'milestone' && <circle cx={h.finish[0]} cy={h.finish[1]} r={5} className="gantt-handle" data-testid="lbs-handle-finish"><title>Drag: the task's duration</title></circle>}
  </>;
}

/** The task across all its locations (its trade on every floor): its duration and its constraint set at once, one undo step. */
function WholeTask({ tasks, segments }: { tasks: string[]; segments: LbsSegment[] }) {
  const mine = segments.filter((x) => tasks.includes(x.task));
  const durations = [...new Set(mine.map((x) => x.duration))];
  const group = mine[0]?.group ?? '';
  const set = (args: Record<string, unknown>) => void execute('planning.updateTasks', { ids: tasks, ...args });
  return (
    <div className="lbs-whole" data-testid="lbs-whole">
      <span><b data-no-i18n>{group}</b>{' · '}<span>{`${tasks.length} tasks`}</span></span>
      <label title="In working days, for every task of the trade">Duration{' '}
        <input key={durations.join(',')} type="number" min={0} step={1} defaultValue={durations.length === 1 ? durations[0] : ''} placeholder={durations.length > 1 ? 'mixed' : ''} data-testid="lbs-whole-duration"
          onBlur={(e) => { const n = Math.round(Number(e.target.value)); if (e.target.value !== '' && Number.isFinite(n) && n >= 0 && !(durations.length === 1 && durations[0] === n)) set({ duration: n }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /> d
      </label>
      <label title="As soon as possible, or each starting no earlier than a day">Constraint{' '}
        <select defaultValue="" onChange={(e) => { const c = e.target.value; if (c === 'asap') set({ constraint: null }); else if (c === 'snet') { const d = promptText('Start no earlier than (yyyy-mm-dd)'); if (d) set({ constraint: 'snet', constraintDate: d.trim() }); } e.target.value = ''; }} data-testid="lbs-whole-constraint">
          <option value="" disabled>…</option>
          <option value="asap">As soon as possible</option>
          <option value="snet">Start no earlier than…</option>
        </select>
      </label>
    </div>
  );
}
