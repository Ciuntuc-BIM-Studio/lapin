/**
 * A schedule: its cards or table as they print (the engine lays them out),
 * and under them the table to type in — a row or column of its own, a
 * parameter's value (typed into the type), or a model value overridden on
 * purpose; its charts beside it (decision 251). Scale, order, rows and CSV
 * here; a sheet places it.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, ChartColumn, FileDown, Maximize, Plus, Settings2 } from 'lucide-react';
import { ScheduleSettings, type Settings } from './ScheduleSettings';
import { ScheduleChartsPanel, type ChartDto } from './ScheduleChartsPanel';
import { setTypeParam } from './scheduleParams';
import { notify } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { execute, query, useSession } from '../engine/session';
import { saveFile, safeFileName } from '../engine/files';
import { DrawingLayer } from './DrawingLayer';
import { TakeoffControls } from './TakeoffControls';
import { placeOnSheet, sheetsFor } from '../modules/cost/schedules';
import type { PenDto } from './drawing';
import { PX_PER_MM, middleDouble, popHistory, pushHistory, useZoomHost, wheelZoom, type ZoomHost } from './zoom';

interface GridDto {
  columns: { key: string; label: string; custom: boolean }[];
  rows: { key: string; manual: boolean; cells: string[]; typed: string[] }[];
}

interface Layout {
  w: number; h: number; pens: PenDto[];
  rows: { id: string; label: string; fromModel: boolean; param?: string | null }[];
  /** type: the type an entry lists (a mirror image's entry, D-M, is its type's: report #83). */
  entries: { key: string; type?: string; mark: string; name: string; total: number; manual?: boolean; rows: Record<string, string> }[];
  problems: { twice: number; none: number; marks: string[] };
  overridden?: Record<string, string[]>;
  /** A module's table, made editable. */
  grid?: GridDto;
  unused?: boolean; unusedCount?: number;
  charts?: ChartDto[]; chartColumns?: string[];
}

/** Commits a cell's text on Enter or when it is left, only when it changed. */
function CellInput({ value, onCommit, title, testId }: { value: string; onCommit: (text: string) => void; title?: string; testId?: string }) {
  return (
    <input key={value} defaultValue={value} title={title} data-testid={testId}
      onKeyDown={(ev) => { if (ev.key === 'Enter') (ev.target as HTMLInputElement).blur(); }}
      onBlur={(ev) => { if (ev.target.value !== value) onCommit(ev.target.value); }} />
  );
}

export function ScheduleView() {
  const id = usePaneField('scheduleId');
  const active = usePaneActive();
  const nodes = useSession((s) => s.nodes);
  const node = id ? nodes.get(id) : undefined;
  const [d, setD] = useState<(Layout & Settings) | null>(null);
  const [side, setSide] = useState<'settings' | 'charts' | null>(null);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [z, setZ] = useState<{ k: number; ox: number; oy: number } | null>(null);
  const drag = useRef<{ x: number; y: number; start: { k: number; ox: number; oy: number } } | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    const t = setTimeout(() => { void query<Layout & Settings>('schedule.layout', { id }).then((r) => { if (live && r) setD(r); }); }, 100);
    return () => { live = false; clearTimeout(t); };
  }, [id, nodes]);
  useEffect(() => { setZ(null); setD(null); }, [id]);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  const fit = useCallback(() => {
    if (!d || !size.w) return;
    const k = Math.min(size.w / (d.w + 20), size.h / (d.h + 20), 6);
    setZ({ k, ox: 10 * k, oy: 10 * k });
  }, [d, size]);
  useEffect(() => { if (!z && d) fit(); }, [d, z, fit]);
  const v = z ?? { k: 2, ox: 10, oy: 10 };
  const key = id ?? 'schedule:none';
  const go = (n: typeof v) => { pushHistory(key, v); setZ(n); };
  const zoomAbout = (k: number, mx = size.w / 2, my = size.h / 2) => ({ k, ox: mx - (mx - v.ox) * (k / v.k), oy: my - (my - v.oy) * (k / v.k) });
  const host: ZoomHost = {
    fitAll: fit, fitSelection: () => false,
    zoomWindow: (a, b) => { if (!size.w) return; const k = Math.min(size.w / Math.max(Math.abs(b.x - a.x), 1), size.h / Math.max(Math.abs(b.y - a.y), 1)); go({ k, ox: size.w / 2 - ((a.x + b.x) / 2) * k, oy: size.h / 2 - ((a.y + b.y) / 2) * k }); },
    actual: () => go(zoomAbout(PX_PER_MM)), zoomBy: (f) => go(zoomAbout(v.k * f)),
    previous: () => { const p = popHistory<typeof v>(key); if (!p) return false; setZ(p); return true; },
  };
  useZoomHost(active, host);
  const middleTwice = useRef(middleDouble());
  const wheelBurst = useRef(0);
  const toScreen = useCallback((p: { x: number; y: number }) => ({ x: p.x * v.k + v.ox, y: p.y * v.k + v.oy }), [v.k, v.ox, v.oy]);

  if (!node) return <div className="view-pane"><p className="muted">No schedule.</p></div>;
  // Saved where the user says on the desktop, downloaded in a browser (report #66).
  const csv = async () => {
    const text = await query<string>('schedule.csv', { id: node.id });
    if (text == null) return;
    // A byte order mark first, so a spreadsheet reads the CSV as UTF-8 (ă, ș, ț, ø).
    const saved = await saveFile(new TextEncoder().encode(`﻿${text}`), `${safeFileName(node.name, 'Schedule')}.csv`, 'text/csv');
    if (saved) notify(`Exported ${saved}.`);
  };
  const xlsx = async () => {
    const b64 = await query<string>('schedule.xlsx', { id: node.id });
    if (!b64) return;
    const bytes = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
    const saved = await saveFile(bytes, `${safeFileName(node.name, 'Schedule')}.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    if (saved) notify(`Exported ${saved}.`);
  };
  const problems = d ? d.problems.twice + d.problems.none + d.problems.marks.length : 0;
  const kind = String(node.props.kind ?? 'all');
  const joinery = ['door', 'window', 'all', 'lintel'].includes(kind);
  // The type assemblies schedule draws each type's build-up after its table (reports #65–#67), at its scale.
  const types = kind === 'types';
  const buildUps = types && (d?.drawings ?? []).includes('buildup');
  const addRow = () => {
    if (!joinery) { void execute('architecture.addScheduleManualRow', { id: node.id }); return; }
    const l = window.prompt('Row label');
    if (l?.trim()) void execute('architecture.addScheduleRow', { id: node.id, label: l.trim() });
  };
  const addColumn = () => {
    const l = window.prompt(joinery ? 'Mark of the new column' : 'Column name');
    if (l?.trim()) void execute('architecture.addScheduleColumn', { id: node.id, label: l.trim() });
  };
  const sortBy = (key: string) => {
    const desc = node.props.sortBy === key && node.props.sortDesc !== true;
    void execute('architecture.updateSchedule', { id: node.id, sortBy: node.props.sortBy === key && node.props.sortDesc === true ? null : key, sortDesc: desc ? true : null });
  };
  return (
    <div className="view-pane schedule-pane">
      <div className="view-toolbar">
        <span className="view-title">{node.name}</span>
        {problems > 0 && <span className="chip warn" title={[...(d?.problems.marks ?? []), d?.problems.twice ? `${d.problems.twice} in two tables` : '', d?.problems.none ? `${d.problems.none} in none` : ''].filter(Boolean).join('\n')}>{problems} to check</span>}
        {types && (
          <label className="chip" title="Each type's build-up drawn after the table, cut through its layers: on screen, on sheets and in the PDF" data-testid="schedule-buildups">
            <input type="checkbox" checked={buildUps} onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, drawings: e.target.checked ? ['buildup'] : [] })} />
            Build-up drawings
          </label>
        )}
        {(joinery || buildUps) && (
          <select value={String(node.props.scale ?? 50)} title="Scale of the drawings" onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, scale: Number(e.target.value) })}>
            {[10, 20, 25, 50, 100].map((s) => <option key={s} value={s}>1:{s}</option>)}
          </select>
        )}
        {joinery && <>
        <select value={String(node.props.sort ?? '')} title="Order of the cards" onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, sort: e.target.value || null })}>
          <option value="">by kind and mark</option><option value="name">by name</option><option value="width">by width</option><option value="height">by height</option><option value="total">most first</option>
        </select>
        </>}
        {joinery && kind !== 'lintel' && ((d?.unusedCount ?? 0) > 0 || d?.unused) && (
          <label className="chip" title="Types no door or window in the model uses: left out unless shown" data-testid="schedule-unused">
            <input type="checkbox" checked={d?.unused === true} onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, unused: e.target.checked || null })} />
            {`Unused types (${d?.unusedCount ?? 0})`}
          </label>
        )}
        <button type="button" className="icon" title={joinery ? 'A row of your own' : 'A row of your own, typed in'} onClick={addRow} data-testid="schedule-add-row"><Plus size={14} /><span className="small">Row</span></button>
        {kind !== 'lintel' && <button type="button" className="icon" title={joinery ? 'A column of your own: a card with no type, every value typed' : 'A column of your own, typed in'} onClick={addColumn} data-testid="schedule-add-column" data-i18n-ctx="schedule"><Plus size={14} /><span className="small">Column</span></button>}
        <button type="button" className={`icon${side === 'charts' ? ' on' : ''}`} title="Charts of the table" onClick={() => setSide(side === 'charts' ? null : 'charts')} data-testid="schedule-charts-toggle"><ChartColumn size={14} /><span className="small">Charts</span></button>
        {kind === 'quantities' && <TakeoffControls id={node.id} groupBy={node.props.groupBy} classes={node.props.classes} />}
        <select value="" title="Place the schedule on a sheet: it is drawn there as it is here, follows the model, and prints and goes to PDF with the sheet" data-testid="schedule-place"
          onChange={(e) => { const v = e.target.value; if (v) void placeOnSheet(node.id, v === 'new' ? null : v); }}>
          <option value="">Place on sheet…</option>
          <option value="new">New sheet</option>
          {sheetsFor(node.id).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <button type="button" className="icon" title="Export CSV" onClick={() => void csv()}><FileDown size={14} /><span className="small">CSV</span></button>
        <button type="button" className="icon" title="Export XLSX (a sheet a table)" onClick={() => void xlsx()}><FileDown size={14} /><span className="small">XLSX</span></button>
        {joinery && <button type="button" className={`icon${side === 'settings' ? ' on' : ''}`} title="Rows, card drawings, tables, what to check" onClick={() => setSide(side === 'settings' ? null : 'settings')}><Settings2 size={14} /><span className="small">Settings</span></button>}
        <button type="button" className="chip scale" title="Size on screen against paper · click for paper size (ZA)" onClick={host.actual}>{Math.round((v.k / PX_PER_MM) * 100)} %</button>
        <button type="button" className="icon" title="Fit (ZE)" onClick={fit}><Maximize size={14} /></button>
      </div>
      <div className="schedule-body">
      <svg ref={ref} className="plan-svg sheet-svg schedule-svg" data-testid="schedule"
        onWheel={(e) => {
          const r = ref.current!.getBoundingClientRect();
          const now = performance.now();
          if (now - wheelBurst.current > 400) pushHistory(key, v);
          wheelBurst.current = now;
          setZ(zoomAbout(v.k * wheelZoom(e, size.h), e.clientX - r.left, e.clientY - r.top));
        }}
        onPointerDown={(e) => { if (middleTwice.current(e)) { fit(); return; } drag.current = { x: e.clientX, y: e.clientY, start: v }; (e.target as Element).setPointerCapture?.(e.pointerId); }}
        onPointerMove={(e) => { const g = drag.current; if (g) setZ({ ...g.start, ox: g.start.ox + e.clientX - g.x, oy: g.start.oy + e.clientY - g.y }); }}
        onPointerUp={() => { drag.current = null; }}>
        {d && <><rect x={v.ox - 5 * v.k} y={v.oy - 5 * v.k} width={(d.w + 10) * v.k} height={(d.h + 10) * v.k} className="paper-sheet" />
          <DrawingLayer drawing={{ pens: d.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} minText={1} /></>}
      </svg>
      {side === 'settings' && d && joinery && <ScheduleSettings id={node.id} s={d} kind={kind} />}
      {side === 'charts' && d && <ScheduleChartsPanel id={node.id} charts={d.charts ?? []} columns={d.chartColumns ?? []} />}
      </div>
      {d && joinery && d.entries.length > 0 && (
        <div className="schedule-grid">
          <table>
            <thead><tr><th>Mark</th>{d.entries.map((e) => (
              <th key={e.key}>
                {e.manual
                  ? <><CellInput value={e.mark} title="The column's mark" onCommit={(t) => { if (t.trim()) void execute('architecture.renameScheduleColumn', { id: node.id, column: e.key, label: t }); }} />
                    <button type="button" className="icon mini" title="Remove the column" onClick={() => void execute('architecture.removeScheduleColumn', { id: node.id, column: e.key })}>×</button></>
                  : e.mark}
              </th>
            ))}</tr></thead>
            <tbody>
              {d.rows.map((r) => (
                <tr key={r.id}>
                  <th title={r.param ? 'A parameter of the types: what is typed here goes into the type' : undefined}>{r.label}{!r.fromModel && <button type="button" className="icon mini" title="Remove the row" onClick={() => void execute('architecture.removeScheduleRow', { id: node.id, rowId: r.id })}>×</button>}</th>
                  {d.entries.map((e) => {
                    const over = d.overridden?.[e.key]?.includes(r.id);
                    const shown = e.rows[r.id] ?? '';
                    if (r.param && !e.manual) {
                      return (
                        <td key={e.key} className="param" title={`${r.param} of ${e.name}: typed into the type (its elements' own values stay theirs)`}>
                          <CellInput value={shown === '—' ? '' : shown} onCommit={(t) => void setTypeParam(e.type ?? e.key, r.param!, t)} testId={`param-${r.param}-${e.mark}`} />
                        </td>
                      );
                    }
                    return (
                      <td key={e.key} className={over ? 'overridden' : ''} title={over ? 'Typed over the model\'s value' : undefined}>
                        {over && <button type="button" className="icon mini" title="Back to the model's value" onClick={() => void execute('architecture.scheduleCell', { id: node.id, entry: e.key, row: r.id, text: null })}>↺</button>}
                        <CellInput value={shown} onCommit={(t) => void execute('architecture.scheduleCell', { id: node.id, entry: e.key, row: r.id, text: t })} />
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {d?.grid && (
        <div className="schedule-grid" data-testid="schedule-grid">
          <table>
            <thead><tr><th />{d.grid.columns.map((c) => (
              <th key={c.key}>
                {c.custom
                  ? <><CellInput value={c.label} title="The column's name" onCommit={(t) => { if (t.trim()) void execute('architecture.renameScheduleColumn', { id: node.id, column: c.key, label: t }); }} />
                    <button type="button" className="icon mini" title="Remove the column" onClick={() => void execute('architecture.removeScheduleColumn', { id: node.id, column: c.key })}>×</button></>
                  : c.label}
                <button type="button" className={`icon mini${node.props.sortBy === c.key ? ' on' : ''}`} title="Sort by this column (again: the other way, then as the model lists)" onClick={() => sortBy(c.key)}>
                  {node.props.sortBy === c.key && node.props.sortDesc === true ? <ArrowDown size={10} /> : <ArrowUp size={10} />}
                </button>
              </th>
            ))}</tr></thead>
            <tbody>
              {d.grid.rows.map((r) => (
                <tr key={r.key} className={r.manual ? 'manual' : ''}>
                  <th>{r.manual && <button type="button" className="icon mini" title="Remove the row" onClick={() => void execute('architecture.removeScheduleManualRow', { id: node.id, row: r.key })}>×</button>}</th>
                  {d.grid!.columns.map((c, i) => {
                    const over = r.typed.includes(c.key);
                    return (
                      <td key={c.key} className={over ? 'overridden' : ''} title={over ? 'Typed over the model\'s value' : undefined}>
                        {over && <button type="button" className="icon mini" title="Back to the model's value" onClick={() => void execute('architecture.scheduleCell', { id: node.id, entry: r.key, row: c.key, text: null })}>↺</button>}
                        <CellInput value={r.cells[i] ?? ''} onCommit={(t) => void execute('architecture.scheduleCell', { id: node.id, entry: r.key, row: c.key, text: t })} />
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
