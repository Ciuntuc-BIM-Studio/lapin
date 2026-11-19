/**
 * Columns' and beams' temporary dimensions in plan (decision 733; memberDims.ts, the shared measuring in viewport/lineDims.ts):
 * a selected column shows, along each of its own axes, its distances to the nearest parallel wall, column or grid axis each
 * side; a selected straight beam its distances across to parallel beams, walls and axes and from its free ends along. Where
 * each end stands follows the user's dimension reference (core faces, faces, axis, reference line). A value typed moves the
 * column (element.transform, one undo step) or the beam square to itself or its end along it (structure.updatePath, as its grips
 * do). While a column is placed or a beam drawn, the same dimensions read-only from the cursor's column or the line drawn.
 */

import type { PlanOverlayProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { useGripDrag } from '../../viewport/gripDrag';
import { useDrawnPath } from '../../viewport/drawnPath';
import { arcOf, isArc } from '../../viewport/arc';
import { TempDimSet, useAxes, useDimLines, useDimRef, usePicks, type HostDims } from '../../viewport/LineTempDims';
import { commitAcross, commitEnd, hostOf, type Host } from '../../viewport/lineDims';
import { t } from '../../i18n';
import { chainMode } from '../core/chainTool';
import { useTypePick } from './typePick';
import { beamDims, columnDims, columnLines, columnMove, type ColumnTypeProps } from './memberDims';

/** The type the tool would place with: the one picked in its bar, else the project's first. */
function pickedType(kind: 'column' | 'beam', nodes: ReturnType<typeof useSession.getState>['nodes']) {
  const id = useTypePick.getState()[kind];
  const s = useSession.getState();
  return (id ? nodes.get(id) : undefined) ?? ofClass(s, kind === 'column' ? 'IfcColumnType' : 'IfcBeamType')[0];
}

export function MemberDims({ toScreen, scale, view }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const drawn = useDrawnPath((s) => s.now);
  const mode = useDimRef();
  useTypePick();
  const one = !tool && selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const onStorey = (n: { storeyId?: string | null }) => !view.storeyId || !n.storeyId || n.storeyId === view.storeyId;
  const member = (one?.type === 'IfcColumn' || one?.type === 'IfcBeam') && one.editable !== false && onStorey(one) && !(drag && drag.owners.includes(one.id)) ? one : undefined;
  const drawnHere = drawn && (!view.storeyId || drawn.storeyId === view.storeyId) ? drawn : null;
  const placing = tool === 'structure.column' && drawnHere?.tool === tool ? drawnHere : null;
  const drawing = tool === 'structure.beam' && chainMode(tool) === 'line' && drawnHere?.tool === tool ? drawnHere : null;
  const lines = useDimLines(member || placing || drawing ? view.storeyId : null);
  const axes = useAxes(view.storeyId);
  const [picks, pick] = usePicks(`${member?.id ?? ''}|${mode}`);
  if (!lines || !(scale > 0)) return null;

  const sets: HostDims[] = [];
  let live = true;
  const moveColumn = (id: string, host: Host) => (d: Parameters<HostDims['commit']>[0], v: number) => {
    const by = columnMove(host, d, v);
    return by ? execute('element.transform', { ids: [id], op: 'move', by: [by.x, by.y] }) : Promise.resolve(null);
  };
  const columnTitle = () => t('Click to type the distance: the column moves to match');
  const ownTitle = t('Click: measure from the element\'s axis, its near face, its far face or its core faces');
  if (member?.type === 'IfcColumn') {
    const own = lines.filter((l) => l.id === member.id && l.kind === 'column');
    for (const { host, dims } of columnDims(own, lines, axes, picks, mode)) sets.push({ host, dims, commit: moveColumn(member.id, host), valueTitle: columnTitle, ownTitle });
  } else if (member?.type === 'IfcBeam') {
    const c = arcOf(member.props);
    const info = lines.find((l) => l.id === member.id && l.kind === 'beam');
    if (!c || isArc(c) || !info) return null;
    const host = hostOf(info, 0);
    const update = (to: { a: { x: number; y: number }; b: { x: number; y: number } } | null) =>
      to ? execute('structure.updatePath', { id: member.id, x1: to.a.x, y1: to.a.y, x2: to.b.x, y2: to.b.y, arc: 0, detachJoinedEnds: true }) : Promise.resolve(null);
    sets.push({
      host, dims: beamDims(host, lines, axes, picks, mode),
      commit: (d, v) => update(d.kind === 'across' ? commitAcross(host, d, v) : commitEnd(host, d, v)),
      valueTitle: (d) => d.kind === 'across' ? t('Click to type the distance: the beam moves to match') : t('Click to type the distance: the beam\'s end moves to match'),
      ownTitle,
    });
  } else if (placing) {
    live = false;
    const type = pickedType('column', nodes);
    const own = columnLines('', '', placing.b, type?.props as ColumnTypeProps | undefined);
    for (const { host, dims } of columnDims(own, lines, axes, {}, mode)) sets.push({ host, dims, commit: () => Promise.resolve(null), valueTitle: columnTitle, ownTitle });
  } else if (drawing) {
    live = false;
    const type = pickedType('beam', nodes);
    const w = (type?.props.section as { width?: number } | undefined)?.width ?? 0.25;
    const host: Host = { a: drawing.a, b: drawing.b, ref: 0, faces: [-w / 2, w / 2], core: [-w / 2, w / 2], joins: { start: [], end: [], path: [] } };
    sets.push({ host, dims: beamDims(host, lines, axes, {}, mode), commit: () => Promise.resolve(null), valueTitle: () => '', ownTitle });
  } else return null;

  const column = member?.type === 'IfcColumn' || !!placing;
  return (
    <TempDimSet testId={column ? 'column-dims' : 'beam-dims'} className={column ? 'column-dims' : 'beam-dims'} live={live} drawing={!live}
      pick={pick} toScreen={toScreen} scale={scale} view={view} sets={sets} />
  );
}
