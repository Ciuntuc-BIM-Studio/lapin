/**
 * Construction → Resources… (decision 772): the project's crews, machines and formwork sets, each with its limit a day, its
 * unit and what a unit costs a day; the tasks it is on, with their units and the days a formwork stays after its task. The
 * tasks selected in the Gantt get a resource here; the formwork laid in Site works becomes a limited resource in one
 * click; Level places the tasks not started so none is asked for more than it has.
 */

import { useState } from 'react';
import { Hammer, Plus, Scale, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';
import { selectedTasks } from './schedule';
import { clearLevelling, level, money, openHistogram, useResources, type ResourceDto, type ResourceKind } from './resources';

const KINDS: [ResourceKind, string][] = [['labour', 'Crew'], ['equipment', 'Equipment'], ['formwork', 'Formwork sets']];

const update = (id: string, args: Record<string, unknown>) => void execute('planning.updateResource', { id, ...args });

export function ResourcesDialog() {
  const data = useResources();
  const error = useSession((s) => s.error);
  const [units, setUnits] = useState(1);
  const [hold, setHold] = useState(0);
  const close = () => useUi.setState({ dialog: null });
  if (!data) return <PendingDialog title="Resources" />;
  const assign = async (r: ResourceDto) => {
    const tasks = selectedTasks();
    if (!tasks.length) { notify('Select the tasks to put the resource on in the Gantt.'); return; }
    await execute('planning.assignResource', { resource: r.id, tasks, units, ...(r.kind === 'formwork' ? { hold } : {}) });
  };
  const formwork = async () => {
    const r = await execute('planning.formworkResource', {}) as { tasks: number; formworks: number } | null;
    if (r) notify(`Formwork as a resource: ${r.formworks} formwork(s) on ${r.tasks} task(s).`);
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Resources">
      <div className="dialog res-dialog" data-testid="resources-dialog">
        <header className="dialog-head">
          <span className="panel-title">Resources</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <table className="standards-table">
            <thead><tr><th>Name</th><th>Kind</th><th className="num">Available a day</th><th>Unit</th><th className="num">Cost a unit a day</th><th className="num">Peak</th><th className="num" title="Working days it is asked for more than it has">Days short</th><th>Tasks</th><th /></tr></thead>
            <tbody>
              {data.resources.map((r) => (
                <tr key={r.id} className={r.over > 0 ? 'res-over' : ''} data-testid="resource-row">
                  <td><input key={r.name} defaultValue={r.name} data-no-i18n aria-label="Name" onBlur={(e) => e.target.value.trim() && e.target.value !== r.name && update(r.id, { name: e.target.value })} /></td>
                  <td>{KINDS.find((k) => k[0] === r.kind)?.[1]}</td>
                  <td className="num"><input key={`c${r.capacity}`} type="number" min={0} step={1} defaultValue={r.capacity} style={{ width: '5em' }} aria-label="Available a day" data-testid="resource-capacity" onBlur={(e) => Number(e.target.value) !== r.capacity && update(r.id, { capacity: Number(e.target.value) })} /></td>
                  <td><input key={`u${r.unit}`} defaultValue={r.unit} size={7} aria-label="Unit" onBlur={(e) => e.target.value !== r.unit && update(r.id, { unit: e.target.value })} /></td>
                  <td className="num"><input key={`r${r.rate}`} type="number" min={0} step={10} defaultValue={r.rate} style={{ width: '6em' }} aria-label="Cost a unit a day" onBlur={(e) => Number(e.target.value) !== r.rate && update(r.id, { rate: Number(e.target.value) })} /></td>
                  <td className="num" data-no-i18n>{r.peak}</td>
                  <td className="num" data-no-i18n>{r.over || ''}</td>
                  <td className="small">
                    {r.assign.map((a) => (
                      <span key={a.task} className="res-chip" data-no-i18n>{a.name} ×{a.units}{a.hold ? ` +${a.hold}d` : ''}
                        <button type="button" className="icon mini" title="Take off the task" onClick={() => void execute('planning.assignResource', { resource: r.id, tasks: [a.task], units: 0 })}><X size={10} /></button>
                      </span>
                    ))}
                    <button type="button" className="btn small" data-testid="resource-assign" onClick={() => void assign(r)} title="Put it on the tasks selected in the Gantt, with the units below">+ selected tasks</button>
                  </td>
                  <td className="row-tools"><button type="button" className="icon mini" title="Delete" onClick={() => void execute('element.delete', { ids: [r.id] })}><Trash2 size={12} /></button></td>
                </tr>
              ))}
              {data.resources.length === 0 && <tr><td colSpan={9} className="muted">No resources yet: add a crew, a machine or the formwork sets.</td></tr>}
            </tbody>
          </table>
          <div className="row" style={{ gap: 6, marginTop: 8, flexWrap: 'wrap' }}>
            {KINDS.map(([kind, label]) => (
              <button key={kind} type="button" className="btn" data-testid={`resource-add-${kind}`} onClick={() => void execute('planning.addResource', { kind, name: label })}><Plus size={14} />{label}</button>
            ))}
            <button type="button" className="btn" onClick={() => void formwork()} title="One set a formwork laid in Site works, held by the task casting its host until it is struck"><Hammer size={14} />Formwork from site works</button>
          </div>
          <dl className="fields" style={{ marginTop: 10 }}>
            <dt>Units on a task</dt>
            <dd><input type="number" min={0} step={0.5} value={units} data-testid="resource-units" onChange={(e) => setUnits(Number(e.target.value))} style={{ width: '5em' }} /></dd>
            <dt title="Working days a formwork set stays after its task (struck later)">Kept after (formwork)</dt>
            <dd><input type="number" min={0} step={1} value={hold} onChange={(e) => setHold(Number(e.target.value))} style={{ width: '5em' }} /> <span className="muted small">working days</span></dd>
          </dl>
          <div className="row" style={{ gap: 6, marginTop: 10 }}>
            <button type="button" className="btn primary" onClick={() => void level()} data-testid="resources-level"><Scale size={14} />Level resources</button>
            {data.levelled > 0 && <button type="button" className="btn" onClick={clearLevelling}>Clear levelling <span data-no-i18n>{`(${data.levelled})`}</span></button>}
            <button type="button" className="btn" onClick={() => { close(); openHistogram(); }}>Histogram</button>
            <span className="muted small" data-no-i18n>{data.resources.length ? `${money(data.resources.reduce((s, r) => s + r.cost, 0))}` : ''}</span>
          </div>
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}
