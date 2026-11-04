/**
 * Quantities → Schedules…: every kind of schedule this edition makes (the engine's list), one click making it and
 * opening it; the quantity takeoff first (decision 287, the tester's report #75).
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { newSchedule, openSchedule, type ScheduleKind } from './schedules';

export function SchedulesDialog() {
  const [kinds, setKinds] = useState<ScheduleKind[]>([]);
  const nodes = useSession((s) => s.nodes);
  useEffect(() => { void query<ScheduleKind[]>('schedule.kinds').then((r) => setKinds(r ?? [])); }, []);
  const close = () => useUi.setState({ dialog: null });
  const made = [...nodes.values()].filter((n) => n.type === 'LapinSchedule');
  const ordered = [...kinds.filter((k) => k.kind === 'quantities'), ...kinds.filter((k) => k.kind !== 'quantities')];
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Schedules" data-testid="schedules-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Schedules</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">A schedule lists the model's quantities as they are now and follows every change. Open it to group, sort and export it (CSV, XLSX), or place it on a sheet to print it.</p>
          <h4 className="eyebrow">New schedule</h4>
          <div className="schedule-kinds">
            {ordered.map((k) => (
              <button type="button" key={k.kind} className={`btn${k.kind === 'quantities' ? ' primary' : ''}`} data-kind={k.kind} onClick={() => void newSchedule(k.kind)}>{k.label}</button>
            ))}
          </div>
          {made.length > 0 && <>
            <h4 className="eyebrow">In the project</h4>
            <ul className="plain-list">
              {made.map((n) => <li key={n.id}><button type="button" className="link" onClick={() => openSchedule(n.id)}>{n.name}</button></li>)}
            </ul>
          </>}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn" onClick={close}>Close</button></footer>
      </div>
    </div>
  );
}
