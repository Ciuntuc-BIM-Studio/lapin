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
import type { Grip, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { useUi } from '../../app/store';
import { frontDiscipline } from './disciplines';

const pt = (raw: unknown): Vec2 | null => (Array.isArray(raw) && raw.length >= 2 ? { x: Number(raw[0]), y: Number(raw[1]) } : null);

/** The points a section's line is drawn through: `pts`, else its start and end. */
export function sectionPoints(node: NodeDto): Vec2[] {
  const pts = Array.isArray(node.props.pts) ? (node.props.pts as unknown[]).map(pt).filter((p): p is Vec2 => !!p) : [];
  if (pts.length >= 2) return pts;
  const a = pt(node.props.start), b = pt(node.props.end);
  return a && b ? [a, b] : [];
}

export const isSection = (n: NodeDto | undefined | null): boolean => n?.type === 'LapinView' && n.props.kind === 'section';
export const isCurvedSection = (n: NodeDto): boolean => n.props.curve === 'arc';
export const followedBy = (n: NodeDto): string | null => (typeof n.props.along === 'string' ? n.props.along : null);

/** What a section can follow: anything with a line in plan the engine knows (roads, ditches, retaining walls, site lines, walls, beams, footings, drafted shapes). */
export const FOLLOWABLE = ['IfcPavement', 'IfcEarthworksCut', 'LapinRetainingWall', 'LapinSiteFeature', 'IfcWall', 'IfcBeam', 'IfcFooting', 'IfcAnnotation'];

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
    id: 'view.sectionCurved', label: 'Curved section', icon: Spline, shortcut: 'SC',
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

/** A section's grips in plan: its points (dragged, or taken out), a new point on each piece; a curved section its three points; one that follows an element has none of its own. */
export function sectionGrips(node: NodeDto): Grip[] {
  if (!isSection(node) || followedBy(node)) return [];
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
  return grips;
}

export const sectionRefs = (node: NodeDto): RefLine[] => (isSection(node) && sectionPoints(node).length >= 2 ? [{ points: sectionPoints(node), closed: false, kind: 'axis' }] : []);

const fmt = (v: number) => (Math.abs(v) >= 100 ? Math.round(v).toString() : v.toFixed(2));

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
      <dd className="muted small">{kind}{followed ? <>: <span data-no-i18n>{followed.name}</span></> : null}{pts.length >= 2 ? <span data-no-i18n> · {fmt(length)} m</span> : null}</dd>
      {along && (
        <>
          <dt title="The section follows the element's line as it is now; stop following to keep the line as it is and edit it">Follows</dt>
          <dd><button type="button" className="btn small" onClick={() => void execute('view.setLine', { id: view.id, along: null })}>Stop following</button></dd>
        </>
      )}
      <dt title="Ticks and chainage (0+020) along the bottom every so many metres, from the station the line starts at; empty: a tenth of the scale when the section follows an element, else none; 0: none">Stations (m)</dt>
      <dd>
        <input key={`${view.id}:${stations}`} className="cell" type="number" min={0} defaultValue={stations == null ? '' : String(stations)} placeholder={along ? String(Math.max(1, scale / 10)) : '0'} aria-label="Stations step"
          onBlur={(e) => { const v = e.target.value.trim(); if (v !== (stations == null ? '' : String(stations))) void execute('view.setLine', { id: view.id, stations: v === '' ? null : Number(v) }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }} />
      </dd>
    </>
  );
}
