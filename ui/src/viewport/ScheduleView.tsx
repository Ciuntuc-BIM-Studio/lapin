/**
 * A joinery or lintel schedule: its cards as they print (the engine lays
 * them out), and under them the table to type in — a custom row's values, or
 * a model value overridden on purpose. Scale, order, rows and CSV here; a
 * sheet places it.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { FileDown, Maximize, Plus } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { execute, query, useSession } from '../engine/session';
import { DrawingLayer } from './DrawingLayer';
import type { PenDto } from './drawing';

interface Layout {
  w: number; h: number; pens: PenDto[];
  rows: { id: string; label: string; fromModel: boolean }[];
  entries: { key: string; mark: string; name: string; total: number; rows: Record<string, string> }[];
  problems: { twice: number; none: number; marks: string[] };
}

export function ScheduleView() {
  const id = useUi((s) => s.scheduleId);
  const nodes = useSession((s) => s.nodes);
  const node = id ? nodes.get(id) : undefined;
  const [d, setD] = useState<Layout | null>(null);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [z, setZ] = useState<{ k: number; ox: number; oy: number } | null>(null);
  const drag = useRef<{ x: number; y: number; start: { k: number; ox: number; oy: number } } | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    const t = setTimeout(() => { void query<Layout>('schedule.layout', { id }).then((r) => { if (live && r) setD(r); }); }, 100);
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
  const toScreen = useCallback((p: { x: number; y: number }) => ({ x: p.x * v.k + v.ox, y: p.y * v.k + v.oy }), [v.k, v.ox, v.oy]);

  if (!node) return <div className="view-pane"><p className="muted">No schedule.</p></div>;
  const csv = async () => {
    const text = await query<string>('schedule.csv', { id: node.id });
    if (text == null) return;
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([text], { type: 'text/csv' }));
    a.download = `${node.name}.csv`; a.click();
    notify(`Exported ${node.name}.csv.`);
  };
  const problems = d ? d.problems.twice + d.problems.none + d.problems.marks.length : 0;
  return (
    <div className="view-pane schedule-pane">
      <div className="view-toolbar">
        <span className="view-title">{node.name}</span>
        {problems > 0 && <span className="chip warn" title={[...(d?.problems.marks ?? []), d?.problems.twice ? `${d.problems.twice} in two tables` : '', d?.problems.none ? `${d.problems.none} in none` : ''].filter(Boolean).join('\n')}>{problems} to check</span>}
        <select value={String(node.props.scale ?? 50)} title="Scale of the drawings" onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, scale: Number(e.target.value) })}>
          {[10, 20, 25, 50, 100].map((s) => <option key={s} value={s}>1:{s}</option>)}
        </select>
        <select value={String(node.props.sort ?? '')} title="Order of the cards" onChange={(e) => void execute('architecture.updateSchedule', { id: node.id, sort: e.target.value || null })}>
          <option value="">by kind and mark</option><option value="name">by name</option><option value="width">by width</option><option value="height">by height</option><option value="total">most first</option>
        </select>
        <button type="button" className="icon" title="A row of your own" onClick={() => { const l = window.prompt('Row label'); if (l?.trim()) void execute('architecture.addScheduleRow', { id: node.id, label: l.trim() }); }}><Plus size={14} /><span className="small">Row</span></button>
        <button type="button" className="icon" title="Export CSV" onClick={() => void csv()}><FileDown size={14} /><span className="small">CSV</span></button>
        <button type="button" className="icon" title="Fit" onClick={fit}><Maximize size={14} /></button>
      </div>
      <svg ref={ref} className="plan-svg sheet-svg schedule-svg" data-testid="schedule"
        onWheel={(e) => { const r = ref.current!.getBoundingClientRect(); const mx = e.clientX - r.left, my = e.clientY - r.top, f = Math.exp(-e.deltaY * 0.0015); setZ({ k: v.k * f, ox: mx - (mx - v.ox) * f, oy: my - (my - v.oy) * f }); }}
        onPointerDown={(e) => { drag.current = { x: e.clientX, y: e.clientY, start: v }; (e.target as Element).setPointerCapture?.(e.pointerId); }}
        onPointerMove={(e) => { const g = drag.current; if (g) setZ({ ...g.start, ox: g.start.ox + e.clientX - g.x, oy: g.start.oy + e.clientY - g.y }); }}
        onPointerUp={() => { drag.current = null; }}>
        {d && <><rect x={v.ox - 5 * v.k} y={v.oy - 5 * v.k} width={(d.w + 10) * v.k} height={(d.h + 10) * v.k} className="paper-sheet" />
          <DrawingLayer drawing={{ pens: d.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} /></>}
      </svg>
      {d && d.entries.length > 0 && (
        <div className="schedule-grid">
          <table>
            <thead><tr><th>Mark</th>{d.entries.map((e) => <th key={e.key}>{e.mark}</th>)}</tr></thead>
            <tbody>
              {d.rows.map((r) => (
                <tr key={r.id}>
                  <th>{r.label}{!r.fromModel && <button type="button" className="icon mini" title="Remove the row" onClick={() => void execute('architecture.removeScheduleRow', { id: node.id, rowId: r.id })}>×</button>}</th>
                  {d.entries.map((e) => (
                    <td key={e.key}>
                      <input key={`${e.key}${r.id}${e.rows[r.id]}`} defaultValue={e.rows[r.id] ?? ''}
                        onKeyDown={(ev) => { if (ev.key === 'Enter') (ev.target as HTMLInputElement).blur(); }}
                        onBlur={(ev) => ev.target.value !== (e.rows[r.id] ?? '') && void execute('architecture.scheduleCell', { id: node.id, entry: e.key, row: r.id, text: ev.target.value })} />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
