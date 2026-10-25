/** A terrain in the inspector: its points, its platforms and ditches, the brush strokes on it, and a new survey for it. */

import { FileUp } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { SURVEY, pickText } from './survey';

/** A terrain in the inspector: its points, and replacing them from a new survey. */
export function TerrainSection({ node }: { node: NodeDto }) {
  const count = Array.isArray(node.props.points) ? node.props.points.length : 0;
  const strokes = Array.isArray(node.props.sculpts) ? node.props.sculpts.length : 0;
  const relations = useSession((s) => s.relations);
  const pads = [...relations.values()].filter((r) => r.class === 'IfcRelVoidsElement' && r.relating === node.id).reduce((n, r) => n + r.related.length, 0);
  return (
    <dl className="fields">
      <dt>Points</dt><dd>{count}</dd>
      <dt>Platforms, ditches, paths</dt><dd>{pads}</dd>
      <dt>Brush strokes</dt>
      <dd>{strokes}{strokes > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id, last: true })}>undo last</button> · <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id })}>clear</button></>}</dd>
      <dt />
      <dd><button type="button" disabled={!node.editable} onClick={() => void pickText(SURVEY).then((f) => f && execute('civil.updateTerrain', { id: node.id, text: f.text }))}>
        <FileUp size={13} /> Replace the points…
      </button></dd>
    </dl>
  );
}
