/**
 * A schedule's charts beside it (decision 251): each one made from the table — the column its categories come from, the
 * column it adds up (or the rows counted) and how — drawn by the engine after the tables, so the screen, the sheets and the
 * PDF show the same chart.
 */

import { useState } from 'react';
import { BarChart3, ChartColumn, ChartLine, ChartPie, Pencil, Plus, X } from 'lucide-react';
import { execute } from '../engine/session';
import { NumberField } from '../shell/cells';

export interface ChartDto { id: string; kind: string; title?: string; heading: string; category: string; value?: string | null; aggregate: string; top?: number | null }

const KINDS: [string, string, typeof BarChart3][] = [['column', 'Columns', ChartColumn], ['bar', 'Bars', BarChart3], ['pie', 'Pie', ChartPie], ['line', 'Line', ChartLine]];
const AGGREGATES: [string, string][] = [['sum', 'Sum'], ['avg', 'Average'], ['min', 'Least'], ['max', 'Most'], ['count', 'Count of rows']];

type Draft = Omit<ChartDto, 'heading'>;

/** A first guess: categories from the first column, values from the last that holds numbers (Total, Area…). */
function guess(columns: string[]): Draft {
  const numeric = ['Total', 'Area', 'Area (m²)', 'Count', 'Length', 'Volume', 'Quantity', 'Mass (kg)'];
  const value = [...columns].reverse().find((c) => numeric.some((n) => c.toLowerCase().startsWith(n.toLowerCase())));
  return { id: '', kind: 'column', category: columns[1] && columns[0] === 'Mark' ? columns[1] : columns[0] ?? '', value: value ?? null, aggregate: value ? 'sum' : 'count', top: null, title: '' };
}

export function ScheduleChartsPanel({ id, charts, columns }: { id: string; charts: ChartDto[]; columns: string[] }) {
  const [draft, setDraft] = useState<Draft | null>(null);
  const save = async () => {
    if (!draft) return;
    const ok = await execute('architecture.scheduleChart', { id, chart: { ...draft, value: draft.aggregate === 'count' ? null : draft.value, title: draft.title?.trim() || null } });
    if (ok) setDraft(null);
  };
  return (
    <aside className="schedule-settings" data-testid="schedule-charts">
      <h4>Charts</h4>
      {charts.length === 0 && !draft && <p className="muted small">A chart of the table: a column for its categories, one to add up, drawn after the tables (on sheets too).</p>}
      <ul className="plain">
        {charts.map((c) => {
          const Icon = KINDS.find((k) => k[0] === c.kind)?.[2] ?? ChartColumn;
          return (
            <li key={c.id} className="row">
              <Icon size={13} />
              <span className="grow">{c.heading}</span>
              <button type="button" className="icon mini" title="Change the chart" onClick={() => setDraft({ ...c, title: c.title ?? '' })}><Pencil size={11} /></button>
              <button type="button" className="icon mini" title="Remove the chart" onClick={() => void execute('architecture.removeScheduleChart', { id, chartId: c.id })}><X size={11} /></button>
            </li>
          );
        })}
      </ul>
      {!draft && <button type="button" className="icon" disabled={columns.length === 0} onClick={() => setDraft(guess(columns))}><Plus size={13} /><span className="small">New chart</span></button>}
      {draft && (
        <div className="chart-form">
          <div className="row seg" data-i18n-ctx="chart">
            {KINDS.map(([k, label, Icon]) => (
              <button key={k} type="button" className={`icon${draft.kind === k ? ' on' : ''}`} title={label} onClick={() => setDraft({ ...draft, kind: k })}><Icon size={14} /></button>
            ))}
          </div>
          <label>Categories from
            <select value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })}>
              {columns.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
          <label>How
            <select value={draft.aggregate} onChange={(e) => setDraft({ ...draft, aggregate: e.target.value, value: e.target.value === 'count' ? null : draft.value ?? columns[columns.length - 1] })}>
              {AGGREGATES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
            </select>
          </label>
          {draft.aggregate !== 'count' && (
            <label>Of
              <select value={draft.value ?? ''} onChange={(e) => setDraft({ ...draft, value: e.target.value })}>
                {columns.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </label>
          )}
          <label>At most (categories)
            <NumberField value={draft.top ?? null} blank placeholder="all" min={2} max={50} onCommit={(v) => setDraft({ ...draft, top: v == null ? null : Math.round(v) })} />
          </label>
          <label>Title
            <input value={draft.title ?? ''} placeholder="as it shows" onChange={(e) => setDraft({ ...draft, title: e.target.value })} />
          </label>
          <div className="row">
            <button type="button" className="primary" data-testid="chart-save" onClick={() => void save()}>{draft.id ? 'Save' : 'Add'}</button>
            <button type="button" onClick={() => setDraft(null)}>Cancel</button>
          </div>
        </div>
      )}
    </aside>
  );
}
