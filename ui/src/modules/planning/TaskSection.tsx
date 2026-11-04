/**
 * A task in Properties: its name and kind, its duration (a milestone has none), its dates as the critical path gives them,
 * a constraint on its start, whether it is wet (it keeps the calendar's winter break), its predecessors with their type and
 * lag, what follows it, its floats, and notes. Every change one undo step.
 */

import { Fragment } from 'react';
import { Trash2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { select } from '../../app/store';
import { linkLabel, showDate, type LinkType } from './gantt';
import { openGantt, taskOf, useSchedule } from './schedule';
import { quantityText } from './generator';

/** IFC's kinds of task (IfcTaskTypeEnum) and how the interface names them. */
const KINDS: [string, string][] = [['', '(not set)'], ['CONSTRUCTION', 'Construction'], ['DEMOLITION', 'Demolition'], ['INSTALLATION', 'Installation'], ['RENOVATION', 'Renovation'],
  ['LOGISTIC', 'Logistics'], ['MOVE', 'Relocation'], ['REMOVAL', 'Removal'], ['DISMANTLE', 'Dismantling'], ['DISPOSAL', 'Disposal'], ['MAINTENANCE', 'Maintenance'], ['OPERATION', 'Operation'], ['ATTENDANCE', 'Attendance']];

const update = (id: string, args: Record<string, unknown>) => void execute('planning.updateTask', { id, ...args });

export function TaskSection({ node }: { node: NodeDto }) {
  const data = useSchedule();
  const t = taskOf(data, node.id);
  if (!data || !t) return null;
  const preds = data.links.filter((l) => l.to === t.id);
  const succs = data.links.filter((l) => l.from === t.id);
  const nameOf = (id: string) => data.tasks.find((x) => x.id === id);
  return (
    <dl className="fields" data-testid="task-section">
      <dt>Name</dt>
      <dd><input key={t.name} defaultValue={t.name} data-no-i18n onBlur={(e) => e.target.value.trim() && e.target.value !== t.name && update(t.id, { name: e.target.value })} /></dd>
      <dt>WBS</dt><dd>{t.wbs}</dd>
      <dt title="IFC's kind of task (IfcTaskTypeEnum)">Kind</dt>
      <dd><select value={t.kind ?? ''} onChange={(e) => update(t.id, { kind: e.target.value })}>
        {KINDS.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
      </select></dd>
      {t.summary ? <>
        <dt>Duration</dt><dd>{`${t.duration} d`} <span className="muted small">from the tasks under it</span></dd>
      </> : <>
        <dt title="In working days of the calendar">Duration</dt>
        <dd><input key={`${t.id}:${t.duration}`} type="number" min={0} step={1} defaultValue={t.duration} data-testid="task-duration"
          onBlur={(e) => { const n = Math.round(Number(e.target.value)); if (Number.isFinite(n) && n >= 0 && n !== t.duration) update(t.id, { duration: n }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /> d</dd>
        {t.durationFrom === 'norm' && t.quantity != null && <>
          <dt title="The elements' quantity × the norm's man-hours per unit ÷ (crew × hours a day)">Duration from</dt>
          <dd data-testid="task-duration-from"><span data-no-i18n>{`${quantityText(t.quantity, t.unit)} × ${t.norm ?? ''} ·`}</span> <span className="muted small">crew</span> <span data-no-i18n>{t.crew ?? 1}</span></dd>
        </>}
        {t.durationFrom === 'default' && t.gen && <>
          <dt title="The elements' quantity × the norm's man-hours per unit ÷ (crew × hours a day)">Duration from</dt>
          <dd className="muted small">the step's days (no norm)</dd>
        </>}
        <dt>Milestone</dt>
        <dd><input type="checkbox" checked={t.milestone} onChange={(e) => update(t.id, { milestone: e.target.checked })} /></dd>
      </>}
      <dt>Start</dt><dd>{showDate(t.start)}</dd>
      <dt data-i18n-ctx="schedule">Finish</dt><dd>{showDate(t.finish)}</dd>
      {!t.summary && <>
        <dt title="What holds the start, besides the links">Constraint</dt>
        <dd>
          <select value={t.constraint ?? ''} onChange={(e) => update(t.id, { constraint: e.target.value || null, ...(e.target.value ? { constraintDate: t.constraintDate ?? t.start } : {}) })} data-testid="task-constraint">
            <option value="">As soon as possible</option>
            <option value="snet">Start no earlier than</option>
            <option value="mso">Must start on</option>
          </select>
          {t.constraint && <input type="date" value={t.constraintDate ?? t.start} onChange={(e) => e.target.value && update(t.id, { constraintDate: e.target.value })} />}
        </dd>
        <dt title="Concrete, screeds, plaster: the task does not work in the calendar's winter break, when it is switched on">Wet works</dt>
        <dd><input type="checkbox" checked={!!t.wet} onChange={(e) => update(t.id, { wet: e.target.checked })} /></dd>
      </>}
      <dt title="How many working days it may slip without moving the finish">Total float</dt>
      <dd className={t.critical ? 'error-text' : undefined} style={{ margin: 0 }}>{`${t.totalFloat} d`}{t.critical ? ' · critical' : ''}</dd>
      <dt title="How many working days it may slip without moving what follows it">Free float</dt><dd>{`${t.freeFloat} d`}</dd>
      <dt>Late start</dt><dd>{showDate(t.lateStart)}</dd>
      <dt>Late finish</dt><dd>{showDate(t.lateFinish)}</dd>
      <dt>Predecessors</dt>
      <dd>
        {preds.length === 0 ? <span className="muted small">none: drag from a bar's end onto another in the Gantt</span> : (
          <table className="task-preds"><tbody>
            {preds.map((l) => (<Fragment key={l.id}>
              <tr><td colSpan={3}><button type="button" className="link" onClick={() => select(l.from)} data-no-i18n>{nameOf(l.from)?.row}. {nameOf(l.from)?.name}</button></td></tr>
              <tr>
                <td><select value={l.type} onChange={(e) => void execute('planning.updateLink', { id: l.id, type: e.target.value as LinkType })}>{(['FS', 'SS', 'FF', 'SF'] as const).map((x) => <option key={x} value={x}>{x}</option>)}</select></td>
                <td><input key={`${l.id}:${l.lag}`} type="number" step={1} defaultValue={l.lag} title="Lag in working days (negative: an overlap)"
                  onBlur={(e) => { const n = Math.round(Number(e.target.value)); if (Number.isFinite(n) && n !== l.lag) void execute('planning.updateLink', { id: l.id, lag: n }); }} /> d</td>
                <td><button type="button" className="icon mini" title="Take off the link" onClick={() => void execute('planning.unlink', { link: l.id })}><Trash2 size={11} /></button></td>
              </tr>
            </Fragment>))}
          </tbody></table>
        )}
      </dd>
      {succs.length > 0 && <>
        <dt>Successors</dt>
        <dd>{succs.map((l) => <div key={l.id}><button type="button" className="link" onClick={() => select(l.to)} data-no-i18n>{nameOf(l.to)?.row}. {nameOf(l.to)?.name}</button> <span className="muted small" data-no-i18n>{linkLabel(l)}</span></div>)}</dd>
      </>}
      {t.gen && <>
        <dt title="Made by Planning → Generate schedule…">Generated</dt>
        <dd><label className="check" title="Edited by hand: generated again, it is left as it is; untick to have it made again from its step">
          <input type="checkbox" checked={!!t.own} onChange={(e) => update(t.id, { own: e.target.checked })} data-testid="task-own" />Kept as edited</label></dd>
      </>}
      <dt>Notes</dt>
      <dd><textarea key={t.notes ?? ''} defaultValue={t.notes ?? ''} rows={2} data-no-i18n onBlur={(e) => e.target.value !== (t.notes ?? '') && update(t.id, { notes: e.target.value })} /></dd>
      <dt />
      <dd><button type="button" className="link" onClick={openGantt}>Show in the Gantt</button></dd>
    </dl>
  );
}
