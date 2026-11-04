/**
 * One grid axis in the properties panel (report #68): its ends, length and
 * angle, its spacing from the parallel axis before it (the axes beyond move
 * with it, or not), in the project's units; and the way to the grid's
 * settings: the Grid dialog (a whole grid from its spacings) and the axis
 * types of the drafting standards (bubbles, size, line, first label).
 */

import { useState } from 'react';
import { Grid3x3, Settings2 } from 'lucide-react';
import { LengthField, NumberField } from './cells';
import { execute, useSession } from '../engine/session';
import { useUi } from '../app/store';
import { runSteps } from '../viewport/grips';
import { openStandards } from '../modules/core/standardsTab';
import { angleOf, axesOf, lengthOf, setSpacing, spacingOf } from './gridAxes';

export function AxisFields({ gridId, tag }: { gridId: string; tag: string }) {
  const grid = useSession((s) => s.nodes.get(gridId));
  const [beyond, setBeyond] = useState(true);
  const axes = axesOf(grid?.props.axes);
  const axis = axes.find((x) => x.tag === tag);
  if (!axis) return null;
  const move = (a: { x: number; y: number }, b: { x: number; y: number }) => void execute('drafting.updateAxis', { gridId, tag, x1: a.x, y1: a.y, x2: b.x, y2: b.y });
  const gap = spacingOf(axes, tag);
  // A new angle: the axis turned about its start.
  const turn = (deg: number) => {
    const l = lengthOf(axis), r = deg * Math.PI / 180;
    move(axis.a, { x: axis.a.x + Math.cos(r) * l, y: axis.a.y + Math.sin(r) * l });
  };
  return (
    <>
      <dl className="fields pad" data-axis-fields>
        <dt>Start</dt>
        <dd className="pair"><LengthField noUnit metres={axis.a.x} onCommit={(x) => { if (x != null) move({ x, y: axis.a.y }, axis.b); }} /><LengthField metres={axis.a.y} onCommit={(y) => { if (y != null) move({ x: axis.a.x, y }, axis.b); }} /></dd>
        <dt>End</dt>
        <dd className="pair"><LengthField noUnit metres={axis.b.x} onCommit={(x) => { if (x != null) move(axis.a, { x, y: axis.b.y }); }} /><LengthField metres={axis.b.y} onCommit={(y) => { if (y != null) move(axis.a, { x: axis.b.x, y }); }} /></dd>
        <dt title="Its end moves along it">Length</dt>
        <dd><LengthField metres={lengthOf(axis)} min={1e-3} onCommit={(l) => { if (l != null) { const k = l / lengthOf(axis); move(axis.a, { x: axis.a.x + (axis.b.x - axis.a.x) * k, y: axis.a.y + (axis.b.y - axis.a.y) * k }); } }} /></dd>
        <dt title="0° along x, counter-clockwise; it turns about its start">Angle</dt>
        <dd><NumberField value={angleOf(axis)} suffix="°" onCommit={(d) => { if (d != null) turn(d); }} /></dd>
        {gap && <>
          <dt title="The spacing from the parallel axis before it, square to it" data-no-i18n>{`↔ ${gap.prev}`}</dt>
          <dd><LengthField metres={gap.spacing} min={1e-3} testId="axis-spacing"
            onCommit={(s) => { if (s != null) void runSteps(setSpacing(gridId, axes, tag, s, beyond)); }} /></dd>
          <dt />
          <dd><label className="small"><input type="checkbox" checked={beyond} onChange={(e) => setBeyond(e.target.checked)} /> the axes beyond move too</label></dd>
        </>}
      </dl>
      <div className="pad row">
        <button type="button" className="btn" data-grid-dialog title="A whole grid from its spacings, numbered and lettered" onClick={() => useUi.setState({ dialog: 'grid' })}><Grid3x3 size={14} />Axis grid…</button>
        <button type="button" className="btn" data-axis-styles title="How axes look: bubbles, ends, size, line type, the first label (Drafting standards → Grid axes)" onClick={() => openStandards('axis')}><Settings2 size={14} />Axis styles…</button>
      </div>
    </>
  );
}
