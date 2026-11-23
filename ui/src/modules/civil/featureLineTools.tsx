/**
 * Feature lines in the interface (site grading T2, docs/plan-site-grading.md): the Feature line, Contour at elevation and Drag
 * contour tools, their grips in plan (corners, edge middles, arcs) and in 3D (a height grip on each corner), and the line drawn
 * over a selected one. The pure parts are in featureLines.ts; the engine does the work (civil.addFeatureLine and the rest).
 */

import { create } from 'zustand';
import { Grab, LineSquiggle, Waypoints } from 'lucide-react';
import type { Grip, Grip3D, GripStep, RefLine, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify } from '../../app/store';
import { promptText } from '../../shell/prompts';
import { query } from '../../engine/session';
import { formatLength, levelUnit, parseLevel, unitName } from '../../engine/unitsStore';
import { axisLine, isArc, middle, riseThrough } from '../../viewport/arc';
import { nearestOnSegment } from '../../viewport/gripKit';
import { toggleOutlineArcs, useOutlineShape } from '../../viewport/outlineShape';
import { bendSegment, draggedContour, insertCorner, lineOf, moveCorner, readHeights, readReach, removeCorner, saveLine, type FLine } from './featureLines';

/** What the two drawing tools take from their bar: heights typed or the ground's, a closed line, a contour's elevation. */
const useLineOptions = create<{ ground: boolean; closed: boolean; elevation: string }>(() => ({ ground: false, closed: false, elevation: '' }));

/** What the Drag contour tool's bar holds: the reach typed, empty for the default. */
export const useDragReach = create<{ text: string }>(() => ({ text: '' }));

/** Arcs on or off for the edges drawn next (Space), as the outline tools' bar has it. */
function ArcsToggle() {
  const arcs = useOutlineShape((s) => s.arcs);
  return <button type="button" className={`cad-toggle${arcs ? ' on' : ''}`} data-shape="arcs" aria-pressed={arcs} title="The next edges are arcs, each going on tangent from the edge before" onClick={toggleOutlineArcs}>{arcs ? 'Arcs (Space)' : 'Lines (Space)'}</button>;
}

function ClosedToggle() {
  const closed = useLineOptions((s) => s.closed);
  return <label className={`cad-toggle${closed ? ' on' : ''}`} title="Close the line back to its first corner"><input type="checkbox" data-testid="feature-line-closed" checked={closed} onChange={(e) => useLineOptions.setState({ closed: e.target.checked })} />Closed line</label>;
}

function FeatureLineBar() {
  const ground = useLineOptions((s) => s.ground);
  return (
    <>
      <ArcsToggle />
      <ClosedToggle />
      <select className={`cad-layer${ground ? ' on' : ''}`} value={ground ? 'ground' : 'heights'} data-testid="feature-line-heights" title="Type the corners' heights after drawing, or lay every corner on the ground of the terrain"
        onChange={(e) => useLineOptions.setState({ ground: e.target.value === 'ground' })}>
        <option value="heights">At heights</option>
        <option value="ground">On the ground</option>
      </select>
    </>
  );
}

function ContourBar() {
  const elevation = useLineOptions((s) => s.elevation);
  return (
    <>
      <ArcsToggle />
      <ClosedToggle />
      <input type="text" className="cad-layer" style={{ width: '6em' }} value={elevation} placeholder="Elevation, e.g. 1.50" data-testid="contour-elevation" aria-label="Elevation of the contour"
        title="The contour's elevation, ±0.00 = 0 (empty: asked when it is drawn)" onChange={(e) => useLineOptions.setState({ elevation: e.target.value })} />
    </>
  );
}

function DragBar() {
  const text = useDragReach((s) => s.text);
  return (
    <input type="text" className="cad-layer" style={{ width: '6em' }} value={text} placeholder="Reach, e.g. 9 m" data-testid="drag-contour-reach" aria-label="Reach of the drag"
      title="How far from the grab point the ground follows (empty: the larger of 2 m and three times the drag)" onChange={(e) => useDragReach.setState({ text: e.target.value })} />
  );
}

/** The rises of the edges drawn: one per edge, 0 where straight; closed adds the closing edge, straight. */
function risesOf(n: number, arcs: number[] | undefined, closed: boolean): number[] {
  const r = Array.from({ length: n - 1 }, (_, i) => arcs?.[i] ?? 0);
  return closed ? [...r, 0] : r;
}

/** An elevation asked (model height, ±0.00 = 0), the present one offered; null when cancelled or unread. */
export function askHeight(text: string, initial: string): number | null {
  const answer = promptText(text, initial, { unit: unitName(levelUnit()) });
  if (answer == null) return null;
  const v = parseLevel(answer);
  if (v == null || !Number.isFinite(v)) { notify('That elevation does not read: type a number in the project\'s unit.'); return null; }
  return v;
}

/** A feature line along a polyline (Enter finishes) on the terrain under it: on the ground, or at the heights typed (a corner without one takes the ground's). */
export const featureLine: ToolDef = {
  id: 'civil.featureLine', label: 'Feature line', icon: Waypoints, shortcut: 'FL',
  gesture: { kind: 'polyline', arcs: true },
  options: FeatureLineBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 2) return;
    const o = useLineOptions.getState();
    const closed = o.closed && points.length >= 3;
    let heights: (number | null)[] = points.map(() => null);
    if (!o.ground) {
      const answer = promptText('The corners\' heights ({unit}, ±0.00 = 0), in order, separated by ; (empty: the ground\'s there):', '', { unit: unitName(levelUnit()) });
      if (answer == null) return;
      const read = readHeights(answer, points.length);
      if (!read) { notify('That elevation does not read: type a number in the project\'s unit.'); return; }
      heights = read;
    }
    const pts = points.map((p, i) => (heights[i] != null ? [p.x, p.y, heights[i] as number] : [p.x, p.y]));
    const rises = risesOf(points.length, arcs, closed);
    await ctx.execute('civil.addFeatureLine', { points: pts, ...(rises.some((r) => Math.abs(r) > 1e-6) ? { arcs: rises } : {}), ...(closed ? { closed: true } : {}), ...(o.ground ? { drape: true } : {}) });
  },
};

/** A contour drawn at an elevation: a polyline (or a closed line) all at the one height, the terrain passing through it. */
export const contourAt: ToolDef = {
  id: 'civil.contourAt', label: 'Contour at elevation', icon: LineSquiggle, shortcut: 'CV',
  gesture: { kind: 'polyline', arcs: true },
  options: ContourBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 2) return;
    const o = useLineOptions.getState();
    let z: number | null;
    if (o.elevation.trim() !== '') {
      z = parseLevel(o.elevation);
      if (z == null || !Number.isFinite(z)) { notify('That elevation does not read: type a number in the project\'s unit.'); return; }
    } else z = askHeight('The contour\'s elevation ({unit}, ±0.00 = 0):', '0');
    if (z == null) return;
    const closed = o.closed && points.length >= 3;
    const rises = risesOf(points.length, arcs, closed);
    await ctx.execute('civil.addFeatureLine', { points: points.map((p) => [p.x, p.y]), elevation: z, ...(rises.some((r) => Math.abs(r) > 1e-6) ? { arcs: rises } : {}), ...(closed ? { closed: true } : {}) });
  },
};

/** What `civil.contourNear` answers. */
interface Near { id: string; elevation: number; distance: number; points: number[][]; lineId?: string }

/** The contour under the cursor, as the last answer of the engine has it: asked again when the cursor has moved, one question at a time. */
const grab: { at: Vec2 | null; near: Near | null; busy: boolean; redraw: () => void } = { at: null, near: null, busy: false, redraw: () => undefined };

function nearAt(at: Vec2, redraw: () => void): Near | null {
  grab.redraw = redraw;
  if (!grab.busy && (!grab.at || Math.hypot(grab.at.x - at.x, grab.at.y - at.y) > 1e-3)) {
    grab.busy = true;
    void query<Near | null>('civil.contourNear', { x: at.x, y: at.y }).then((r) => { grab.at = at; grab.near = r; grab.busy = false; grab.redraw(); });
  }
  return grab.near;
}

const asLine = (n: Near): Vec2[] => n.points.filter((p) => p.length >= 2).map((p) => ({ x: p[0], y: p[1] }));

/**
 * A contour dragged in plan: press on one (it lights up as the cursor comes near it) and drag; the ground follows within the reach,
 * fading with the distance from the grab. The drag is previewed while the button is down; Esc drops it.
 */
export const dragContour: ToolDef = {
  id: 'civil.dragContour', label: 'Drag contour', icon: Grab, shortcut: 'DG',
  options: DragBar,
  gesture: {
    kind: 'twoPoints', drag: true,
    hover: (at, redraw) => { const n = nearAt(at, redraw); return n ? [asLine(n)] : []; },
    body: ({ a, b }) => {
      const n = nearAt(a, grab.redraw);
      if (!n) return [];
      const reach = readReach(useDragReach.getState().text) ?? undefined;
      return [draggedContour(asLine(n), a, b, reach).line];
    },
  },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [a, b] = points;
    if (Math.hypot(b.x - a.x, b.y - a.y) < 1e-6) return;
    const near = await query<Near | null>('civil.contourNear', { x: a.x, y: a.y });
    if (!near) { notify('No contour near where you pressed: press on a contour line.'); return; }
    const reach = readReach(useDragReach.getState().text);
    if (reach === null) { notify('That reach does not read: type a length, or leave it empty.'); return; }
    const move = { from: [a.x, a.y], to: [b.x, b.y], ...(reach ? { reach } : {}) };
    await ctx.execute('civil.dragContour', near.lineId ? { lineId: near.lineId, ...move } : { id: near.id, elevation: near.elevation, ...move });
    grab.at = null; grab.near = null;
  },
};

/** A line in plan as the screen draws it: each arc in short steps. */
export function flatten(l: FLine): Vec2[] {
  const n = l.pts.length;
  const out: Vec2[] = [];
  for (let k = 0; k < l.arcs.length; k++) out.push(...axisLine({ a: l.pts[k], b: l.pts[(k + 1) % n], rise: l.arcs[k] }).slice(0, -1));
  out.push(l.closed ? l.pts[0] : l.pts[n - 1]);
  return out;
}

/** The line drawn over a selected feature line. */
export const featureLineRefs = (n: NodeDto): RefLine[] => {
  const l = lineOf(n.props);
  return l.pts.length >= 2 ? [{ points: flatten(l), closed: false, kind: 'axis' }] : [];
};

/** A corner's height typed, as one engine command. */
const heightStep = (id: string, i: number, now: number | null): GripStep | null => {
  const z = askHeight('The corner\'s elevation ({unit}, ±0.00 = 0):', now == null ? '' : formatLength(now, levelUnit()));
  return z == null ? null : { command: 'civil.setFeatureLinePoints', args: { id, indices: [i], z } };
};

/**
 * A feature line's grips in plan: its corners (dragged; Ctrl/⌘-click takes one out; the right-click types its elevation or lays it
 * on the ground), the middle of each segment (dragged or Ctrl/⌘-clicked: a corner there) and, on an arc, a grip at its middle that
 * bends it. Each edit is the line as `civil.updateFeatureLine` takes it, one undo step.
 */
export function featureLineGrips(n: NodeDto): Grip[] {
  const l = lineOf(n.props);
  if (l.pts.length < 2) return [];
  const save = (next: FLine) => saveLine(n.id, next);
  const grips: Grip[] = l.pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex' as const, at: p,
    title: 'Drag to move the corner · Ctrl/⌘-click: remove this corner',
    step: (to: Vec2) => save(moveCorner(l, i, to)),
    remove: () => { const next = removeCorner(l, i); return next ? save(next) : null; },
    menu: [
      { label: 'Set elevation…', run: () => heightStep(n.id, i, l.resolved[i]) },
      { label: 'On the ground', run: () => ({ command: 'civil.setFeatureLinePoints', args: { id: n.id, indices: [i], ground: true } }) },
    ],
  }));
  const count = l.arcs.length;
  for (let k = 0; k < count; k++) {
    const a = l.pts[k], b = l.pts[(k + 1) % l.pts.length];
    const c = { a, b, rise: l.arcs[k] };
    const bent = isArc(c);
    const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    grips.push({
      key: `e${k}`, kind: 'edgeMid', at: mid,
      title: 'Drag to put a corner there · Ctrl/⌘-click: add a corner',
      step: (to: Vec2) => save(insertCorner(l, k, to)),
      insert: () => save(insertCorner(l, k)),
      edge: bent ? axisLine(c) : [a, b],
      addCornerAt: (to: Vec2) => save(insertCorner(l, k, bent ? to : nearestOnSegment(a, b, to).at)),
      ...(bent
        ? { straighten: () => save(bendSegment(l, k, 0)) }
        : { bend: () => save(bendSegment(l, k, Math.hypot(b.x - a.x, b.y - a.y) / 8)) }),
    });
    if (bent) grips.push({ key: `a${k}`, kind: 'radius', at: middle(c), title: 'Drag to bend the segment through the point', step: (to: Vec2) => save(bendSegment(l, k, riseThrough(a, b, to))) });
  }
  return grips;
}

/** A feature line's corners as height grips in 3D: dragged up or down, or clicked to type the elevation. */
export function featureLineGrips3d(n: NodeDto): Grip3D[] {
  const l = lineOf(n.props);
  return l.pts.flatMap((p, i): Grip3D[] => {
    const z = l.resolved[i];
    if (z == null) return [];
    return [{
      key: `fl${i}`, kind: 'height', at: { x: p.x, y: p.y, z }, dir: { x: 0, y: 0, z: 1 },
      title: 'Corner height: drag up or down, or click to type its elevation',
      step: (by: number) => ({ command: 'civil.setFeatureLinePoints', args: { id: n.id, indices: [i], dz: by } }),
      click: () => heightStep(n.id, i, z),
    }];
  });
}
