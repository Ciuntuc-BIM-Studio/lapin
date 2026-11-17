/** An image's or an imported PDF's properties: Scale to a known distance… (forum 143), the tool started on it. */

import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { scalableTrace, startScaleImage } from './scaleImage';

export const scaleImageShows = (node: NodeDto): boolean => {
  const { nodes, relations } = useSession.getState();
  return scalableTrace(node.id, nodes, relations);
};

export function ScaleImageSection({ node }: { node: NodeDto }) {
  return (
    <div className="fields">
      <button type="button" className="btn" data-action="scale-image" disabled={!node.editable}
        title="Two points on it a known distance apart, then their true distance: it grows or shrinks about the first"
        onClick={() => startScaleImage(node.id)}>Scale to a known distance…</button>
    </div>
  );
}
