/**
 * A schedule's settings beside it: which rows and in what order, what each
 * card draws, its tables (by kind, level, inside/outside, types), and the
 * elements to check (listed twice, or nowhere), marked reviewed on purpose.
 */

import { ArrowDown, ArrowUp, Plus, X } from 'lucide-react';
import { execute } from '../engine/session';

export interface Part { id: string; title: string; kind?: string; levels?: string[]; external?: boolean; types?: string[] }
export interface Settings {
  available: { id: string; label: string }[];
  order: string[];
  drawings: string[];
  parts: Part[];
  reviewed: string[];
  check: { id: string; name?: string; why: string }[];
}

const DRAWINGS: [string, string][] = [['interior', 'Interior view'], ['exterior', 'Exterior view']];

export function ScheduleSettings({ id, s, kind }: { id: string; s: Settings; kind: string }) {
  const update = (patch: Record<string, unknown>) => void execute('architecture.updateSchedule', { id, ...patch });
  const order = s.order;
  const shown = new Set(order);
  const move = (i: number, d: number) => { const o = [...order]; const j = i + d; if (j < 0 || j >= o.length) return; [o[i], o[j]] = [o[j], o[i]]; update({ rows: o }); };
  const setPart = (i: number, patch: Partial<Part>) => update({ parts: s.parts.map((p, k) => (k === i ? { ...p, ...patch } : p)) });
  const label = (rid: string) => s.available.find((a) => a.id === rid)?.label ?? rid;
  return (
    <aside className="schedule-settings">
      <h4>Rows</h4>
      <ul className="plain">
        {order.map((r, i) => (
          <li key={r} className="row">
            <input type="checkbox" checked onChange={() => update({ rows: order.filter((x) => x !== r) })} />
            <span className="grow">{label(r)}</span>
            <button type="button" className="icon mini" title="Up" onClick={() => move(i, -1)}><ArrowUp size={11} /></button>
            <button type="button" className="icon mini" title="Down" onClick={() => move(i, 1)}><ArrowDown size={11} /></button>
          </li>
        ))}
        {s.available.filter((a) => !shown.has(a.id)).map((a) => (
          <li key={a.id} className="row muted">
            <input type="checkbox" checked={false} onChange={() => update({ rows: [...order, a.id] })} /><span className="grow">{a.label}</span>
          </li>
        ))}
      </ul>
      <button type="button" className="link" onClick={() => update({ rows: null })}>Back to the default rows</button>

      {kind !== 'lintel' && <>
        <h4>On each card</h4>
        {DRAWINGS.map(([k, l]) => (
          <label key={k} className="row"><input type="checkbox" checked={s.drawings.includes(k)}
            onChange={(e) => update({ drawings: e.target.checked ? [...s.drawings, k] : s.drawings.filter((x) => x !== k) })} /> {l}</label>
        ))}
      </>}

      <h4>Tables</h4>
      {s.parts.length === 0 && <p className="muted small">One table of everything. Split it by kind, level, inside or outside:</p>}
      {s.parts.map((p, i) => (
        <div key={p.id} className="schedule-part">
          <div className="row">
            <input defaultValue={p.title} key={p.title} onBlur={(e) => e.target.value !== p.title && setPart(i, { title: e.target.value })} placeholder="Title" />
            <button type="button" className="icon mini" title="Remove the table" onClick={() => update({ parts: s.parts.filter((_, k) => k !== i) })}><X size={11} /></button>
          </div>
          <div className="row">
            <select value={p.kind ?? ''} onChange={(e) => setPart(i, { kind: e.target.value || undefined })}><option value="">doors and windows</option><option value="door">doors</option><option value="window">windows</option></select>
            <select value={p.external == null ? '' : p.external ? 'yes' : 'no'} onChange={(e) => setPart(i, { external: e.target.value === '' ? undefined : e.target.value === 'yes' })}>
              <option value="">inside and outside</option><option value="yes">outside</option><option value="no">inside</option>
            </select>
          </div>
          <input className="wide" placeholder="Levels (comma between; empty: all)" defaultValue={(p.levels ?? []).join(', ')} key={(p.levels ?? []).join(',')}
            onBlur={(e) => setPart(i, { levels: e.target.value.split(',').map((x) => x.trim()).filter(Boolean) })} />
        </div>
      ))}
      <button type="button" className="btn" onClick={() => update({ parts: [...s.parts, { id: `p${Date.now().toString(36)}`, title: `Table ${s.parts.length + 1}` }] })}><Plus size={12} />Table</button>

      {(s.check.length > 0 || s.reviewed.length > 0) && <>
        <h4>To check</h4>
        <ul className="plain">
          {s.check.map((c) => (
            <li key={c.id} className="row"><span className="grow">{c.name ?? c.id} <span className="muted small">{c.why}</span></span>
              <button type="button" className="link" onClick={() => update({ reviewed: [...s.reviewed, c.id] })}>reviewed</button></li>
          ))}
        </ul>
        {s.reviewed.length > 0 && <button type="button" className="link" onClick={() => update({ reviewed: null })}>Check the {s.reviewed.length} reviewed again</button>}
      </>}
    </aside>
  );
}
