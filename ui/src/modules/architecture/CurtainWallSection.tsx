/** A curtain wall in the inspector: its type (and the configurator), its length along its axis, how many of its type there are. */

import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { arcLength, arcOf } from '../../viewport/arc';

export function CurtainWallSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const c = arcOf(node.props);
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setCurtainWallType', { ids: [node.id], typeId: e.target.value })}>
            {ofClass(model, 'IfcCurtainWallType').map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Curtain walls…" onClick={() => useUi.setState({ dialog: 'curtainWalls', curtainTypeId: type?.id ?? null })}><Settings2 size={14} /></button>
        </span>
      </label>
      <dl className="fields">
        <dt>Length</dt><dd className="num">{c ? mm(arcLength(c)) : '—'} mm</dd>
        <dt>Of its type</dt><dd className="num">{type ? relatedOf(model, type.id, 'IfcRelDefinesByType').length : 0}</dd>
      </dl>
    </div>
  );
}
