/**
 * A wall's or floor's layer extents in the inspector (forum topic 89, docs/plan-layer-extents.md): each a row — the layer, the
 * edge, how far past it (or short of it, negative) and what the piece past the edge does where it meets another element
 * (overlaps it, cuts it, or cuts it only when its element outranks that one by the cut order) — set at once, one undo step
 * each (architecture.setLayerExtent); × takes one away. A wall type or a floor type holds the extents its elements start
 * with: an element shows its type's rows (changed there, they become its own), and its own over its type's give the place
 * back to the type's. "Edit layers" drags the edges in plans and sections instead.
 */

import { Layers, Plus, RotateCcw, X } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import { materialName, typeLayers, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { LengthField } from '../../shell/cells';
import { EDGE, OUTLINE, WALL_EDGES, changeSteps, extentRows, extentsOf, newExtent, removeArgs, sameKey, setArgs, type LayerExtent } from './layerExtents';
import { openLayerEdit } from './layerEdit';

const OFFSET_TIP = 'How far the layer goes past this edge (positive) or stops short of it (negative)';
const CUTS_TIP = 'What the layer past the edge does where it meets another element: overlaps it, cuts it, or cuts it only where its element comes first in the cut order';
const WALL_EDGE_LABEL: Record<string, string> = { start: 'Start', end: 'End', bottom: 'Bottom', top: 'Top' };
const TYPES = new Set(['IfcWallType', 'IfcSlabType']);

/** Whether the section shows: a wall or a floor of Lapin's own (a reference keeps its file's body), or their types. */
export const hasLayerExtents = (node: NodeDto) =>
  TYPES.has(node.type) || (!node.props.reference && (node.type === 'IfcSlab' || node.type === 'IfcWall'));

const cutsValue = (x: LayerExtent) => (x.cuts === 'order' ? 'order' : x.cuts ? 'on' : 'off');
const cutsOf = (v: string): LayerExtent['cuts'] => (v === 'order' ? 'order' : v === 'on');

export function LayerExtentsSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const isType = TYPES.has(node.type);
  const type = isType ? null : typeOf(model, node.id);
  const layers = typeLayers(model, isType ? node.id : type?.id ?? '');
  if (layers.length === 0) return null;
  const wall = node.type === 'IfcWall' || node.type === 'IfcWallType';
  const own = extentsOf(node.props);
  const fromType = type ? extentsOf(type.props) : [];
  const rows = extentRows(own, fromType);
  const typeHas = (x: LayerExtent) => fromType.some((t) => sameKey(t, x));
  const edges = Array.isArray(node.props.contour) ? node.props.contour.length : 0;
  const run = (steps: { command: string; args: Record<string, unknown> }[]) => {
    if (steps.length === 1) void execute(steps[0].command, steps[0].args);
    else if (steps.length > 1) void execute('edit.batch', { steps });
  };
  const change = (was: LayerExtent, next: LayerExtent) => run(changeSteps(node.id, own, was, next, typeHas));
  const edgeValue = (x: LayerExtent) => (x.edge === EDGE ? `${EDGE}:${x.index ?? 0}` : x.edge);
  const fromEdge = (x: LayerExtent, v: string): LayerExtent => {
    if (v.startsWith(`${EDGE}:`)) return { ...x, edge: EDGE, index: Number(v.slice(EDGE.length + 1)) };
    const { index: _drop, ...rest } = x;
    return { ...rest, edge: v };
  };
  const fresh = newExtent(wall, layers, rows.map((r) => r.x));
  return (
    <div className="layer-extents" data-testid="layer-extents">
      {isType && <p className="muted small">{wall ? 'Every wall of this type starts with these; its own extent at the same place wins.' : 'Every floor of this type starts with these; its own extent at the same place wins.'}</p>}
      {rows.length === 0
        ? <p className="muted small">Every layer ends at the element's edges.</p>
        : rows.map(({ x, fromType: inherited, typeOffset }) => (
          <div key={`${x.layer}/${edgeValue(x)}`} className="layer-extent" data-testid="layer-extent" data-from-type={inherited || undefined} style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1fr) auto auto', gap: 4, alignItems: 'center', marginTop: 6 }}>
            <select style={{ gridColumn: '1 / -1', minWidth: 0 }} value={x.layer} disabled={!node.editable || inherited} title="Layer" onChange={(e) => change(x, { ...x, layer: Number(e.target.value) })}>
              {layers.map((l, i) => l.thickness > 0 && <option key={i} value={i}>{i + 1} · {l.name || materialName(model, l.material)}</option>)}
            </select>
            <select style={{ minWidth: 0 }} value={edgeValue(x)} disabled={!node.editable || inherited} title={wall ? 'Edge' : 'Which edge of the outline, numbered from its first corner'} onChange={(e) => change(x, fromEdge(x, e.target.value))}>
              {wall
                ? WALL_EDGES.map((k) => <option key={k} value={k}>{WALL_EDGE_LABEL[k]}</option>)
                : <>
                    <option value={OUTLINE}>All round</option>
                    {!isType && Array.from({ length: edges }, (_, i) => <option key={i} value={`${EDGE}:${i}`}>{i + 1}</option>)}
                  </>}
            </select>
            <LengthField metres={x.offset} title={OFFSET_TIP} testId="layer-extent-offset" style={{ minWidth: 0 }} onCommit={(m) => { if (m != null) change(x, { ...x, offset: m }); }} />
            <select value={cutsValue(x)} disabled={!node.editable} title={CUTS_TIP} data-testid="layer-extent-cuts" style={{ minWidth: 0 }} onChange={(e) => change(x, { ...x, cuts: cutsOf(e.target.value) })}>
              <option value="off">Overlaps</option>
              <option value="on">Cuts others</option>
              <option value="order">By cut order</option>
            </select>
            {(inherited || typeOffset != null) && (
              <span className="muted small" style={{ gridColumn: '1 / 3' }}>{inherited ? 'From the type' : <>The type's: {Math.round(typeOffset! * 10000) / 10} mm</>}</span>
            )}
            {typeOffset != null && !inherited
              ? <button type="button" className="icon" title="Back to the type's" disabled={!node.editable} onClick={() => void execute('architecture.setLayerExtent', removeArgs(node.id, x, true))}><RotateCcw size={12} /></button>
              : <button type="button" className="icon" title={inherited ? 'Keep this layer at the edge' : 'Take the extent away'} disabled={!node.editable} onClick={() => void execute('architecture.setLayerExtent', inherited ? setArgs(node.id, { ...x, offset: 0 }) : removeArgs(node.id, x, false))}><X size={12} /></button>}
          </div>
        ))}
      <div className="row" style={{ gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
        {fresh && node.editable && (
          <button type="button" className="small" data-testid="layer-extent-add" onClick={() => void execute('architecture.setLayerExtent', setArgs(node.id, fresh))}>
            <Plus size={12} /> Add a layer extent
          </button>
        )}
        {!isType && node.editable && (
          <button type="button" className="small" data-testid="layer-extent-edit" title="Drag each layer's edges in plans and sections" onClick={() => openLayerEdit()}>
            <Layers size={12} /> Edit layers
          </button>
        )}
      </div>
    </div>
  );
}
