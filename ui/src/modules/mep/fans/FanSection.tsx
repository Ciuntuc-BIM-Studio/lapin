/**
 * An exhaust fan in Properties: its type (of its mounting), its kind and what it is, the way to a duct out of a ceiling
 * or inline fan, and its type's dialog. Its height, turn and outlet are the class's fields, shown as they apply.
 */

import { Route, Settings2 } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute, useSession } from '../../../engine/session';
import { relatingOf } from '../../../engine/model';
import { activate } from '../../../platform/modules';
import { CLASS, TYPE_CLASS, fanSummary, kindOf, specOf } from './fans';
import { editFanType, useFanCatalogs } from './FanTypesDialog';
import { fanDuct } from './tools';
import { sameKind } from '../../../shell/params';

const upright = (m: unknown) => m === 'wall' || m === 'window';

export function FanSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const { kinds } = useFanCatalogs();
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined;
  const kind = kinds.find((k) => k.key === kindOf(type));
  const mounting = node.props.mounting;
  // The types a fan may take: those mounted as it is (a wall fan, a wall or window type).
  const others = [...nodes.values()].filter((n) => n.type === TYPE_CLASS && upright(specOf(n).mounting ?? kinds.find((k) => k.key === kindOf(n))?.mountings[0]) === upright(mounting))
    .sort((a, b) => a.name.localeCompare(b.name));
  if (node.type !== CLASS) return null;
  return (
    <div data-testid="fan-section" data-i18n-ctx="fans">
      <dl className="fields">
        <dt>Type</dt>
        <dd>
          <select value={type?.id ?? ''} data-field="fan-type" onChange={(e) => void execute('mep.setFanType', { ids: sameKind(node), typeId: e.target.value })}>
            {others.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
        </dd>
        <dt>Kind</dt><dd>{kind?.label ?? '—'}</dd>
        <dt>Size</dt><dd className="small" data-no-i18n>{fanSummary(type, kinds)}</dd>
      </dl>
      <div className="row pad" style={{ gap: 6 }}>
        {!upright(mounting) && (
          <button type="button" className="btn small" data-action="fan-duct" title="A duct from the fan to an outlet in a wall or through the roof: click where it goes out" onClick={() => activate(fanDuct)}>
            <Route size={13} /> Duct to outlet…
          </button>
        )}
        <button type="button" className="btn small" data-action="fan-edit-type" onClick={() => editFanType(type)}><Settings2 size={13} /> Edit type…</button>
      </div>
    </div>
  );
}
