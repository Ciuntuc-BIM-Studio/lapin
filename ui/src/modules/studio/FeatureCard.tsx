/** A Studio solid in the inspector. */

import { useEffect, useState } from 'react';
import { PenTool } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { query, useSession } from '../../engine/session';
import { editSketch, type Feature } from './sketch';

/** A Studio solid in the inspector: its operation, sketch, net face, guide; Edit sketch. */
export function FeatureCard({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [f, setF] = useState<Feature | null>(null);
  useEffect(() => { void query<Feature>('studio.feature', { id: node.id }).then(setF); }, [node.id, nodes]);
  if (!f) return null;
  const op = f.operation[0].toUpperCase() + f.operation.slice(1);
  return (
    <dl className="fields">
      <dt>Studio</dt><dd>{op}</dd>
      <dt>Sketch</dt><dd>{f.view ?? '—'} · outline, {f.holes} hole(s)</dd>
      {f.area != null && <><dt>Face</dt><dd>{f.area.toFixed(2)} m² net</dd></>}
      {f.guide != null && <><dt>{f.operation === 'sweep' ? 'Path' : 'Axis'}</dt><dd className={f.guide === 'missing' ? 'error-text' : ''}>{f.guide === 'missing' ? 'missing: redraw it to rebuild' : f.guide}</dd></>}
      <dt />
      <dd><button type="button" onClick={() => void editSketch(node.id)}><PenTool size={13} /> Edit sketch</button></dd>
      <dd className="muted small">Every step stays editable: change the sketch, its holes or the path, and the solid follows.</dd>
    </dl>
  );
}
