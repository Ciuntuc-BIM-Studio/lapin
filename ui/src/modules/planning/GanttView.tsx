/**
 * The Gantt (docs/plan-4d5d.md, stage 1): a view like the others, in a pane (under the 3D, split in two rows) or a window
 * of its own. On the left the task table — WBS, name, duration, start, finish, predecessors, float — edited in place;
 * on the right the bars on the time scale: dragged to move a task (it then starts no earlier than that day), dragged by
 * their end to change its duration in working days, linked by dragging from the circle at one end onto another bar (the
 * ends touched give FS, SS, FF or SF), the links as arrows (a click changes the type and lag), the critical path in red,
 * the days not worked shaded, today's line. Only the rows and days in view are drawn, so thousands of tasks scroll
 * smoothly. The keyboard works as in a table: arrows, Insert, Delete, Tab and Shift+Tab to indent and outdent, F2 to rename.
 */

import { useEffect, useMemo, useRef, useState, type DragEvent as ReactDragEvent, type KeyboardEvent as ReactKeyboardEvent, type PointerEvent as ReactPointerEvent } from 'react';
import {
  CalendarDays, ChevronDown, ChevronRight, Crosshair, Diamond, IndentDecrease, IndentIncrease, Link2, Maximize, Plus, Route, Trash2, Unlink2, X,
} from 'lucide-react';
import { select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { useLang } from '../../i18n';
import {
  arrowPoints, barOf, dateOf, dayOf, dragDays, endAt, fitScale, linkLabel, linkType, MAX_PX, MIN_PX, movedFinish, movedStart, namesOf, parseDate,
  rowWindow, showDate, span, ticks, tierOf, today, visibleRows, workingDaysBetween, workingTest, ZOOMS, type Bar, type LinkDto, type LinkType, type TaskDto, type Zoom,
} from './gantt';
import { addTask, assignTo, deleteSelected, indent, linkSelected, outdent, selectUnlinked, showTaskElements, unlinkSelected, useSchedule } from './schedule';
import { DRAG_TYPE, dropOf, elementIds, ruleSummary, tasksHolding } from './links';

const ROW_H = 26;
const HEAD_H = 40;
const COLUMNS = [
  { key: 'row', label: '#', w: 30 },
  { key: 'wbs', label: 'WBS', w: 40 },
  { key: 'name', label: 'Task', w: 170 },
  { key: 'duration', label: 'Duration', w: 58 },
  { key: 'start', label: 'Start', w: 84 },
  { key: 'finish', label: 'Finish', w: 84 },
  { key: 'predecessors', label: 'Predecessors', w: 84 },
  { key: 'float', label: 'Float', w: 46 },
  { key: 'elements', label: 'Elements', w: 70 },
] as const;
type Column = (typeof COLUMNS)[number]['key'];
const TABLE_W = COLUMNS.reduce((s, c) => s + c.w, 0);

type Drag =
  | { kind: 'move' | 'finish'; id: string; x0: number; days: number }
  | { kind: 'link'; id: string; end: 'start' | 'finish'; x: number; y: number }
  | { kind: 'pan'; x0: number; y0: number; left: number; top: number };

/** A cell edited in place: Enter or leaving it commits, Escape gives up. */
function CellEditor({ value, onCommit, onDone, testId }: { value: string; onCommit: (text: string) => void; onDone: () => void; testId?: string }) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => { ref.current?.focus(); ref.current?.select(); }, []);
  const done = useRef(false);
  const commit = (text: string) => { if (done.current) return; done.current = true; if (text !== value) onCommit(text); onDone(); };
  return (
    <input ref={ref} className="gantt-input" defaultValue={value} data-testid={testId}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === 'Enter') commit((e.target as HTMLInputElement).value);
        if (e.key === 'Escape') { done.current = true; onDone(); }
      }}
      onBlur={(e) => commit(e.target.value)} />
  );
}

export function GanttView() {
  const data = useSchedule();
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const lang = useLang((s) => s.lang);
  const names = useMemo(() => namesOf(lang), [lang]);
  const scroller = useRef<HTMLDivElement>(null);
  const root = useRef<HTMLDivElement>(null);
  const [size, setSize] = useState({ w: 800, h: 300 });
  const [scroll, setScroll] = useState({ left: 0, top: 0 });
  const [ppd, setPpd] = useState<number>(ZOOMS.week);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [critical, setCritical] = useState(true);
  const [editing, setEditing] = useState<{ id: string; col: Column } | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [hover, setHover] = useState<string | null>(null);
  const [linkEdit, setLinkEdit] = useState<{ link: LinkDto; x: number; y: number } | null>(null);
  // The row the selection is being dragged over (stage 2: dropped, its elements go into that task).
  const [dropRow, setDropRow] = useState<string | null>(null);
  const fitted = useRef(false);

  useEffect(() => {
    const el = scroller.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const rows = useMemo(() => visibleRows(data?.tasks ?? [], collapsed), [data, collapsed]);
  const rowOf = useMemo(() => new Map(rows.map((t, i) => [t.id, i])), [rows]);
  const range = useMemo(() => (data ? span(data) : { from: today() - 7, to: today() + 90 }), [data]);
  const works = useMemo(() => (data ? workingTest(data.calendar, data.nonWorking) : () => true), [data]);
  const chartW = Math.max(0, (range.to - range.from) * ppd);
  const viewW = Math.max(0, size.w - TABLE_W);
  const viewH = Math.max(0, size.h - HEAD_H);
  const totalH = rows.length * ROW_H;

  // The first time a schedule shows, it is fitted to the pane.
  useEffect(() => {
    if (fitted.current || !data || data.tasks.length === 0 || viewW < 50) return;
    fitted.current = true;
    const k = Math.min(ZOOMS.day, fitScale(data, viewW));
    setPpd(k);
    requestAnimationFrame(() => scroller.current?.scrollTo({ left: Math.max(0, (dayOf(data.start) - range.from - 3) * k) }));
  }, [data, viewW, range.from]);

  const xOf = (day: number) => (day - range.from) * ppd;
  const barFor = (t: TaskDto, row: number): Bar => barOf(t, row, range.from, ppd, ROW_H);

  const zoomTo = (k: number, anchorX = viewW / 2) => {
    const next = Math.min(MAX_PX, Math.max(MIN_PX, k));
    const day = (scroll.left + anchorX) / ppd;
    setPpd(next);
    requestAnimationFrame(() => scroller.current?.scrollTo({ left: Math.max(0, day * next - anchorX) }));
  };
  const fit = () => { if (data) { const k = fitScale(data, viewW); setPpd(k); requestAnimationFrame(() => scroller.current?.scrollTo({ left: Math.max(0, (dayOf(data.start) - range.from - 3) * k) })); } };
  const goToday = () => scroller.current?.scrollTo({ left: Math.max(0, xOf(today()) - viewW / 3) });

  const sel = useMemo(() => new Set(selected), [selected]);
  // The tasks holding the elements selected in the views: their rows marked.
  const holding = useMemo(() => tasksHolding(data?.tasks ?? [], elementIds(selected, nodes)), [data, selected, nodes]);
  const nameOf = (id: string) => nodes.get(id)?.name;
  const classLabel = (cls: string) => cls.replace(/^Ifc/, '');
  const first = rows.findIndex((t) => sel.has(t.id));

  // ── Pointer on the chart ────────────────────────────────────────────────
  const at = (e: { clientX: number; clientY: number }) => {
    const r = scroller.current!.getBoundingClientRect();
    return { x: e.clientX - r.left - TABLE_W + scroll.left, y: e.clientY - r.top - HEAD_H + scroll.top };
  };
  const rowAt = (y: number) => (y < 0 ? -1 : Math.floor(y / ROW_H));

  const onDown = (e: ReactPointerEvent) => {
    root.current?.focus();
    setLinkEdit(null);
    const p = at(e);
    const i = rowAt(p.y);
    const t = rows[i];
    (e.target as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 1 || !t) { setDrag({ kind: 'pan', x0: e.clientX, y0: e.clientY, left: scroll.left, top: scroll.top }); if (!t && e.button === 0) select(null); return; }
    const b = barFor(t, i);
    const cy = b.y + b.h / 2;
    const near = (x: number) => Math.hypot(p.x - x, p.y - cy) <= 7;
    if (near(b.x - 7)) { setDrag({ kind: 'link', id: t.id, end: 'start', x: p.x, y: p.y }); return; }
    if (near(b.x + b.w + 7)) { setDrag({ kind: 'link', id: t.id, end: 'finish', x: p.x, y: p.y }); return; }
    const onBar = t.milestone ? Math.abs(p.x - b.x) <= b.h / 2 + 2 : p.x >= b.x - 2 && p.x <= b.x + b.w + 2;
    if (!onBar) { select(t.id, e.shiftKey || e.ctrlKey || e.metaKey); setDrag({ kind: 'pan', x0: e.clientX, y0: e.clientY, left: scroll.left, top: scroll.top }); return; }
    select(t.id, e.shiftKey || e.ctrlKey || e.metaKey);
    if (t.summary) return;
    const finish = !t.milestone && p.x >= b.x + b.w - 6;
    setDrag({ kind: finish ? 'finish' : 'move', id: t.id, x0: p.x, days: 0 });
  };

  const onMove = (e: ReactPointerEvent) => {
    const p = at(e);
    if (!drag) {
      const t = rows[rowAt(p.y)];
      setHover(t && !t.summary ? t.id : null);
      return;
    }
    if (drag.kind === 'pan') { scroller.current?.scrollTo({ left: drag.left - (e.clientX - drag.x0), top: drag.top - (e.clientY - drag.y0) }); return; }
    if (drag.kind === 'link') { setDrag({ ...drag, x: p.x, y: p.y }); return; }
    const days = dragDays(p.x - drag.x0, ppd);
    if (days !== drag.days) setDrag({ ...drag, days });
  };

  const onUp = (e: ReactPointerEvent) => {
    const d = drag;
    setDrag(null);
    if (!d || d.kind === 'pan') return;
    const t = data?.tasks.find((x) => x.id === d.id);
    if (!t) return;
    if (d.kind === 'link') {
      const p = at(e);
      const i = rowAt(p.y);
      const target = rows[i];
      if (!target || target.id === t.id) return;
      const type = linkType(d.end, endAt(barFor(target, i), p.x));
      void execute('planning.link', { from: t.id, to: target.id, type });
      return;
    }
    if (d.days === 0) return;
    if (d.kind === 'move') void execute('planning.updateTask', { id: t.id, start: movedStart(t, d.days) });
    else void execute('planning.updateTask', { id: t.id, finish: movedFinish(t, d.days) });
  };

  // ── Keyboard ────────────────────────────────────────────────────────────
  const onKey = (e: ReactKeyboardEvent) => {
    if ((e.target as HTMLElement).closest('input, select, textarea')) return;
    const handled = () => { e.preventDefault(); e.stopPropagation(); };
    const current = first >= 0 ? rows[first] : null;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      const i = Math.max(0, Math.min(rows.length - 1, (first < 0 ? -1 : first) + (e.key === 'ArrowDown' ? 1 : -1)));
      if (rows[i]) { select(rows[i].id); reveal(i); }
      handled();
    } else if (e.key === 'Insert' || (e.key === 'Enter' && e.ctrlKey)) { void addTask(e.shiftKey); handled(); }
    else if (e.key === 'Delete') { deleteSelected(); handled(); }
    else if (e.key === 'Tab' || (e.altKey && (e.key === 'ArrowRight' || e.key === 'ArrowLeft'))) {
      if (e.key === 'Tab' ? e.shiftKey : e.key === 'ArrowLeft') outdent(); else indent();
      handled();
    } else if ((e.key === 'F2' || e.key === 'Enter') && current) { setEditing({ id: current.id, col: 'name' }); handled(); }
    else if (e.key === 'Escape' && (drag || linkEdit)) { setDrag(null); setLinkEdit(null); handled(); }
  };

  const reveal = (i: number) => {
    const el = scroller.current;
    if (!el) return;
    const y = i * ROW_H;
    if (y < el.scrollTop) el.scrollTop = y;
    else if (y + ROW_H > el.scrollTop + viewH) el.scrollTop = y + ROW_H - viewH;
  };

  // Ctrl + wheel zooms about the cursor (the page itself must not zoom).
  useEffect(() => {
    const el = scroller.current;
    if (!el) return;
    const wheel = (e: WheelEvent) => {
      if (!e.ctrlKey && !e.metaKey) return;
      e.preventDefault();
      const r = el.getBoundingClientRect();
      zoomTo(ppd * Math.exp(-e.deltaY * 0.002), Math.max(0, e.clientX - r.left - TABLE_W));
    };
    el.addEventListener('wheel', wheel, { passive: false });
    return () => el.removeEventListener('wheel', wheel);
  });

  const commit = (t: TaskDto, col: Column, text: string) => {
    const v = text.trim();
    switch (col) {
      case 'name': if (v) void execute('planning.updateTask', { id: t.id, name: v }); break;
      case 'duration': { const n = Number(v.replace(',', '.').replace(/\s*(d|z|zile|zi|days?)$/i, '')); if (Number.isFinite(n) && n >= 0) void execute('planning.updateTask', { id: t.id, duration: Math.round(n) }); break; }
      case 'start': { const d = parseDate(v); if (d) void execute('planning.updateTask', { id: t.id, start: d }); break; }
      case 'finish': { const d = parseDate(v); if (d) void execute('planning.updateTask', { id: t.id, finish: d }); break; }
      case 'predecessors': void execute('planning.setPredecessors', { id: t.id, text: v }); break;
      default: break;
    }
  };

  /** The selection dropped on a task's row: its elements put in the task, or (Shift) taken out of it. */
  const drop = (e: ReactDragEvent, t: TaskDto | undefined) => {
    setDropRow(null);
    const d = dropOf(e.dataTransfer.getData(DRAG_TYPE), e.shiftKey);
    if (!d || !t) return;
    e.preventDefault();
    select(t.id);
    void assignTo(t.id, d.ids, d.remove);
  };

  const toggle = (id: string) => setCollapsed((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  // ── What is in view ─────────────────────────────────────────────────────
  const [r0, r1] = rowWindow(scroll.top, viewH, ROW_H, rows.length);
  const day0 = Math.floor(scroll.left / ppd) + range.from - 1;
  const day1 = Math.ceil((scroll.left + viewW) / ppd) + range.from + 1;
  const scale = useMemo(() => ticks(ppd, day0 - 40, day1 + 40, names), [ppd, day0, day1, names]);
  const tier = tierOf(ppd);
  const now = today();

  const cell = (t: TaskDto, col: Column): string => {
    switch (col) {
      case 'row': return String(t.row);
      case 'wbs': return t.wbs;
      case 'name': return t.name;
      case 'duration': return `${t.duration} d`;
      case 'start': return showDate(t.start);
      case 'finish': return showDate(t.finish);
      case 'predecessors': return t.predecessors;
      case 'elements': return t.count ? String(t.count) : '';
      default: return t.summary ? '' : `${t.totalFloat} d`;
    }
  };
  const editable = (t: TaskDto, col: Column) => col === 'name' || (!t.summary && (col === 'duration' || col === 'start' || col === 'finish' || col === 'predecessors'));

  // The bar being dragged as it would land, and what it says.
  const ghost = (() => {
    if (!drag || (drag.kind !== 'move' && drag.kind !== 'finish')) return null;
    const t = rows.find((x) => x.id === drag.id);
    const i = t ? rowOf.get(t.id) : undefined;
    if (!t || i === undefined) return null;
    const moved = drag.kind === 'move'
      ? { ...t, start: movedStart(t, drag.days), finish: dateOf(dayOf(t.finish) + drag.days) }
      : { ...t, finish: movedFinish(t, drag.days) };
    const b = barFor(moved, i);
    const label = drag.kind === 'move'
      ? `${showDate(moved.start)}`
      : `${workingDaysBetween((d) => works(d, !!t.wet), dayOf(moved.start), dayOf(moved.finish))} d · ${showDate(moved.finish)}`;
    return { b, label };
  })();

  const linkLine = drag?.kind === 'link' ? (() => {
    const i = rowOf.get(drag.id);
    const t = rows.find((x) => x.id === drag.id);
    if (i === undefined || !t) return null;
    const b = barFor(t, i);
    return { x1: drag.end === 'start' ? b.x - 7 : b.x + b.w + 7, y1: b.y + b.h / 2, x2: drag.x, y2: drag.y };
  })() : null;

  if (!data) return <div className="gantt-pane" data-testid="gantt"><p className="muted gantt-empty">Reading the schedule…</p></div>;

  const shading: number[] = [];
  if (tier === 'day' || tier === 'week') for (let d = Math.max(day0, range.from); d <= Math.min(day1, range.to); d++) if (!works(d)) shading.push(d);

  return (
    <div className="gantt-pane" data-testid="gantt" ref={root} tabIndex={0} onKeyDown={onKey}>
      <div className="view-toolbar gantt-toolbar">
        <button type="button" className="icon" title="A new task before the one selected (Insert)" onClick={() => void addTask(false)} data-testid="gantt-add"><Plus size={14} /><span className="small">Task</span></button>
        <button type="button" className="icon" title="A milestone before the task selected (Shift+Insert)" onClick={() => void addTask(true)}><Diamond size={14} /><span className="small">Milestone</span></button>
        <button type="button" className="icon" title="Indent: under the task above (Tab)" onClick={indent}><IndentIncrease size={14} /></button>
        <button type="button" className="icon" title="Outdent: up a level (Shift+Tab)" onClick={outdent}><IndentDecrease size={14} /></button>
        <button type="button" className="icon" title="Link the tasks selected one after the other (finish to start)" onClick={linkSelected}><Link2 size={14} /></button>
        <button type="button" className="icon" title="Take off the links between the tasks selected" onClick={unlinkSelected}><Unlink2 size={14} /></button>
        <button type="button" className="icon" title="Delete the tasks selected with what is under them (Delete)" onClick={deleteSelected}><Trash2 size={14} /></button>
        <span className="group-rule" aria-hidden />
        <span className="chips gantt-zoom" role="radiogroup" aria-label="Zoom">
          {(Object.keys(ZOOMS) as Zoom[]).map((z) => (
            <button type="button" key={z} className={`chip${tier === z ? ' on' : ''}`} onClick={() => zoomTo(ZOOMS[z])} data-testid={`gantt-zoom-${z}`}>{z === 'day' ? 'Days' : z === 'week' ? 'Weeks' : z === 'month' ? 'Months' : 'Quarters'}</button>
          ))}
        </span>
        <button type="button" className="icon" title="The whole schedule in view" onClick={fit}><Maximize size={14} /></button>
        <button type="button" className="icon" title="Go to today" onClick={goToday}><Crosshair size={14} /><span className="small">Today</span></button>
        <button type="button" className={`icon${critical ? ' on' : ''}`} title="The critical path in red: the tasks with no float" onClick={() => setCritical(!critical)} data-testid="gantt-critical"><Route size={14} /><span className="small">Critical path</span></button>
        <button type="button" className="icon" title="Working days and hours, holidays, the winter break" onClick={() => useUi.setState({ dialog: 'planning.calendar' })}><CalendarDays size={14} /><span className="small">Calendar…</span></button>
        <span className="gantt-facts" data-testid="gantt-facts">
          <label title="The day the schedule starts">Start <input type="date" value={data.start} onChange={(e) => e.target.value && void execute('planning.updateSchedule', { start: e.target.value })} /></label>
          <span>{`${data.tasks.length} tasks · ${data.workingDays} working days · finish ${showDate(data.finish)}`}</span>
          {data.problem && <span className="error-text">{data.problem}</span>}
          {data.tasks.some((t) => t.count > 0) && (data.elements.none > 0 || data.elements.partial > 0
            ? <button type="button" className="link cde-warn" onClick={() => void selectUnlinked()} title="Select them, to drag them onto a task" data-testid="gantt-unlinked">{`${data.elements.none + data.elements.partial} of ${data.elements.total} elements without a task`}</button>
            : <span className="muted" data-testid="gantt-unlinked">{`all ${data.elements.total} elements have a task`}</span>)}
        </span>
      </div>
      <div className="gantt-scroll" ref={scroller} onScroll={(e) => { const el = e.currentTarget; setScroll({ left: el.scrollLeft, top: el.scrollTop }); setLinkEdit(null); }}>
        <div className="gantt-canvas" style={{ width: TABLE_W + chartW, height: HEAD_H + totalH }}>
          <div className="gantt-view" style={{ width: size.w, height: size.h }}>
            {/* The time scale */}
            <svg className="gantt-head-time" style={{ left: TABLE_W, width: viewW, height: HEAD_H }} aria-hidden>
              <g transform={`translate(${-scroll.left},0)`}>
                {scale.top.map((k) => <g key={`t${k.from}`}><line x1={xOf(k.from)} x2={xOf(k.from)} y1={0} y2={HEAD_H} className="gantt-tick major" /><text x={Math.max(xOf(k.from), scroll.left) + 4} y={14} className="gantt-tick-label">{k.label}</text></g>)}
                {scale.bottom.map((k) => (k.to - k.from) * ppd >= 14 && <g key={`b${k.from}`}><line x1={xOf(k.from)} x2={xOf(k.from)} y1={20} y2={HEAD_H} className="gantt-tick" /><text x={xOf(k.from) + 3} y={34} className="gantt-tick-label minor">{k.label}</text></g>)}
                <line x1={xOf(now)} x2={xOf(now)} y1={20} y2={HEAD_H} className="gantt-today" />
              </g>
            </svg>
            {/* The chart */}
            <svg className="gantt-chart" style={{ left: TABLE_W, top: HEAD_H, width: viewW, height: viewH }}
              onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerLeave={() => !drag && setHover(null)}
              onDoubleClick={() => { const t = first >= 0 ? rows[first] : null; if (t) showTaskElements(t); }}
              onDragOver={(e) => { if (!e.dataTransfer.types.includes(DRAG_TYPE)) return; e.preventDefault(); e.dataTransfer.dropEffect = e.shiftKey ? 'move' : 'copy'; setDropRow(rows[rowAt(at(e).y)]?.id ?? null); }}
              onDragLeave={() => setDropRow(null)}
              onDrop={(e) => { const t = rows[rowAt(at(e).y)]; drop(e, t); }} data-testid="gantt-chart">
              <defs>
                <marker id="gantt-arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,0 L8,4 L0,8 z" className="gantt-arrowhead" /></marker>
                <marker id="gantt-arrow-critical" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,0 L8,4 L0,8 z" className="gantt-arrowhead critical" /></marker>
              </defs>
              <g transform={`translate(${-scroll.left},${-scroll.top})`}>
                {shading.map((d) => <rect key={d} x={xOf(d)} y={scroll.top} width={ppd} height={viewH} className="gantt-off" />)}
                {scale.bottom.map((k) => (k.to - k.from) * ppd >= 14 && <line key={k.from} x1={xOf(k.from)} x2={xOf(k.from)} y1={scroll.top} y2={scroll.top + viewH} className="gantt-grid" />)}
                {rows.slice(r0, r1 + 1).map((t, k) => (sel.has(t.id) || holding.has(t.id) || dropRow === t.id) && <rect key={t.id} x={scroll.left} y={(r0 + k) * ROW_H} width={viewW} height={ROW_H} className={dropRow === t.id ? 'gantt-row-drop' : sel.has(t.id) ? 'gantt-row-on' : 'gantt-row-holds'} />)}
                <line x1={xOf(now)} x2={xOf(now)} y1={scroll.top} y2={scroll.top + viewH} className="gantt-today" />
                {data.dataDate && <line x1={xOf(dayOf(data.dataDate))} x2={xOf(dayOf(data.dataDate))} y1={scroll.top} y2={scroll.top + viewH} className="gantt-data-date" />}
                {data.links.map((l) => {
                  const i = rowOf.get(l.from), j = rowOf.get(l.to);
                  if (i === undefined || j === undefined || (Math.max(i, j) < r0) || (Math.min(i, j) > r1)) return null;
                  const pts = arrowPoints(barFor(rows[i], i), barFor(rows[j], j), l.type, ROW_H);
                  const red = critical && l.critical;
                  const d = pts.map((p) => p.join(',')).join(' ');
                  return (
                    <g key={l.id} className="gantt-link" onPointerDown={(e) => { e.stopPropagation(); const r = scroller.current!.getBoundingClientRect(); setLinkEdit({ link: l, x: e.clientX - r.left, y: e.clientY - r.top }); }}>
                      <polyline points={d} className="gantt-link-hit" />
                      <polyline points={d} className={`gantt-link-line${red ? ' critical' : ''}`} markerEnd={`url(#gantt-arrow${red ? '-critical' : ''})`} />
                    </g>
                  );
                })}
                {rows.slice(r0, r1 + 1).map((t, k) => {
                  const i = r0 + k;
                  const b = barFor(t, i);
                  if (b.x + b.w < scroll.left - 200 || b.x > scroll.left + viewW + 50) return null;
                  const red = critical && t.critical;
                  const cy = b.y + b.h / 2;
                  return (
                    <g key={t.id} className={`gantt-task${sel.has(t.id) ? ' on' : ''}`} data-task={t.id}>
                      {t.summary
                        ? <path d={`M${b.x},${b.y + 2} h${b.w} v${b.h - 2} l-5,-4 h${-(b.w - 10)} l-5,4 z`} className={`gantt-summary${red ? ' critical' : ''}`} />
                        : t.milestone
                          ? <path d={`M${b.x},${cy - 7} l7,7 l-7,7 l-7,-7 z`} className={`gantt-milestone${red ? ' critical' : ''}`} />
                          : <rect x={b.x} y={b.y} width={Math.max(2, b.w)} height={b.h} rx={2} className={`gantt-bar${red ? ' critical' : ''}${t.wet ? ' wet' : ''}`} />}
                      <text x={b.x + Math.max(b.w, 7) + 12} y={cy + 4} className="gantt-bar-label">{t.name}</text>
                      {!t.summary && (hover === t.id || drag?.kind === 'link') && <>
                        <circle cx={b.x - 7} cy={cy} r={4} className="gantt-handle" />
                        <circle cx={b.x + b.w + 7} cy={cy} r={4} className="gantt-handle" />
                      </>}
                    </g>
                  );
                })}
                {ghost && <><rect x={ghost.b.x} y={ghost.b.y} width={Math.max(2, ghost.b.w)} height={ghost.b.h} rx={2} className="gantt-ghost" /><text x={ghost.b.x + ghost.b.w + 6} y={ghost.b.y - 3} className="gantt-drag-label">{ghost.label}</text></>}
                {linkLine && <line {...linkLine} className="gantt-link-drag" />}
              </g>
            </svg>
            {/* The table's header and rows, over the chart's left */}
            <div className="gantt-head-table" style={{ width: TABLE_W, height: HEAD_H }}>
              {COLUMNS.map((c) => <span key={c.key} className={`gantt-th c-${c.key}`} style={{ width: c.w }} data-i18n-ctx="schedule">{c.label}</span>)}
            </div>
            <div className="gantt-table" style={{ width: TABLE_W, top: HEAD_H, height: viewH }}>
              {rows.slice(r0, r1 + 1).map((t, k) => {
                const i = r0 + k;
                return (
                  <div key={t.id} className={`gantt-row${sel.has(t.id) ? ' on' : ''}${holding.has(t.id) ? ' holds' : ''}${dropRow === t.id ? ' drop' : ''}${t.summary ? ' summary' : ''}${critical && t.critical ? ' critical' : ''}`}
                    style={{ top: i * ROW_H - scroll.top, height: ROW_H }} data-row={t.row} data-task={t.id}
                    onDragOver={(e) => { if (!e.dataTransfer.types.includes(DRAG_TYPE)) return; e.preventDefault(); e.dataTransfer.dropEffect = e.shiftKey ? 'move' : 'copy'; setDropRow(t.id); }}
                    onDragLeave={() => setDropRow((r) => (r === t.id ? null : r))}
                    onDrop={(e) => drop(e, t)}
                    onPointerDown={(e) => { if (!(e.target as HTMLElement).closest('input')) { root.current?.focus(); select(t.id, e.shiftKey || e.ctrlKey || e.metaKey); } }}>
                    {COLUMNS.map((c) => (
                      <span key={c.key} className={`gantt-td c-${c.key}`} style={{ width: c.w, ...(c.key === 'name' ? { paddingLeft: 4 + t.level * 14 } : {}) }}
                        title={c.key === 'elements' && t.count ? ruleSummary(t, nameOf, classLabel) || undefined : undefined}
                        onDoubleClick={() => editable(t, c.key) && setEditing({ id: t.id, col: c.key })}>
                        {c.key === 'name' && (t.summary
                          ? <button type="button" className="gantt-fold" onClick={(e) => { e.stopPropagation(); toggle(t.id); }} title={collapsed.has(t.id) ? 'Show the tasks under it' : 'Hide the tasks under it'}>{collapsed.has(t.id) ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</button>
                          : <span className="gantt-fold-space">{t.milestone ? <Diamond size={9} /> : null}</span>)}
                        {editing?.id === t.id && editing.col === c.key
                          ? <CellEditor value={c.key === 'duration' ? String(t.duration) : cell(t, c.key)} onCommit={(text) => commit(t, c.key, text)} onDone={() => { setEditing(null); root.current?.focus(); }} testId={`gantt-edit-${c.key}`} />
                          : <span className="gantt-text" data-no-i18n={c.key === 'name' || c.key === 'predecessors' ? true : undefined}>{cell(t, c.key)}</span>}
                      </span>
                    ))}
                  </div>
                );
              })}
            </div>
            {rows.length === 0 && (
              <div className="gantt-empty" style={{ top: HEAD_H + 24 }}>
                <p>No tasks yet. Add the first with Task or the Insert key, or import a schedule from MS Project (Planning → Import MS Project…).</p>
                <button type="button" className="btn primary" onClick={() => void addTask(false, null)} data-testid="gantt-first-task"><Plus size={14} />First task</button>
              </div>
            )}
            {linkEdit && <LinkEditor link={linkEdit.link} x={linkEdit.x} y={linkEdit.y} onClose={() => setLinkEdit(null)} names={(id) => data.tasks.find((t) => t.id === id)?.name ?? ''} />}
          </div>
        </div>
      </div>
    </div>
  );
}

/** A link clicked on the chart: its type and lag changed, or it taken off. */
function LinkEditor({ link, x, y, onClose, names }: { link: LinkDto; x: number; y: number; onClose: () => void; names: (id: string) => string }) {
  const [type, setType] = useState<LinkType>(link.type);
  const [lag, setLag] = useState(String(link.lag));
  const apply = () => { const n = Math.round(Number(lag.replace(',', '.'))); void execute('planning.updateLink', { id: link.id, type, lag: Number.isFinite(n) ? n : 0 }).then(onClose); };
  return (
    <div className="gantt-link-editor" style={{ left: Math.max(4, x - 120), top: y + 10 }} onPointerDown={(e) => e.stopPropagation()} onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') apply(); if (e.key === 'Escape') onClose(); }} data-testid="gantt-link-editor">
      <div className="gantt-link-title"><span data-no-i18n>{names(link.from)} → {names(link.to)}</span><button type="button" className="icon mini" title="Close" onClick={onClose}><X size={12} /></button></div>
      <label>Type <select value={type} onChange={(e) => setType(e.target.value as LinkType)}>
        <option value="FS">Finish to start (FS)</option><option value="SS">Start to start (SS)</option><option value="FF">Finish to finish (FF)</option><option value="SF">Start to finish (SF)</option>
      </select></label>
      <label title="Working days after the predecessor's end; negative: an overlap">Lag <input type="number" step={1} value={lag} onChange={(e) => setLag(e.target.value)} /> d</label>
      <div className="gantt-link-buttons">
        <button type="button" className="btn primary" onClick={apply}>Apply</button>
        <button type="button" className="btn danger" onClick={() => void execute('planning.unlink', { link: link.id }).then(onClose)}><Trash2 size={13} />Remove</button>
      </div>
      <span className="muted small" data-no-i18n>{linkLabel({ type, lag: Math.round(Number(lag) || 0) })}</span>
    </div>
  );
}
