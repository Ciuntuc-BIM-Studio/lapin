/** A column, beam or footing in the inspector: its type (and the structure types dialog), what it is made of; a pad footing's level. */

import { useEffect, useState } from 'react';
import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { describeType } from './describe';
import { sameKind } from '../../shell/params';
import { LengthField } from '../../shell/cells';
import type { PadLevel } from './padLevel';

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
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('structure.setType', { ids: sameKind(node), typeId: e.target.value })}>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Structure types…" onClick={() => useUi.setState({ dialog: 'structureTypes' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <p className="muted small">{describeType(type)}</p>
      {pad && <PadLevelFields node={node} />}
    </div>
  );
}

/**
 * A pad footing's level (decision 507): its top and bottom as absolute elevations, what decides them (the column it follows,
 * its own top, its level), its own top to type (which unlinks it) and the way back to the column's base.
 */
function PadLevelFields({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [level, setLevel] = useState<PadLevel | null>(null);
  useEffect(() => { let live = true; void query<PadLevel>('structure.padLevel', { id: node.id }).then((r) => { if (live) setLevel(r); }); return () => { live = false; }; }, [node.id, nodes]);
  if (!level) return null;
  const pads = () => sameKind(node).filter((id) => nodes.get(id)?.props.predefinedType === 'PAD_FOOTING');
  const editable = node.editable !== false;
  return (
    <div className="pad-level" data-testid="pad-level">
      <span className="eyebrow">Level</span>
      <p className="muted small" data-testid="pad-level-rule">
        {level.rule === 'column' && level.column
          ? <><span>At the base of the column</span> <span data-no-i18n>„{level.column.name}”{level.column.storey ? ` · ${level.column.storey}` : ''}</span></>
          : level.rule === 'own' ? <span>Its own top: it does not follow a column.</span>
            : level.follows ? <span>No column over it: its type's bottom under its level.</span>
              : <span>Where its IFC file puts it.</span>}
      </p>
      <dl className="fields">
        <dt title="An elevation of its own for its top: it no longer follows the column">Top</dt>
        <dd><LengthField metres={level.top} readOnly={!editable} testId="pad-top"
          onCommit={(m) => { if (m != null) void execute('structure.setPadLevel', { ids: pads(), top: m }); }} /></dd>
        <dt>Bottom</dt>
        <dd><LengthField metres={level.bottom} readOnly testId="pad-bottom" onCommit={() => undefined} /></dd>
      </dl>
      {editable && (level.rule === 'own' || !level.follows) && (
        <button type="button" className="btn" data-testid="pad-follow" onClick={() => void execute('structure.setPadLevel', { ids: pads(), follow: true })}>At the column's base</button>
      )}
    </div>
  );
}
