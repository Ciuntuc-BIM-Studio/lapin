/**
 * A gate in Properties: its type (its style), its kind and size, the flips (the leaves to the other side, the hinges or
 * parking on the other hand) and its type's configurator. Its turn, height, swing, hand and drawn opening are the class's
 * fields, shown as its kind takes them.
 */

import { FlipHorizontal2, FlipVertical2, Settings2 } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute, useSession } from '../../../engine/session';
import { relatingOf } from '../../../engine/model';
import { CLASS, TYPE_CLASS, gateSummary, kindOf } from './gates';
import { editGateType, useGateCatalogs } from './GateTypesDialog';
import { sameKind } from '../../../shell/params';

export function GateSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const { kinds } = useGateCatalogs();
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined;
  const kind = kinds.find((k) => k.key === kindOf(type));
  const others = [...nodes.values()].filter((n) => n.type === TYPE_CLASS).sort((a, b) => (kindOf(a) === kind?.key ? 0 : 1) - (kindOf(b) === kind?.key ? 0 : 1) || a.name.localeCompare(b.name));
  if (node.type !== CLASS) return null;
  return (
    <div data-testid="gate-section" data-i18n-ctx="gates">
      <dl className="fields">
        <dt>Type</dt>
        <dd>
          <select value={type?.id ?? ''} data-field="gate-type" onChange={(e) => void execute('architecture.setGateType', { ids: sameKind(node), typeId: e.target.value })}>
            {others.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
        </dd>
        <dt>Kind</dt><dd>{kind?.label ?? '—'}</dd>
        <dt>Size</dt><dd className="small" data-no-i18n>{gateSummary(type, kinds)}</dd>
      </dl>
      <div className="row pad" style={{ gap: 6, flexWrap: 'wrap' }}>
        <button type="button" className="btn small" data-action="gate-flip-swing" title="The leaves open to the other side (a sliding gate runs on the other face)" onClick={() => void execute('architecture.flipGate', { ids: sameKind(node), axis: 'swing' })}>
          <FlipVertical2 size={13} /> Other side
        </button>
        <button type="button" className="btn small" data-action="gate-flip-hand" title="The hinges, or where it slides to, on the other hand" onClick={() => void execute('architecture.flipGate', { ids: sameKind(node), axis: 'hand' })}>
          <FlipHorizontal2 size={13} /> Other hand
        </button>
        <button type="button" className="btn small" data-action="gate-edit-type" onClick={() => editGateType(type)}><Settings2 size={13} /> Edit type…</button>
      </div>
    </div>
  );
}
