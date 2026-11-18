/**
 * A straight wall's temporary dimensions in plan (decision 693; wallDims.ts): with one wall selected, its distances across to
 * the nearest parallel wall or grid axis on each side and, from each free end, to the wall its line runs into; read-only while
 * a wall is being drawn, from the line clicked to the walls beside it. A click on a value types it (the project's unit, sums
 * allowed): Enter moves the wall square to itself, or its end along it, as dragging its grips does (architecture.updateWall,
 * one undo step); Escape leaves it. The reference's end cycles the other wall's faces, core faces and axis; the wall's own end
 * its reference line, near face and far face. The padlock makes a permanent dimension of the view.
 */

import { useEffect, useRef, useState } from 'react';
import type { PlanOverlayProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { hasModule } from '../../platform/brand';
import { useGripDrag } from '../../viewport/gripDrag';
import { useDrawnPath } from '../../viewport/drawnPath';
import { Marks, ROW_PX, type Shown } from '../../viewport/TempDimMarks';
import { arcOf, isArc } from '../../viewport/arc';
import { referenceArc } from '../../viewport/wallReference';
import { axesOf } from '../../shell/gridAxes';
import { t } from '../../i18n';
import { chainMode } from '../core/chainTool';
import { newWallSection } from './newWallSection';
import { commitAcross, commitEnd, framePoint, hostOf, wallDims, type AxisLine, type Host, type WallDim, type WallDimPick, type WallLineInfo } from './wallDims';

interface Answer { storeyId: string; walls: { id: string; name: string; start: [number, number]; end: [number, number]; faces: [number, number]; core?: [number, number]; joins: WallLineInfo['joins'] }[] }

/** The storey's straight walls from the engine, asked again whenever the model changes; the last answer kept until the next. */
function useWallLines(storeyId: string | null): WallLineInfo[] | null {
  const nodes = useSession((s) => s.nodes);
  const [lines, setLines] = useState<{ storeyId: string; walls: WallLineInfo[] } | null>(null);
  const asked = useRef(0);
  useEffect(() => {
    if (!storeyId) { setLines(null); return; }
    const n = ++asked.current;
    void query<Answer>('architecture.wallLines', { storeyId }).then((r) => {
      if (n !== asked.current || !r) return;
      setLines({ storeyId: r.storeyId, walls: r.walls.map((w) => ({ id: w.id, name: w.name, a: { x: w.start[0], y: w.start[1] }, b: { x: w.end[0], y: w.end[1] }, faces: w.faces, core: w.core, joins: w.joins })) });
    });
  }, [storeyId, nodes]);
  return lines && lines.storeyId === storeyId ? lines.walls : null;
}

/** The grid axes a plan of this storey shows: its grids' and those of no storey. */
function useAxes(storeyId: string | null): AxisLine[] {
  const nodes = useSession((s) => s.nodes);
  const out: AxisLine[] = [];
  for (const n of nodes.values()) {
    if (n.type !== 'IfcGrid' || (n.storeyId && n.storeyId !== storeyId)) continue;
    for (const x of axesOf(n.props.axes)) out.push({ id: `${n.id}#${x.tag}`, name: x.tag, a: x.a, b: x.b });
  }
  return out;
}

/** Per wall, what the user chose for its dimensions; forgotten for another. */
function usePicks(who: string): [Record<string, WallDimPick>, (key: string, p: WallDimPick) => void] {
  const [picks, setPicks] = useState<{ who: string; p: Record<string, WallDimPick> }>({ who, p: {} });
  const now = picks.who === who ? picks.p : {};
  return [now, (key, p) => setPicks({ who, p: { ...now, [key]: { ...now[key], ...p } } })];
}

export function WallDims({ toScreen, scale, view }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const drawn = useDrawnPath((s) => s.now);
  const one = !tool && selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const wall = one?.type === 'IfcWall' && one.editable !== false && (!view.storeyId || !one.storeyId || one.storeyId === view.storeyId) ? one : undefined;
  const drawing = tool === 'architecture.wall' && chainMode(tool) === 'line' && drawn?.tool === tool && (!view.storeyId || drawn.storeyId === view.storeyId) ? drawn : null;
  const lines = useWallLines(wall || drawing ? view.storeyId : null);
  const axes = useAxes(view.storeyId);
  const [picks, pick] = usePicks(wall?.id ?? '');
  if (!lines || !(scale > 0)) return null;

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
    // Measured from the line being clicked (the wall's reference as it will be drawn), its faces `left` off it as Space left them.
    const { bands, left } = newWallSection();
    const lo = Math.min(...bands.map((b) => b.from)), hi = Math.max(...bands.map((b) => b.to));
    host = { a: drawing.a, b: drawing.b, ref: 0, faces: [lo - left, hi - left], joins: { start: [], end: [], path: [] } };
    live = false;
  } else return null;

  const dims = wallDims(host, lines, axes, picks);
  if (!dims.length) return null;
  const viewId = view.id ?? `plan:${view.storeyId}`;
  const update = (to: { a: { x: number; y: number }; b: { x: number; y: number } } | null) =>
    to && wall ? execute('architecture.updateWall', { id: wall.id, x1: to.a.x, y1: to.a.y, x2: to.b.x, y2: to.b.y, arc: 0, detachJoinedEnds: true }) : Promise.resolve(null);
  // Across: the line runs across the wall at its station, `out` px further along it; along: beside its left face.
  const ptOf = (d: WallDim) => d.kind === 'across'
    ? (s: number, out: number) => toScreen(framePoint(host, s, d.station + out / scale))
    : (s: number, out: number) => toScreen(framePoint(host, host.faces[1] + out / scale, s));
  const world = (d: WallDim, s: number, out: number) => d.kind === 'across' ? framePoint(host, s, d.station + out / scale) : framePoint(host, host.faces[1] + out / scale, s);
  const items: Shown[] = dims.map((d) => ({
    key: d.key, row: 0, a: d.a, b: d.b, pt: ptOf(d), steps: 1, value: d.value,
    data: { ref: d.target.kind, what: d.options[d.opt].what, own: d.ownWhat, target: d.target.id },
    refTitle: `${t(d.target.kind === 'axis' ? 'The grid axis' : 'Click: the next reference (near face, far face, core faces, axis)')} · ${d.target.name}`,
    cycleRef: live && d.options.length > 1 ? () => pick(d.key, { opt: d.opt + 1 }) : undefined,
    cycleOwn: live && d.kind === 'across' ? () => pick(d.key, { own: d.own + 1 }) : undefined,
    ownTitle: t('Click: measure from the wall\'s reference line, its near face or its far face'),
    valueTitle: d.kind === 'across' ? t('Click to type the distance: the wall moves to match') : t('Click to type the distance: the wall\'s end moves to match'),
    commit: (v) => update(d.kind === 'across' ? commitAcross(host, d, v) : commitEnd(host, d, v)),
    permanent: live && hasModule('drafting') ? () => {
      const [a, b, at] = [world(d, d.a, 0), world(d, d.b, 0), world(d, (d.a + d.b) / 2, ROW_PX[0])];
      void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [at.x, at.y] });
    } : undefined,
  }));
  return (
    <g className="temp-dims wall-dims" data-testid="wall-dims" data-drawing={drawing && !wall ? 'true' : undefined}>
      <Marks items={items} live={live} />
    </g>
  );
}
