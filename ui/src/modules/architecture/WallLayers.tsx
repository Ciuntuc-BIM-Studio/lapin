/**
 * A wall in the inspector: its type (and the button to edit types), its
 * layers as its own usage lays them, its length, and Flip.
 */

import { FlipHorizontal2, Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialColor, materialName, mm, ofClass, typeLayers, typeOf, usageOf, wallLength } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

export function WallLayers({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const types = ofClass(model, 'IfcWallType');
  const layers = type ? typeLayers(model, type.id) : [];
  const usage = usageOf(model, node.id);
  const total = layers.reduce((s, l) => s + l.thickness, 0);
  // Layers are listed from the face right of the drawn direction; a flipped wall lays them from the left one.
  const shown = usage?.negative ? [...layers].reverse() : layers;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setWallType', { id: node.id, typeId: e.target.value })}>
            {!type && <option value="">(none)</option>}
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Wall types…" onClick={() => useUi.setState({ dialog: 'wallTypes' })}><Settings2 size={14} /></button>
        </span>
      </label>
      {layers.length > 0 && (
        <>
          <div className="layer-bar" title="Right face → left face">
            {shown.map((l, i) => <span key={i} style={{ flex: l.thickness, background: materialColor(model, l.material) }} title={materialName(model, l.material)} />)}
          </div>
          <table>
            <tbody>
              {shown.map((l, i) => (
                <tr key={i}>
                  <td><span className="swatch" style={{ background: materialColor(model, l.material) }} />{l.name ?? materialName(model, l.material)}{l.core && <span className="chip">core</span>}</td>
                  <td className="num">{mm(l.thickness)} mm</td>
                </tr>
              ))}
              <tr className="total"><td>Total</td><td className="num">{mm(total)} mm</td></tr>
            </tbody>
          </table>
        </>
      )}
      <dl className="fields">
        <dt>Length</dt><dd className="num">{mm(wallLength(node))} mm</dd>
        <dt>Layers run</dt><dd>{usage ? (usage.negative ? 'flipped' : 'as drawn') : '—'}</dd>
      </dl>
      {node.editable && (
        <button type="button" className="btn" title="Swap the wall's faces (its ends stay)" onClick={() => void execute('architecture.flipWall', { id: node.id })}>
          <FlipHorizontal2 size={14} />Flip
        </button>
      )}
    </div>
  );
}
