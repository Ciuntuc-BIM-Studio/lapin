/** The properties panel's hatch origin row: u and v of where the element's hatches start, and the way back (docs/plan-material-graphics.md, stage 1). */

import { RotateCcw } from 'lucide-react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { LengthCell } from '../../shell/cells';
import { hatchOriginOf, setOriginArgs, withCoordinate } from './hatchOrigin';

export function HatchOriginSection({ node }: { node: NodeDto }) {
  const origin = hatchOriginOf(node);
  const selected = useUi((s) => s.selected);
  const ids = selected.includes(node.id) ? selected : [node.id];
  const send = (o: [number, number] | null) => void execute('element.setHatchOrigin', setOriginArgs(ids, o));
  return (
    <dl className="fields" data-testid="hatch-origin">
      <dt title="Where the element's hatches start, in its own frame: along its axis from the start, and up from its base in elevations and sections (to the left of the axis in a plan). Real-size patterns (bricks, tiles) start their courses here">Hatch origin, along (u)</dt>
      <dd><span data-field="hatch-origin-u"><LengthCell metres={origin?.[0]} placeholder="automatic" onCommit={(v) => send(withCoordinate(origin, 0, v))} /></span></dd>
      <dt title="Up from the element's base in elevations and sections; to the left of the axis in a plan">Hatch origin, up (v)</dt>
      <dd>
        <span data-field="hatch-origin-v"><LengthCell metres={origin?.[1]} placeholder="automatic" onCommit={(v) => send(withCoordinate(origin, 1, v))} /></span>
        {origin ? <button type="button" className="icon mini" data-action="hatch-origin-reset" title="Back to automatic: the view's origin, or the element's base for a real-size pattern" aria-label="Reset the hatch origin" onClick={() => send(null)}><RotateCcw size={11} /></button> : <span className="muted small"> automatic</span>}
      </dd>
    </dl>
  );
}
