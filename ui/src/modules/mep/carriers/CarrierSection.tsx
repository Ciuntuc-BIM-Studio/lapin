/**
 * A cable carrier in Properties: its type (the project's, or a built-in one made on choosing), its kind and size, the
 * cables in it and their fill against its limit; a size of its own kept as a type of the user's. Its elevation, width,
 * height and diameter are the class's fields.
 */

import { useEffect, useState } from 'react';
import { Save } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute, query, useSession } from '../../../engine/session';
import { relatingOf } from '../../../engine/model';
import { sameKind } from '../../../shell/params';
import { CLASS, carrierKinds, carrierPresets, carrierTypes, fillText, type CarrierFill } from './carriers';

export function CarrierSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const version = useSession((s) => s.version);
  const [fill, setFill] = useState<CarrierFill | null>(null);
  useEffect(() => {
    let live = true;
    void query<CarrierFill>('mep.carrierFill', { id: node.id }).then((r) => { if (live) setFill(r ?? null); });
    return () => { live = false; };
  }, [node.id, version]);
  if (node.type !== CLASS) return null;
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined;
  const kind = carrierKinds().find((k) => k.key === (type?.props.kind ?? node.props.kind));
  const types = carrierTypes(nodes);
  const own = ['width', 'height', 'diameter'].some((k) => node.props[k] != null);
  const choose = async (value: string) => {
    let typeId = value;
    if (value.startsWith('preset:')) {
      const made = await execute('mep.createCarrierType', { preset: value.slice(7) }) as { id?: string } | null;
      if (!made?.id) return;
      typeId = made.id;
    }
    await execute('mep.setCarrierType', { ids: sameKind(node), typeId });
  };
  // Its own size kept as a type of the user's, which it then takes.
  const saveType = async () => {
    const made = await execute('mep.createCarrierType', {
      kind: kind?.key ?? 'perforated',
      width: node.props.width ?? type?.props.width, height: node.props.height ?? type?.props.height, diameter: node.props.diameter ?? type?.props.diameter,
    }) as { id?: string } | null;
    if (made?.id) await execute('mep.setCarrierType', { ids: [node.id], typeId: made.id });
  };
  return (
    <div data-testid="carrier-section">
      <dl className="fields">
        <dt>Type</dt>
        <dd>
          <select value={type?.id ?? ''} data-field="carrier-type" disabled={!node.editable} onChange={(e) => void choose(e.target.value)}>
            {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}{t.props.indicative ? ' *' : ''}</option>)}
            <optgroup label="Built-in types (indicative)">
              {carrierPresets().filter((p) => !types.some((t) => t.props.preset === p.key)).map((p) => <option key={p.key} value={`preset:${p.key}`} data-no-i18n>{p.name}</option>)}
            </optgroup>
          </select>
        </dd>
        <dt>Kind</dt><dd>{kind?.label ?? '—'}</dd>
        <dt>Size</dt><dd data-no-i18n>{fill?.size ?? ''}</dd>
        <dt>Fill</dt>
        <dd data-testid="carrier-fill" className={fill?.over ? 'warn' : ''} title="The cables' cross-sections over the carrier's usable area, against its type's limit (indicative)">
          <span>{fillText(fill)}</span>{fill?.over ? <> · <strong>over the limit</strong></> : null}
        </dd>
      </dl>
      {own && (
        <div className="row pad" style={{ gap: 6 }}>
          <button type="button" className="btn small" data-action="carrier-save-type" disabled={!node.editable} onClick={() => void saveType()}><Save size={13} /> Save size as a type</button>
        </div>
      )}
      <p className="muted small">Built-in types are indicative: check them against the manufacturer's.</p>
    </div>
  );
}
