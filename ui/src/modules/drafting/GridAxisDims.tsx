/**
 * A grid axis's temporary dimensions in plan (forum #78; gridDims.ts): with one axis selected, the distances to the
 * parallel axes either side of it (for a radial grid's axis, the angles to its neighbours round the centre). A click on a
 * value types it (the project's unit, sums allowed; an angle in degrees): Enter moves the selected axis so the dimension
 * reads it, the neighbour staying put, in one undo step; with "move the axes beyond too" (shared with the Properties
 * panel's spacing) the axes past it move with it. The padlock makes the distance a dimension of the view, held on both axes.
 */

import type { PlanOverlayProps, Vec2 } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { useGripDrag } from '../../viewport/gripDrag';
import { runSteps } from '../../viewport/grips';
import { Marks, ROW_PX, type Shown } from '../../viewport/TempDimMarks';
import { axesOf, useAxesBeyond } from '../../shell/gridAxes';
import { t } from '../../i18n';
import { axisClickPoint, axisDims, commitSteps, formatDegrees, framePoint, parseDegrees } from './gridDims';

export function GridAxisDims({ toScreen, scale, view }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const { beyond, set: setBeyond } = useAxesBeyond();
  const one = !tool && selected.length === 1 && selected[0].includes('#') ? selected[0] : null;
  const gridId = one ? one.slice(0, one.indexOf('#')) : null;
  const grid = gridId ? nodes.get(gridId) : undefined;
  if (!one || !gridId || grid?.type !== 'IfcGrid' || !(scale > 0)) return null;
  if (drag && drag.owners.some((o) => o === gridId || o === one)) return null;
  const tag = one.slice(gridId.length + 1);
  const axes = axesOf(grid.props.axes);
  const dims = axisDims(axes, tag, axisClickPoint(one));
  if (!dims?.dims.length) return null;
  const live = grid.editable !== false;
  const commit = (key: string) => (v: number) => runSteps(commitSteps(gridId, axes, dims, key, v, beyond));
  const viewId = view.id ?? `plan:${view.storeyId}`;
  let items: Shown[];
  let box: Vec2;
  if (dims.kind === 'parallel') {
    const pt = (s: number, out: number) => toScreen(framePoint(dims, s, dims.station + out / scale));
    items = dims.dims.map((d) => ({
      key: d.key, row: 0, a: d.a, b: d.b, pt, steps: 1, value: d.value, data: { ref: 'axis', other: d.other },
      commit: commit(d.key),
      valueTitle: t('Click to type the distance: the axis moves to match'),
      // Made permanent: an aligned dimension between the two axes, its points held on them.
      permanent: () => {
        const [a, b] = [framePoint(dims, d.a, dims.station), framePoint(dims, d.b, dims.station)];
        const at = framePoint(dims, (d.a + d.b) / 2, dims.station + ROW_PX[0] / scale);
        void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [at.x, at.y] });
      },
    }));
    box = toScreen(framePoint(dims, dims.dims[0].b, dims.station + (ROW_PX[0] + 26) / scale));
  } else {
    const c = dims.centre;
    const pt = (a: number, out: number) => toScreen({ x: c.x + Math.cos(a) * (dims.radius + out / scale), y: c.y + Math.sin(a) * (dims.radius + out / scale) });
    items = dims.dims.map((d) => ({
      key: d.key, row: 0, a: d.a, b: d.b, pt, steps: 24, value: d.value, data: { ref: 'axis', other: d.other, angle: 'true' },
      commit: commit(d.key), format: formatDegrees, parse: parseDegrees,
      valueTitle: t('Click to type the angle: the axis turns about the centre to match'),
    }));
    box = pt(dims.dims[0].b, ROW_PX[0] + 26);
  }
  return (
    <g className="temp-dims grid-dims" data-testid="grid-dims" data-kind={dims.kind}>
      <Marks items={items} live={live} />
      {live && (
        <foreignObject x={box.x - 4} y={box.y - 10} width={220} height={22} data-overlay-hit style={{ pointerEvents: 'all', overflow: 'visible' }}>
          <label className="temp-dim-option small" title={t('With it, the axes past the selected one move with it, as the spacing in its properties does')}>
            <input type="checkbox" data-field="grid-dims-beyond" checked={beyond} onChange={(e) => setBeyond(e.target.checked)} /> {t('move the axes beyond too')}
          </label>
        </foreignObject>
      )}
    </g>
  );
}
