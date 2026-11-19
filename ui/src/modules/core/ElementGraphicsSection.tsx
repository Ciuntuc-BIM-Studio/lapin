/** The properties panel's Graphics in view row (decision 718, issue #88): whether the element is drawn otherwise in the view in front, and the dialog that sets it. */

import { Paintbrush, RotateCcw } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { elementGraphicsArgs, frontViewIdNow, hasOwnGraphics } from './elementGraphics';

export function ElementGraphicsSection({ node }: { node: NodeDto }) {
  const view = useUi((s) => s.view);
  const planViewId = useUi((s) => s.planViewId);
  const drawingViewId = useUi((s) => s.drawingViewId);
  const view3dId = useUi((s) => s.view3dId);
  const selected = useUi((s) => s.selected);
  const viewId = frontViewIdNow({ view, planViewId, drawingViewId, view3dId });
  const viewNode = useSession((s) => (viewId ? s.nodes.get(viewId) : undefined));
  const own = hasOwnGraphics(viewNode, node.id);
  const ids = selected.includes(node.id) ? selected : [node.id];
  return (
    <div className="pad" data-testid="element-graphics">
      <p className="muted small">{own ? 'Drawn with graphics of its own in this view.' : 'Drawn as its material and the view\'s Visibility / Graphics say.'}</p>
      <div className="row">
        <button type="button" className="btn" data-action="element-graphics" title="Its hatch, fill and lines in the view in front, over its material's (Revit's Override Graphics in View)"
          onClick={() => useUi.setState({ dialog: 'elementGraphics' })}><Paintbrush size={14} />Graphics in this view…</button>
        {own && viewId && <button type="button" className="icon" title="Back to its material's and the view's graphics" aria-label="Reset its graphics in this view"
          onClick={() => void execute('view.elementGraphics', elementGraphicsArgs(viewId, ids, null, 'view'))}><RotateCcw size={13} /></button>}
      </div>
    </div>
  );
}
