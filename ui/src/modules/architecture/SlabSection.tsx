/** A floor in the inspector: its type (and the floor types dialog), its build-up from the top down, its area. */

import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialColor, materialName, mm, ofClass, typeLayers, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

export function SlabSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const layers = type ? typeLayers(model, type.id) : [];
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setSlabType', { id: node.id, typeId: e.target.value })}>
            {ofClass(model, 'IfcSlabType').map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Floor types…" onClick={() => useUi.setState({ dialog: 'slabTypes' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <table>
        <tbody>
          {layers.map((l, i) => (
            <tr key={i}><td><span className="swatch" style={{ background: materialColor(model, l.material) }} />{l.name ?? materialName(model, l.material)}{l.core && <span className="chip">core</span>}</td><td className="num">{mm(l.thickness)} mm</td></tr>
          ))}
          <tr className="total"><td>Total</td><td className="num">{mm(layers.reduce((s, l) => s + l.thickness, 0))} mm</td></tr>
        </tbody>
      </table>
    </div>
  );
}
