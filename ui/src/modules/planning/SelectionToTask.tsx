/**
 * The selection's card over the model view while the Gantt is open (docs/plan-4d5d.md, stage 2): how many elements are
 * selected and the tasks they are in, dragged by its handle onto a task's row to put them in it (Shift on the drop takes
 * them out), or Add to task… (TA) to pick the task from a list.
 */

import { useMemo } from 'react';
import { GripVertical, ListPlus } from 'lucide-react';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { DRAG_TYPE, elementIds, tasksOf } from './links';
import { assignSelection, usePlanning } from './schedule';

export function SelectionToTask() {
  const gantt = useUi((s) => s.panes.some((p) => p.view === 'gantt'));
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const data = usePlanning((s) => s.data);
  const ids = useMemo(() => elementIds(selected, nodes), [selected, nodes]);
  if (!gantt || !ids.length || !data?.tasks.length) return null;
  const inTasks = ids.length === 1 ? tasksOf(data.tasks, ids[0]) : [];
  return (
    <div className="sel-card task-drag" role="region" aria-label="Selection to a task" data-testid="selection-to-task">
      <div className="task-drag-handle" draggable title="Drag onto a task's row in the Gantt; hold Shift when you drop to take them out of it"
        onDragStart={(e) => { e.dataTransfer.setData(DRAG_TYPE, JSON.stringify(ids)); e.dataTransfer.setData('text/plain', `${ids.length}`); e.dataTransfer.effectAllowed = 'copyMove'; }}
        data-testid="selection-drag">
        <GripVertical size={14} />
        <span>{`${ids.length} selected`}</span>
        <span className="muted small">drag onto a task</span>
      </div>
      {inTasks.length > 0 && <span className="muted small" data-no-i18n>{inTasks.map((t) => t.name).join(', ')}</span>}
      <button type="button" className="link" onClick={assignSelection}><ListPlus size={13} />Add to task…<kbd data-no-i18n>TA</kbd></button>
    </div>
  );
}
