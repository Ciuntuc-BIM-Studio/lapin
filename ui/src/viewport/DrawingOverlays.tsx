/**
 * What the modules draw inside a section's or an elevation's SVG (LapinModule.drawingOverlays, decision 511), in the
 * drawing's own coordinates; clicked as plan overlays are: what carries `data-overlay-hit` keeps its press to itself.
 */

import { drawingOverlays } from '../platform/modules';
import type { DrawingOverlayProps } from '../platform/module';
import { onOverlayHit } from './PlanOverlays';

export function DrawingOverlays(props: DrawingOverlayProps) {
  const list = drawingOverlays();
  if (!list.length) return null;
  const keep = (e: React.SyntheticEvent) => { if (onOverlayHit(e.target)) e.stopPropagation(); };
  return (
    <g className="drawing-overlays" style={{ pointerEvents: 'none' }} onPointerDown={keep} onPointerUp={keep} onDoubleClick={keep} onContextMenu={keep}>
      {list.map((Overlay, k) => <Overlay key={k} {...props} />)}
    </g>
  );
}
