/**
 * A straight wall's temporary dimensions in plan (decision 693; wallDims.ts, the measuring shared since decision 733 in
 * viewport/lineDims.ts): with one wall selected, its distances across to the nearest parallel wall or grid axis on each side
 * and, from each free end, to the wall its line runs into; read-only while a wall is being drawn, from the line clicked (its
 * core face, face or axis as the dimension reference says, from the type's layers and the line it is drawn on) to the walls
 * beside it. Where each end stands follows the user's dimension reference (core faces, faces, axis, reference line); a click on
 * a dot cycles that end for this dimension, a right click makes it the default. A click on a value types it (the project's
 * unit, sums allowed): Enter moves the wall square to itself, or its end along it, as dragging its grips does
 * (architecture.updateWall, one undo step); Escape leaves it. The padlock makes a permanent dimension of the view.
 */

import type { PlanOverlayProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { useGripDrag } from '../../viewport/gripDrag';
import { useDrawnPath } from '../../viewport/drawnPath';
import { arcOf, isArc } from '../../viewport/arc';
import { referenceArc } from '../../viewport/wallReference';
import { TempDimSet, useAxes, useDimLines, useDimRef, usePicks } from '../../viewport/LineTempDims';
import { t } from '../../i18n';
import { chainMode } from '../core/chainTool';
import { newWallSection } from './newWallSection';
import { commitAcross, commitEnd, hostOf, wallDims, type Host } from './wallDims';

export function WallDims({ toScreen, scale, view }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const drawn = useDrawnPath((s) => s.now);
  const mode = useDimRef();
  const one = !tool && selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const wall = one?.type === 'IfcWall' && one.editable !== false && (!view.storeyId || !one.storeyId || one.storeyId === view.storeyId) ? one : undefined;
  const drawing = tool === 'architecture.wall' && chainMode(tool) === 'line' && drawn?.tool === tool && (!view.storeyId || drawn.storeyId === view.storeyId) ? drawn : null;
  const all = useDimLines(wall || drawing ? view.storeyId : null);
  const axes = useAxes(view.storeyId);
  const [picks, pick] = usePicks(`${wall?.id ?? ''}|${mode}`);
  if (!all || !(scale > 0)) return null;
  // A wall measures to walls and grid axes, as it did before columns and beams had temporary dimensions.
  const lines = all.filter((l) => l.kind === 'wall');

  let host: Host;
  let live: boolean;
  if (wall) {
    const c = arcOf(wall.props);
    const info = lines.find((w) => w.id === wall.id);
    if (!c || isArc(c) || !info) return null;
    if (drag && drag.owners.includes(wall.id)) return null;
    host = hostOf(info, referenceArc(wall.props)?.left ?? 0);
    live = true;
  } else if (drawing) {
    // Measured from the line being clicked (the wall's reference as it will be drawn), its faces and core `left` off it as Space left them.
    const { bands, left } = newWallSection();
    const lo = Math.min(...bands.map((b) => b.from)), hi = Math.max(...bands.map((b) => b.to));
    const core = bands.filter((b) => b.core);
    const c0 = core.length ? Math.min(...core.map((b) => b.from)) : lo, c1 = core.length ? Math.max(...core.map((b) => b.to)) : hi;
    host = { a: drawing.a, b: drawing.b, ref: 0, faces: [lo - left, hi - left], core: [c0 - left, c1 - left], joins: { start: [], end: [], path: [] } };
    live = false;
  } else return null;

  const dims = wallDims(host, lines, axes, picks, { mode });
  if (!dims.length) return null;
  const update = (to: { a: { x: number; y: number }; b: { x: number; y: number } } | null) =>
    to && wall ? execute('architecture.updateWall', { id: wall.id, x1: to.a.x, y1: to.a.y, x2: to.b.x, y2: to.b.y, arc: 0, detachJoinedEnds: true }) : Promise.resolve(null);
  return (
    <TempDimSet testId="wall-dims" className="wall-dims" live={live} drawing={!!drawing && !wall} pick={pick} toScreen={toScreen} scale={scale} view={view}
      sets={[{
        host, dims,
        commit: (d, v) => update(d.kind === 'across' ? commitAcross(host, d, v) : commitEnd(host, d, v)),
        valueTitle: (d) => d.kind === 'across' ? t('Click to type the distance: the wall moves to match') : t('Click to type the distance: the wall\'s end moves to match'),
        ownTitle: t('Click: measure from the wall\'s reference line, its near face or its far face'),
      }]} />
  );
}
