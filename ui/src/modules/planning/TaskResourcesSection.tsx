/** A task's resources in Properties (decision 772): each resource on it with its units, and the days a formwork stays after. */

import type { NodeDto } from '../../engine/protocol';
import { useResources } from './resources';

export function TaskResourcesSection({ node }: { node: NodeDto }) {
  const data = useResources();
  const on = (data?.resources ?? []).flatMap((r) => r.assign.filter((a) => a.task === node.id).map((a) => ({ r, a })));
  return (
    <div className="res-task-section" data-testid="task-resources">
      {on.length === 0 ? <p className="muted small">No resource on this task (Construction → Resources…).</p> : (
        <ul className="small">
          {on.map(({ r, a }) => <li key={r.id}><span data-no-i18n>{r.name}</span>: <span data-no-i18n>{`${a.units} ${r.unit}${a.hold ? `, +${a.hold}` : ''}`}</span>{a.hold ? <> working days after</> : null}</li>)}
        </ul>
      )}
    </div>
  );
}
