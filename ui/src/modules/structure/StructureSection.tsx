/** A column, beam or footing in the inspector: its type (and the structure types dialog), what it is made of. */

import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { describeType } from './describe';

const TYPE_CLASS: Record<string, string> = { IfcColumn: 'IfcColumnType', IfcBeam: 'IfcBeamType', IfcFooting: 'IfcFootingType' };

export function StructureSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const pad = node.props.predefinedType === 'PAD_FOOTING';
  const types = ofClass(model, TYPE_CLASS[node.type] ?? '').filter((t) => node.type !== 'IfcFooting' || (t.props.kind === 'pad') === pad);
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('structure.setType', { id: node.id, typeId: e.target.value })}>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Structure types…" onClick={() => useUi.setState({ dialog: 'structureTypes' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <p className="muted small">{describeType(type)}</p>
    </div>
  );
}
