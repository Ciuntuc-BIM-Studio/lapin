/** A room's imposed load in its inspector: its category (from its function unless chosen) and the qk it gives. */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { selectionWith, sharedValue } from '../../shell/params';
import { useSession } from '../../engine/session';
import { useAnalysis } from './hooks';
import { formatKn, type CategoryDto } from './loads';

const VARIES = '~varies';
const FROM_FUNCTION = '';
const NONE = 'none';

export function OccupancySection({ node }: { node: NodeDto }) {
  const cats = useAnalysis<CategoryDto[]>('analysis.categories');
  const nodes = useSession((s) => s.nodes);
  if (!cats) return null;
  const ids = selectionWith(node, (n) => n.type === 'IfcSpace');
  const shared = sharedValue(ids, 'loadCategory', (id) => nodes.get(id)?.props);
  const value = shared.varies ? VARIES : typeof shared.value === 'string' ? shared.value : FROM_FUNCTION;
  const qk = cats.find((c) => c.key === value)?.qk;
  return (
    <dl className="fields">
      <dt>Category</dt>
      <dd><select data-field="load-category" value={value} onChange={(e) => void execute('analysis.setOccupancy', { ids, category: e.target.value === FROM_FUNCTION ? null : e.target.value })}>
        {shared.varies && <option value={VARIES} disabled>varies</option>}
        <option value={FROM_FUNCTION}>From function</option>
        <option value={NONE}>None</option>
        {cats.map((c) => <option key={c.key} value={c.key}>{c.key} · {c.label}</option>)}
      </select></dd>
      {qk != null && <><dt>qk</dt><dd>{formatKn(qk)} kN/m²</dd></>}
    </dl>
  );
}
