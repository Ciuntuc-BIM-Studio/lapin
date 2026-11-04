/**
 * Add to task (TA, docs/plan-4d5d.md, stage 2): the elements selected put in a task picked from the schedule's list (its
 * WBS, name and how many elements it takes), or taken out of it. Each choice one step to undo; the task's rule stays as it
 * was, the elements going into its exceptions.
 */

import { useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';
import { elementIds, tasksHolding } from './links';
import { assignTo, useSchedule } from './schedule';

export function AssignDialog() {
  const data = useSchedule();
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const ids = useMemo(() => elementIds(selected, nodes), [selected, nodes]);
  const [filter, setFilter] = useState('');
  const [remove, setRemove] = useState(false);
  if (!data) return <PendingDialog title="Add to task" />;
  const close = () => useUi.setState({ dialog: null });
  const holding = tasksHolding(data.tasks, ids);
  const f = filter.trim().toLowerCase();
  const rows = data.tasks.filter((t) => !f || t.name.toLowerCase().includes(f) || t.wbs.startsWith(f));
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Add to task">
      <div className="dialog assign-dialog" data-testid="assign-dialog">
        <header className="dialog-head">
          <span className="panel-title">Add to task</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">{`${ids.length} selected`}</p>
          <input autoFocus placeholder="Find a task" value={filter} onChange={(e) => setFilter(e.target.value)} data-testid="assign-filter"
            onKeyDown={(e) => { if (e.key === 'Enter' && rows[0]) void assignTo(rows[0].id, ids, remove).then(close); }} />
          <label className="check"><input type="checkbox" checked={remove} onChange={(e) => setRemove(e.target.checked)} />Take them out of the task instead</label>
          {data.tasks.length === 0 ? <p className="muted">No tasks yet. Add the first in the Gantt (Planning → Gantt).</p> : (
            <ul className="assign-list">
              {rows.map((t) => (
                <li key={t.id}>
                  <button type="button" className={`assign-row${holding.has(t.id) ? ' holds' : ''}`} style={{ paddingLeft: 6 + t.level * 12 }}
                    onClick={() => void assignTo(t.id, ids, remove).then(close)} data-task={t.id}>
                    <span className="muted" data-no-i18n>{t.wbs}</span>
                    <span data-no-i18n>{t.name}</span>
                    <span className="muted small">{t.count === 1 ? '1 element' : t.count ? `${t.count} elements` : ''}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
