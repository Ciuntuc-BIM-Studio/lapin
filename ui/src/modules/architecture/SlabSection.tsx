/** A floor in the inspector: its type (and the floor types dialog), its build-up from the top down, its area. */

import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialColor, materialName, ofClass, typeLayers, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { lengthText } from '../../engine/unitsStore';
import { sameKind } from '../../shell/params';
import { beamBlockOf, spanEdges } from './beamBlock';

export function SlabSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const layers = type ? typeLayers(model, type.id) : [];
  const bb = beamBlockOf(type?.props.beamBlock);
  const contour = (node.props.contour as [number, number][] | undefined) ?? [];
  const span = typeof node.props.spanAngle === 'number' ? node.props.spanAngle : null;
  const edges = spanEdges(contour);
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setSlabType', { ids: sameKind(node), typeId: e.target.value })}>
            {ofClass(model, 'IfcSlabType').map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Floor types…" onClick={() => useUi.setState({ dialog: 'slabTypes' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <table>
        <tbody>
          {layers.map((l, i) => (
            <tr key={i}><td><span className="swatch" style={{ background: materialColor(model, l.material) }} />{l.name ?? materialName(model, l.material)}{l.core && <span className="chip">core</span>}</td><td className="num">{lengthText(l.thickness)}</td></tr>
          ))}
          <tr className="total"><td>Total</td><td className="num">{lengthText(layers.reduce((s, l) => s + l.thickness, 0))}</td></tr>
        </tbody>
      </table>
      {bb && (
        <label className="field-line" data-field="span" title="The joists' way in this floor: the type's, or across the edge they bear on">
          <span className="eyebrow">Span direction</span>
          <select value={span == null ? '' : String(span)} disabled={!node.editable} data-testid="floor-span"
            onChange={(e) => {
              const v = e.target.value;
              if (v === '') void execute('architecture.setFloorSpan', { ids: sameKind(node) });
              else if (v.startsWith('edge:')) void execute('architecture.setFloorSpan', { ids: [node.id], edge: Number(v.slice(5)) });
              else void execute('architecture.setFloorSpan', { ids: sameKind(node), angle: Number(v) });
            }}>
            <option value="">The type's</option>
            {span != null && <option value={String(span)}>{`${Math.round(span * 10) / 10}°`}</option>}
            {edges.map((e) => <option key={e.index} value={`edge:${e.index}`}>{`Across edge ${e.index + 1} (${lengthText(e.length)})`}</option>)}
          </select>
        </label>
      )}
    </div>
  );
}
