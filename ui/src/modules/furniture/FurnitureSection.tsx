/**
 * A piece of furniture in Properties: its type (of its kind, or another), its turn, its height above the floor, a
 * kitchen's length, Flip (its other hand, in place) and the way to its type's dialog.
 */

import { FlipHorizontal2, Settings2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { relatingOf } from '../../engine/model';
import { CLASS, TYPE_CLASS, kindOf, sizeOf } from './furniture';
import { editType, useKinds } from './FurnitureTypesDialog';

export function FurnitureSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const kinds = useKinds();
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined;
  const typeId = type?.id;
  const kind = kindOf(type);
  const others = [...nodes.values()].filter((n) => n.type === TYPE_CLASS).sort((a, b) => (kindOf(a) === kind ? 0 : 1) - (kindOf(b) === kind ? 0 : 1) || a.name.localeCompare(b.name));
  const update = (attrs: Record<string, unknown>) => void execute('element.update', { ids: [node.id], attrs });
  if (node.type !== CLASS) return null;
  return (
    <div data-testid="furniture-section" data-i18n-ctx="furniture">
      <dl className="fields">
        <dt>Type</dt>
        <dd>
          <select value={typeId ?? ''} data-field="furniture-type" onChange={(e) => void execute('furniture.setType', { ids: [node.id], typeId: e.target.value })}>
            {others.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
        </dd>
        <dt>Kind</dt><dd>{kinds.find((k) => k.key === kind)?.label ?? '—'}</dd>
        <dt>Size</dt><dd className="small" data-no-i18n>{sizeOf(type, kinds)}</dd>
        <dt>Rotation</dt><dd><NumberField value={Number(node.props.rotation ?? 0)} suffix="°" onCommit={(v) => { if (v != null) update({ rotation: ((v % 360) + 360) % 360 }); }} /></dd>
        <dt title="Raised above its storey's floor (on a podium, a wall unit hung higher)">Above the floor</dt>
        <dd><LengthField metres={Number(node.props.offset ?? 0)} onCommit={(v) => update({ offset: v ?? 0 })} /></dd>
        {kind === 'kitchen' && <><dt>Length</dt><dd><LengthField testId="kitchen-length" metres={Number(node.props.length ?? 3)} min={0.6} max={12} onCommit={(v) => { if (v != null) update({ length: v }); }} /></dd></>}
      </dl>
      <div className="row pad" style={{ gap: 6 }}>
        <button type="button" className="btn small" data-action="furniture-flip" title="Its other hand, where it stands: a chaise on the other side, the fridge at the other end" onClick={() => void execute('furniture.flip', { ids: [node.id] })}>
          <FlipHorizontal2 size={13} /> Flip
        </button>
        <button type="button" className="btn small" data-action="furniture-edit-type" onClick={() => editType(type)}><Settings2 size={13} /> Edit type…</button>
      </div>
    </div>
  );
}
