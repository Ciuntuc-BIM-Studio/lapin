/**
 * Sections along more than one line (docs/sections.md): jogged (a polyline,
 * each straight piece a planar cut laid beside the previous at its length),
 * curved (an arc through three points) and along an element (a road's axis,
 * a wall, a drafted polyline: its line read live, with the stations along
 * it). Their tools, their grips in plan and their fields in the view's
 * settings. The engine keeps `pts`, `curve` and `along` on the view.
 */

import { GitCommitHorizontal, Route, Spline } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripView, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { useUi } from '../../app/store';
import { frontDiscipline } from './disciplines';
import { pt } from '../../platform/vec';
import { formatLength, lengthText } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';

/** The points a section's line is drawn through: `pts`, else its start and end. */
export function sectionPoints(node: NodeDto): Vec2[] {
  const pts = Array.isArray(node.props.pts) ? (node.props.pts as unknown[]).map(pt).filter((p): p is Vec2 => !!p) : [];
  if (pts.length >= 2) return pts;
  const a = pt(node.props.start), b = pt(node.props.end);
  return a && b ? [a, b] : [];
}

const isSection = (n: NodeDto | undefined | null): boolean => n?.type === 'LapinView' && n.props.kind === 'section';
const isCurvedSection = (n: NodeDto): boolean => n.props.curve === 'arc';
const followedBy = (n: NodeDto): string | null => (typeof n.props.along === 'string' ? n.props.along : null);

/** What a section can follow: anything with a line in plan the engine knows (roads, ditches, retaining walls, site lines, walls, beams, footings, drafted shapes). */
const FOLLOWABLE = ['IfcPavement', 'IfcEarthworksCut', 'LapinRetainingWall', 'LapinSiteFeature', 'IfcWall', 'IfcBeam', 'IfcFooting', 'IfcAnnotation'];

async function open(r: unknown) {
  const id = (r as { id?: string } | null)?.id;
  if (id) useUi.setState({ view: 'drawing', drawingViewId: id, tool: null });
}

/** The tools beside View → Section: a jogged, a curved and a following section. */
export const sectionTools: ToolDef[] = [
  {
    id: 'view.sectionJogged', label: 'Jogged section', icon: GitCommitHorizontal, shortcut: 'SJ',
    gesture: { kind: 'polyline' },
    complete: async ({ points }, ctx) => {
      if (points.length < 2) return;
      await open(await ctx.execute('view.create', { kind: 'section', pts: points.map((p) => [p.x, p.y]), discipline: frontDiscipline() }));
    },
  },
  {
    id: 'view.sectionCurved', label: 'Curved section', icon: Spline, shortcut: 'SV',
    gesture: { kind: 'threePoints', arc: true },
    complete: async ({ points: [a, b, c] }, ctx) => {
      // Three points: the arc's start, its end, then a point on it (as the curved wall is drawn).
      if (!a || !b || !c) return;
      await open(await ctx.execute('view.create', { kind: 'section', pts: [[a.x, a.y], [c.x, c.y], [b.x, b.y]], curve: 'arc', discipline: frontDiscipline() }));
    },
  },
  {
    id: 'view.sectionAlong', label: 'Section along', icon: Route, shortcut: 'SA',
    gesture: { kind: 'pick', types: FOLLOWABLE },
    complete: async ({ entity }, ctx) => {
      if (!entity) return;
      await open(await ctx.execute('view.create', { kind: 'section', along: entity.id, name: `Section along ${entity.name}`, discipline: frontDiscipline() }));
    },
  },
];

const isAcross = (n: NodeDto): boolean => typeof n.props.across === 'string';
const num = (v: unknown, fallback: number): number => (typeof v === 'number' ? v : fallback);
const r3 = (v: number) => Math.round(v * 1000) / 1000;

/**
 * A section across an element (decision 223): its line is derived, so its ends set the margin on their side (dragged along
 * the line, the other end fixed) and its middle slides the station along the element (the drag projected on the element's
 * tangent, which the line stands across).
 */
function acrossGrips(node: NodeDto): Grip[] {
  const pts = sectionPoints(node);
  if (pts.length < 2) return [];
  const [a, b] = [pts[0], pts[pts.length - 1]];
  const len = Math.hypot(b.x - a.x, b.y - a.y);
  if (len < 1e-9) return [];
  const u = { x: (b.x - a.x) / len, y: (b.y - a.y) / len };
  // The section looks left of its line (flipped: right), along the element towards its end.
  const t = node.props.flip === true ? { x: u.y, y: -u.x } : { x: -u.y, y: u.x };
  const along = (p: Vec2, from: Vec2, d: Vec2) => (p.x - from.x) * d.x + (p.y - from.y) * d.y;
  const margin = num(node.props.margin, 0.5);
  const [mA, mB, station] = [num(node.props.marginA, margin), num(node.props.marginB, margin), num(node.props.station, 0)];
  const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  const id = node.id;
  const set = (args: Record<string, number>) => ({ command: 'view.updateAcross', args: { id, ...args } });
  return [
    { key: 'a', kind: 'end', at: a, title: 'Drag along the line: the margin on this side', step: (to) => set({ marginA: r3(Math.max(0, mA - along(to, a, u))) }) },
    { key: 'b', kind: 'end', at: b, title: 'Drag along the line: the margin on this side', step: (to) => set({ marginB: r3(Math.max(0, mB + along(to, b, u))) }) },
    { key: 'station', kind: 'centre', at: mid, title: 'Drag along the element: the station the section cuts at', step: (to) => set({ station: r3(Math.max(0, station + along(to, mid, t))) }) },
  ];
}

/** What an elevation's marker in plan tells of its line (decision 456): its ends, its right, and how far it reaches along it. */
type ElevationLine = { ends?: number[][]; right?: number[]; min?: number; max?: number; symbol?: number[] };

/** A move by the step from a grip's point to where it is dragged, through element.transform (one undo step). */
const moveBy = (id: string, from: Vec2, to: Vec2) => ({ command: 'element.transform', args: { ids: [id], op: 'move', by: [r3(to.x - from.x), r3(to.y - from.y)] } });

/**
 * An elevation's grips in plan (decision 456): its marker line's two ends, each dragged along the line, set how far the
 * elevation reaches on that side (view.setExtent, one undo step; the other end stays where it is drawn).
 */
export function elevationGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.type !== 'LapinView' || node.props.kind !== 'elevation') return [];
  const line = view?.mark?.data as ElevationLine | undefined;
  const { ends, right, min, max, symbol } = line ?? {};
  if (!ends || ends.length < 2 || !right || typeof min !== 'number' || typeof max !== 'number') return [];
  // Its symbol dragged: the marker moves (along its line, out from the building), what the elevation sees stays (decision 460).
  const at = symbol ? { x: symbol[0], y: symbol[1] } : null;
  const mover: Grip[] = at ? [{ key: 'marker', kind: 'move', at, title: 'Drag: move the marker (the elevation sees the same)', step: (to) => moveBy(node.id, at, to) }] : [];
  const along = (p: Vec2) => p.x * right[0] + p.y * right[1];
  const [a, b] = ends.map(([x, y]) => ({ x, y }));
  const set = (lo: number, hi: number) => ({ command: 'view.setExtent', args: { id: node.id, min: r3(lo), max: r3(hi) } });
  const title = 'Drag along the line: how far the elevation reaches on this side';
  return [
    { key: 'extentMin', kind: 'end', at: a, title, step: (to) => set(Math.min(along(to), max - 0.1), max) },
    { key: 'extentMax', kind: 'end', at: b, title, step: (to) => set(min, Math.max(along(to), min + 0.1)) },
    ...mover,
  ];
}

/** What a section's marker in plan tells of its heads (decision 460): each end's arrow tip and how far it stands off the end. */
type SectionHead = { end: 'a' | 'b'; at: number[]; offset: number[] };

/** A section's heads in plan: dragged, each moves off its line's end (the cut stays); Ctrl/⌘-click puts it back. */
export function sectionHeadGrips(node: NodeDto, view?: GripView): Grip[] {
  const heads = (view?.mark?.data as { heads?: SectionHead[] } | undefined)?.heads;
  if (!heads) return [];
  return heads.map((h) => {
    const at = { x: h.at[0], y: h.at[1] };
    const set = (offset: number[] | null) => ({ command: 'view.moveHead', args: { id: node.id, end: h.end, offset } });
    return {
      key: `head-${h.end}`, kind: 'move' as const, at, title: 'Drag: move the head (the section cuts the same); Ctrl/⌘-click: back on its end',
      step: (to: Vec2) => set([r3(h.offset[0] + to.x - at.x), r3(h.offset[1] + to.y - at.y)]),
      remove: () => (h.offset[0] || h.offset[1] ? set(null) : null),
    };
  });
}

/** A section's grips in plan: its points (dragged, or taken out), a new point on each piece; a curved section its three points; one that follows an element has none of its own; one across an element its margins and station. An elevation's line ends. */
export function sectionGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.props.kind === 'elevation') return elevationGrips(node, view);
  if (!isSection(node)) return [];
  const heads = sectionHeadGrips(node, view);
  if (followedBy(node)) return heads;
  if (isAcross(node)) return [...acrossGrips(node), ...heads];
  const pts = sectionPoints(node);
  if (pts.length < 2) return [];
  const id = node.id;
  const set = (next: Vec2[]) => ({ command: 'view.setLine', args: { id, pts: next.map((p) => [p.x, p.y]) } });
  const grips: Grip[] = pts.map((p, i) => ({
    key: `p${i}`, kind: i === 0 || i === pts.length - 1 ? 'end' : 'vertex', at: p,
    title: isCurvedSection(node) ? 'Drag: the arc goes through the point' : 'Drag: move the point; Ctrl/⌘-click: take it out (a jog goes)',
    step: (to: Vec2) => set(pts.map((q, k) => (k === i ? to : q))),
    remove: () => (!isCurvedSection(node) && pts.length > 2 ? set(pts.filter((_, k) => k !== i)) : null),
  }));
  if (!isCurvedSection(node))
    for (let i = 0; i + 1 < pts.length; i++) {
      const m = { x: (pts[i].x + pts[i + 1].x) / 2, y: (pts[i].y + pts[i + 1].y) / 2 };
      grips.push({
        key: `e${i}`, kind: 'edgeMid', at: m, title: 'Drag: a new point here, a jog; Ctrl/⌘-click: one halfway',
        step: (to: Vec2) => set([...pts.slice(0, i + 1), to, ...pts.slice(i + 1)]),
        insert: () => set([...pts.slice(0, i + 1), m, ...pts.slice(i + 1)]),
      });
    }
  return [...grips, ...heads];
}

export const sectionRefs = (node: NodeDto): RefLine[] => (isSection(node) && sectionPoints(node).length >= 2 ? [{ points: sectionPoints(node), closed: false, kind: 'axis' }] : []);

/** In a section's settings: its line (how many pieces, how long), what it follows, and the stations written along it. */
export function SectionFields({ view }: { view: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const pts = sectionPoints(view);
  const along = followedBy(view);
  const followed = along ? nodes.get(along) : undefined;
  let length = 0;
  for (let i = 0; i + 1 < pts.length; i++) length += Math.hypot(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y);
  const kind = along ? 'Along an element' : isCurvedSection(view) ? 'Arc' : pts.length > 2 ? 'Jogged line' : 'Straight line';
  const stations = typeof view.props.stations === 'number' ? view.props.stations : null;
  const scale = Number(view.props.scale ?? 100);
  return (
    <>
      <dt title="The line the section cuts along: straight, jogged (each piece drawn beside the previous at its length), an arc, or an element's">Line</dt>
      <dd className="muted small">{kind}{followed ? <>: <span data-no-i18n>{followed.name}</span></> : null}{pts.length >= 2 ? <span data-no-i18n> · {lengthText(length)}</span> : null}</dd>
      {along && (
        <>
          <dt title="The section follows the element's line as it is now; stop following to keep the line as it is and edit it">Follows</dt>
          <dd><button type="button" className="btn small" onClick={() => void execute('view.setLine', { id: view.id, along: null })}>Stop following</button></dd>
        </>
      )}
      <dt title="Ticks and chainage (0+020) along the bottom every so many metres, from the station the line starts at; empty: a tenth of the scale when the section follows an element, else none; 0: none">Stations</dt>
      <dd>
        <LengthField metres={stations} min={0} blank placeholder={along ? formatLength(Math.max(1, scale / 10)) : '0'} title="Stations step"
          onCommit={(v) => void execute('view.setLine', { id: view.id, stations: v })} />
      </dd>
    </>
  );
}
