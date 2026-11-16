/**
 * Properties section for paintable elements (walls, slabs, columns, beams): shows any Finish Painter
 * zones on the element and lets the user clear them per face or all at once (forum #124).
 */

import { Eraser } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { ZONE } from './finishPainter';

export function paintedZonesOf(nodes: ReadonlyMap<string, NodeDto>, hostId: string): NodeDto[] {
  const out: NodeDto[] = [];
  for (const n of nodes.values()) if (n.type === ZONE && n.props.hostId === hostId) out.push(n);
  return out;
}

export const hasPaintedFinishes = (node: NodeDto): boolean =>
  paintedZonesOf(useSession.getState().nodes, node.id).length > 0;

export function PaintedFinishesSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const zones = paintedZonesOf(nodes, node.id);
  if (zones.length === 0) return null;
  const faces = [...new Set(zones.map((z) => String(z.props.face ?? '')))].filter(Boolean);
  const clearFace = (face: string) => void execute('architecture.clearFinish', { hostId: node.id, face, all: true });
  const clearAll = async () => {
    for (const face of faces) await execute('architecture.clearFinish', { hostId: node.id, face, all: true });
  };
  return (
    <div className="painted-finishes" data-testid="painted-finishes-section">
      <ul className="part-list">
        {zones.map((z) => (
          <li key={z.id} className="row between small">
            <span data-no-i18n>{String(z.props.face ?? '')}</span>
            <span className="muted" data-no-i18n>{z.props.bare ? 'bare' : z.name}</span>
          </li>
        ))}
      </ul>
      <div className="row wrap" style={{ marginTop: 6 }}>
        {faces.length === 1
          ? <button type="button" className="btn small" onClick={() => clearFace(faces[0])}><Eraser size={12} />Clear painted finishes</button>
          : <button type="button" className="btn small" onClick={() => void clearAll()}><Eraser size={12} />Clear all ({faces.length} faces)</button>}
      </div>
    </div>
  );
}
