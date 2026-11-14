/**
 * A wall's or floor's layer extents in the inspector (forum topic 89, docs/plan-layer-extents.md): each a row — the layer, the
 * edge, how far past it (or short of it, negative) and whether the piece past the edge cuts the walls and floors it meets —
 * set at once, one undo step each (architecture.setLayerExtent); × takes one away.
 */

import { Plus, X } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import { materialName, typeLayers, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { LengthField } from '../../shell/cells';
import { EDGE, OUTLINE, WALL_EDGES, changeSteps, extentsOf, newExtent, setArgs, type LayerExtent } from './layerExtents';

const OFFSET_TIP = 'How far the layer goes past this edge (positive) or stops short of it (negative)';
const CUTS_TIP = 'Whether the layer past the edge cuts the walls and floors it meets; off, it overlaps them';
const WALL_EDGE_LABEL: Record<string, string> = { start: 'Start', end: 'End', bottom: 'Bottom', top: 'Top' };

/** Whether the section shows: a straight wall or a floor of Lapin's own (a reference keeps its file's body). */
export const hasLayerExtents = (node: NodeDto) =>
  !node.props.reference && (node.type === 'IfcSlab' || (node.type === 'IfcWall' && !node.props.arc));

export function LayerExtentsSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const layers = type ? typeLayers(model, type.id) : [];
  if (layers.length === 0) return null;
  const wall = node.type === 'IfcWall';
  const all = extentsOf(node.props);
  const edges = Array.isArray(node.props.contour) ? node.props.contour.length : 0;
  const run = (steps: { command: string; args: Record<string, unknown> }[]) => {
    if (steps.length === 1) void execute(steps[0].command, steps[0].args);
    else if (steps.length > 1) void execute('edit.batch', { steps });
  };
  const change = (was: LayerExtent, next: LayerExtent) => run(changeSteps(node.id, all, was, next));
  const edgeValue = (x: LayerExtent) => (x.edge === EDGE ? `${EDGE}:${x.index ?? 0}` : x.edge);
  const fromEdge = (x: LayerExtent, v: string): LayerExtent => {
    if (v.startsWith(`${EDGE}:`)) return { ...x, edge: EDGE, index: Number(v.slice(EDGE.length + 1)) };
    const { index: _drop, ...rest } = x;
    return { ...rest, edge: v };
  };
  const fresh = newExtent(wall, layers, all);
  return (
    <div className="layer-extents" data-testid="layer-extents">
      {all.length === 0
        ? <p className="muted small">Every layer ends at the element's edges.</p>
        : all.map((x) => (
          <div key={`${x.layer}/${edgeValue(x)}`} className="layer-extent" data-testid="layer-extent" style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1fr) auto auto', gap: 4, alignItems: 'center', marginTop: 6 }}>
            <select style={{ gridColumn: '1 / -1', minWidth: 0 }} value={x.layer} disabled={!node.editable} title="Layer" onChange={(e) => change(x, { ...x, layer: Number(e.target.value) })}>
              {layers.map((l, i) => l.thickness > 0 && <option key={i} value={i}>{i + 1} · {l.name || materialName(model, l.material)}</option>)}
            </select>
            <select style={{ minWidth: 0 }} value={edgeValue(x)} disabled={!node.editable} title={wall ? 'Edge' : 'Which edge of the outline, numbered from its first corner'} onChange={(e) => change(x, fromEdge(x, e.target.value))}>
              {wall
                ? WALL_EDGES.map((k) => <option key={k} value={k}>{WALL_EDGE_LABEL[k]}</option>)
                : <>
                    <option value={OUTLINE}>All round</option>
                    {Array.from({ length: edges }, (_, i) => <option key={i} value={`${EDGE}:${i}`}>{i + 1}</option>)}
                  </>}
            </select>
            <LengthField metres={x.offset} title={OFFSET_TIP} testId="layer-extent-offset" style={{ minWidth: 0 }} onCommit={(m) => { if (m != null) change(x, { ...x, offset: m }); }} />
            <label className="row small" title={CUTS_TIP}><input type="checkbox" checked={x.cuts === true} disabled={!node.editable} data-testid="layer-extent-cuts" onChange={(e) => change(x, { ...x, cuts: e.target.checked })} /> Cuts others</label>
            <button type="button" className="icon" title="Take the extent away" disabled={!node.editable} onClick={() => void execute('architecture.setLayerExtent', setArgs(node.id, { ...x, offset: 0 }))}><X size={12} /></button>
          </div>
        ))}
      {fresh && node.editable && (
        <button type="button" className="small" data-testid="layer-extent-add" onClick={() => void execute('architecture.setLayerExtent', setArgs(node.id, fresh))}>
          <Plus size={12} /> Add a layer extent
        </button>
      )}
    </div>
  );
}
