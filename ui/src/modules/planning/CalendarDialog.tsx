/**
 * The work calendar (Planning → Calendar…): the weekdays worked, the day's start and hours, Romania's legal holidays
 * (computed each year, Orthodox Easter and Pentecost included) or none, the winter break the wet tasks keep (off by
 * default), and the project's own exceptions (a day off, a Saturday worked). A year's holidays are listed to check.
 */

import { useEffect, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';
import { showDate, type CalendarException } from './gantt';
import { useSchedule } from './schedule';

const WEEK = [[1, 'Mon'], [2, 'Tue'], [3, 'Wed'], [4, 'Thu'], [5, 'Fri'], [6, 'Sat'], [7, 'Sun']] as const;

const set = (args: Record<string, unknown>) => void execute('planning.setCalendar', args);

export function CalendarDialog() {
  const data = useSchedule();
  const error = useSession((s) => s.error);
  const [year, setYear] = useState(() => new Date().getFullYear());
  const [holidays, setHolidays] = useState<{ date: string; name: string }[]>([]);
  useEffect(() => { void query<{ date: string; name: string }[]>('planning.holidays', { year }).then((h) => setHolidays(h ?? [])); }, [year]);
  if (!data) return <PendingDialog title="Work calendar" />;
  const c = data.calendar;
  const exceptions = c.exceptions;
  const putExceptions = (list: CalendarException[]) => set({ exceptions: list });
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Work calendar">
      <div className="dialog calendar-dialog" data-testid="calendar-dialog">
        <header className="dialog-head">
          <span className="panel-title">Work calendar</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Name</dt>
            <dd><input key={c.name} defaultValue={c.name} data-no-i18n onBlur={(e) => e.target.value.trim() && e.target.value !== c.name && set({ name: e.target.value })} /></dd>
            <dt>Working days</dt>
            <dd className="calendar-days">
              {WEEK.map(([d, label]) => (
                <label key={d}><input type="checkbox" checked={c.days.includes(d)}
                  onChange={(e) => set({ days: e.target.checked ? [...c.days, d].sort() : c.days.filter((x) => x !== d) })} />{label}</label>
              ))}
            </dd>
            <dt>Day starts at</dt>
            <dd><input type="time" value={c.dayStart} onChange={(e) => e.target.value && set({ dayStart: e.target.value })} /></dd>
            <dt>Hours a day</dt>
            <dd><input key={c.hours} type="number" min={1} max={24} step={0.5} defaultValue={c.hours} onBlur={(e) => { const n = Number(e.target.value); if (n > 0 && n <= 24 && n !== c.hours) set({ hours: n }); }} /></dd>
            <dt>Holidays</dt>
            <dd><select value={c.holidays} onChange={(e) => set({ holidays: e.target.value })} data-testid="calendar-holidays">
              <option value="ro">Romania's legal holidays</option>
              <option value="none">None (only the exceptions below)</option>
            </select></dd>
            <dt title="Concrete, screeds, plaster: the tasks marked wet do not work between these days">Winter break</dt>
            <dd>
              <label><input type="checkbox" checked={c.winter.on} onChange={(e) => set({ winter: { on: e.target.checked } })} data-testid="calendar-winter" /> for wet works</label>
              {c.winter.on && <span className="calendar-days">
                <label>from <input key={c.winter.from} defaultValue={c.winter.from} size={5} title="Month and day: 12-15" onBlur={(e) => /^\d{2}-\d{2}$/.test(e.target.value) && set({ winter: { from: e.target.value } })} /></label>
                <label>to <input key={c.winter.to} defaultValue={c.winter.to} size={5} title="Month and day: 03-01" onBlur={(e) => /^\d{2}-\d{2}$/.test(e.target.value) && set({ winter: { to: e.target.value } })} /></label>
              </span>}
            </dd>
          </dl>
          <h4 className="eyebrow">Exceptions</h4>
          <table className="standards-table">
            <thead><tr><th>First day</th><th>Last day</th><th>Worked</th><th>Name</th><th /></tr></thead>
            <tbody>
              {exceptions.map((x, i) => (
                <tr key={`${x.from}-${i}`}>
                  <td><input type="date" value={x.from} onChange={(e) => e.target.value && putExceptions(exceptions.map((y, j) => (j === i ? { ...y, from: e.target.value, to: y.to < e.target.value ? e.target.value : y.to } : y)))} /></td>
                  <td><input type="date" value={x.to} onChange={(e) => e.target.value && putExceptions(exceptions.map((y, j) => (j === i ? { ...y, to: e.target.value } : y)))} /></td>
                  <td><input type="checkbox" checked={x.working} title="A day worked that would not be (a Saturday); unticked: a day off" onChange={(e) => putExceptions(exceptions.map((y, j) => (j === i ? { ...y, working: e.target.checked } : y)))} /></td>
                  <td><input key={x.name} defaultValue={x.name} data-no-i18n onBlur={(e) => e.target.value !== x.name && putExceptions(exceptions.map((y, j) => (j === i ? { ...y, name: e.target.value } : y)))} /></td>
                  <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => putExceptions(exceptions.filter((_, j) => j !== i))}><Trash2 size={12} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn" onClick={() => { const d = data.start; putExceptions([...exceptions, { from: d, to: d, working: false, name: '' }]); }}><Plus size={14} />Add an exception</button>
          {c.holidays === 'ro' && <>
            <h4 className="eyebrow">Legal holidays in <input type="number" value={year} min={1900} max={2099} style={{ width: '5em' }} onChange={(e) => setYear(Number(e.target.value) || year)} /></h4>
            <ul className="small" data-testid="calendar-holiday-list">
              {holidays.map((h) => <li key={h.date}>{showDate(h.date)} · <span data-no-i18n>{h.name}</span></li>)}
            </ul>
          </>}
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}
