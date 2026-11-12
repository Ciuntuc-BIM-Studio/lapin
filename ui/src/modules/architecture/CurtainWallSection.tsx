/** A curtain wall in the inspector: its type (and the configurator), its length along its axis, how many of its type there are. */

import { Boxes, LayoutGrid, Mountain, Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, relatedOf, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { arcLength, arcOf } from '../../viewport/arc';
import { lengthText } from '../../engine/unitsStore';
import { sameKind } from '../../shell/params';

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
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setCurtainWallType', { ids: sameKind(node), typeId: e.target.value })}>
            {ofClass(model, 'IfcCurtainWallType').map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Curtain walls…" onClick={() => useUi.setState({ dialog: 'curtainWalls', curtainTypeId: type?.id ?? null })}><Settings2 size={14} /></button>
        </span>
      </label>
      {node.editable && (
        <span className="row" style={{ gap: 6 }}>
          <button type="button" className="btn" data-action="curtain-profile" title="Its boundary drawn in elevation: a gable, a slope, steps; the grid, panels and mullions follow" onClick={() => useUi.setState({ dialog: 'wallProfile', selection: node.id })}>
            <Mountain size={14} />Profile…
          </button>
          <button type="button" className="btn" data-action="curtain-members" title="Chosen mullions and transoms with a profile of their own, turned, aligned or offset" onClick={() => useUi.setState({ dialog: 'curtainMembers', selection: node.id })}>
            <Boxes size={14} />Members…
          </button>
          <button type="button" className="btn" data-action="curtain-cells" title="Chosen cells with a panel type of their own, or a door in them" onClick={() => useUi.setState({ dialog: 'curtainCells', selection: node.id })}>
            <LayoutGrid size={14} />Panels and doors…
          </button>
          {Array.isArray(node.props.topProfile) && <button type="button" className="btn" title="Flat on top again" onClick={() => void execute('architecture.setCurtainWallProfile', { ids: [node.id], top: null })}>Flat top</button>}
        </span>
      )}
      <dl className="fields">
        <dt>Length</dt><dd className="num">{c ? lengthText(arcLength(c)) : '—'}</dd>
        <dt>Of its type</dt><dd className="num">{type ? relatedOf(model, type.id, 'IfcRelDefinesByType').length : 0}</dd>
        <dt>Doors</dt><dd className="num">{relatedOf(model, node.id, 'IfcRelAggregates').filter((d) => d.type === 'IfcDoor').length}</dd>
      </dl>
    </div>
  );
}
