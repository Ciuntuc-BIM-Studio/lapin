/**
 * What the modules draw inside a plan's SVG (LapinModule.planOverlays), in the plan's own coordinates. The layer takes no
 * pointer by itself; what a module wants clicked carries `data-overlay-hit`, and a press on it stays there, so it starts
 * neither a selection, a grip nor a tool in the plan underneath.
 */

import { planOverlays } from '../platform/modules';
import type { PlanOverlayProps } from '../platform/module';

/** Whether a press on `target` is on something an overlay made clickable. */
export const onOverlayHit = (target: EventTarget | null): boolean => target instanceof Element && target.closest('[data-overlay-hit]') !== null;

export function PlanOverlays(props: PlanOverlayProps) {
  const list = planOverlays();
  if (!list.length) return null;
  const keep = (e: React.SyntheticEvent) => { if (onOverlayHit(e.target)) e.stopPropagation(); };
  return (
    <g className="plan-overlays" style={{ pointerEvents: 'none' }} onPointerDown={keep} onPointerUp={keep} onDoubleClick={keep} onContextMenu={keep}>
      {list.map((Overlay, k) => <Overlay key={k} {...props} />)}
    </g>
  );
}
