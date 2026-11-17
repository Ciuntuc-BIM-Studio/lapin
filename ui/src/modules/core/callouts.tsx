/**
 * Detail callouts (forum topic 107, as Revit's and ArchiCAD's): View → Detail callout (DC) in a plan, a section or an
 * elevation draws a rectangle (or, from the tool's bar, a circle or a sketched polygon) round an area; the engine makes a
 * detail view of the same kind cropped to it, at a larger scale and the finest level (`view.callout`, Callouts.cs), and the
 * parent draws the boundary, a leader and a head with the detail's number and sheet ("3 / A-501", "–" before it is placed),
 * the calloutHead symbol slot's when one is chosen. Double-click the callout (or right-click → Open view) opens the detail;
 * its grips reshape it and crop the detail again; deleting it asks whether the detail view goes too. With "Refer to" set,
 * the callout points at a view that is there (a drafting view, another detail) and makes none.
 */

import { CircleDashed, ExternalLink, Pentagon, ScanSearch } from 'lucide-react';
import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { notify, setTool, useUi } from '../../app/store';
import type { Grip, GripView, ToolContext, ToolDef, Vec2 } from '../../platform/module';
import { NumberField } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { turnOfView } from './ViewCrop';
import { registerDraftMenu } from '../../viewport/draftMenu';
import { openCallout } from '../../viewport/openView';
import { FrameLineFields, useInheritedFrame } from './frameLine';
import { ownFrameLine, ownPatch } from './frameLineModel';

export type CalloutShape = 'rect' | 'circle' | 'polygon';

/** What the tool makes: the detail's scale (null: 1:20 or the project's next standard scale under the parent's) and, for a reference callout, the view it calls. */
export const useCalloutTool = create<{ scale: number | null; reference: string | null }>(() => ({ scale: null, reference: null }));

/** The scales the tool's bar offers a detail. */
const DETAIL_SCALES = [50, 25, 20, 10, 5, 2, 1];

const TOOL_IDS: Record<CalloutShape, string> = { rect: 'view.callout', circle: 'view.calloutCircle', polygon: 'view.calloutPolygon' };

const rot = (p: Vec2, a: number): Vec2 => (a === 0 ? p : { x: p.x * Math.cos(a) - p.y * Math.sin(a), y: p.x * Math.sin(a) + p.y * Math.cos(a) });
const arr = (p: Vec2) => [p.x, p.y];

/**
 * The boundary as `view.callout` and `view.updateCallout` take it, from the points drawn (drawing coordinates): a rectangle's
 * two corners into the view's turned frame (`turn`, radians, as its crop is kept), a circle's centre and a point on it, a polygon's corners.
 */
export function boundaryArgs(shape: CalloutShape, points: Vec2[], turn = 0): Record<string, unknown> | null {
  if (shape === 'rect') {
    if (points.length < 2) return null;
    const [a, b] = [rot(points[0], turn), rot(points[1], turn)];
    return { shape, min: [Math.min(a.x, b.x), Math.min(a.y, b.y)], max: [Math.max(a.x, b.x), Math.max(a.y, b.y)] };
  }
  if (shape === 'circle') {
    if (points.length < 2) return null;
    const r = Math.hypot(points[1].x - points[0].x, points[1].y - points[0].y);
    return r > 0 ? { shape, centre: arr(points[0]), radius: r } : null;
  }
  return points.length >= 3 ? { shape, pts: points.map(arr) } : null;
}

/** The turn the view a tool works in shows its drawing with (a level's default plan: none). */
function turnOf(viewId: string): number {
  const node = useSession.getState().nodes.get(viewId);
  return node ? turnOfView(node) : 0;
}

async function complete(shape: CalloutShape, points: Vec2[], ctx: ToolContext): Promise<void> {
  const boundary = boundaryArgs(shape, points, turnOf(ctx.viewId));
  if (!boundary) return;
  const { scale, reference } = useCalloutTool.getState();
  const r = await ctx.execute('view.callout', { viewId: ctx.viewId, ...boundary, ...(scale ? { scale } : {}), ...(reference ? { reference } : {}) }) as { id?: string; view?: string } | null;
  if (!r?.id) return;
  useUi.setState({ selection: r.id, selected: [r.id], tool: null });
  notify(reference ? 'The callout refers to the view chosen: double-click it to open that view.' : 'The detail view is made: double-click the callout to open it.');
}

const tooltip = 'A detail of an area of this view: a new view cropped to it at a larger scale, marked here with its number and sheet';

/** View → Detail callout (DC): a rectangle round the area. */
export const calloutTool: ToolDef = {
  id: TOOL_IDS.rect, label: 'Detail callout', icon: ScanSearch, shortcut: 'DC', tooltip,
  gesture: { kind: 'rectangle' },
  complete: ({ points }, ctx) => complete('rect', points, ctx),
  options: CalloutOptions,
};

/** The same with a circle: its centre, then a point on it. */
export const calloutCircleTool: ToolDef = {
  id: TOOL_IDS.circle, label: 'Round callout', icon: CircleDashed, tooltip,
  gesture: { kind: 'twoPoints' },
  complete: ({ points }, ctx) => complete('circle', points, ctx),
  options: CalloutOptions,
};

/** The same with a sketched outline: its corners, Enter closes it. */
export const calloutPolygonTool: ToolDef = {
  id: TOOL_IDS.polygon, label: 'Sketched callout', icon: Pentagon, tooltip,
  gesture: { kind: 'polygon' },
  complete: ({ points }, ctx) => complete('polygon', points, ctx),
  options: CalloutOptions,
};

/** The views a callout may refer to: every plan, section, elevation and reference plane's view but the one drawn on. */
export function referableViews(nodes: Map<string, NodeDto>, hostId: string | null): { id: string; name: string; kind: string }[] {
  return [...nodes.values()].filter((n) => n.type === 'LapinView' && n.id !== hostId && ['plan', 'section', 'elevation', 'plane'].includes(String(n.props.kind ?? 'plan')))
    .map((v) => ({ id: v.id, name: v.name ?? '', kind: String(v.props.kind ?? 'plan') }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/** The tool's bar: the boundary's shape, the detail's scale, and a view to refer to instead of making one. */
function CalloutOptions() {
  const tool = useUi((s) => s.tool);
  const { scale, reference } = useCalloutTool();
  const nodes = useSession((s) => s.nodes);
  const views = referableViews(nodes, null);
  const shapes: [CalloutShape, string, string][] = [
    ['rect', 'Rectangle', 'Two opposite corners'],
    ['circle', 'Circle', 'Its centre, then a point on it (or its radius typed)'],
    ['polygon', 'Polygon', 'Click the corners, then the first one again (or Enter)'],
  ];
  return (
    <>
      {shapes.map(([s, label, title]) => (
        <button key={s} type="button" className={`cad-toggle${tool === TOOL_IDS[s] ? ' on' : ''}`} data-callout-shape={s} title={title} onClick={() => setTool(TOOL_IDS[s])}>{label}</button>
      ))}
      <select className={`cad-layer${scale ? ' on' : ''}`} data-testid="callout-scale" value={scale ?? ''} disabled={!!reference}
        title="The detail view's scale; auto: 1:20, or the project's next standard scale under this view's" onChange={(e) => useCalloutTool.setState({ scale: Number(e.target.value) || null })}>
        <option value="">auto scale</option>
        {DETAIL_SCALES.map((s) => <option key={s} value={s} data-no-i18n>1:{s}</option>)}
      </select>
      <select className={`cad-layer${reference ? ' on' : ''}`} data-testid="callout-reference" value={reference ?? ''}
        title="A view that is there already (a drafting view, another detail): the callout points at it and makes none" onChange={(e) => useCalloutTool.setState({ reference: e.target.value || null })}>
        <option value="">a new detail view</option>
        {views.map((v) => <option key={v.id} value={v.id} data-no-i18n>{v.name} · {v.kind}</option>)}
      </select>
    </>
  );
}

/** What the engine drew a callout with: its head, the corner the head hangs off, and the view's turn. */
interface CalloutData { head?: number[]; anchor?: number[]; turn?: number }

/**
 * A callout's grips in the view it is drawn on: a rectangle's corners and edges, a circle's centre and radius, a polygon's
 * corners, a move grip in the middle, and the head (drag: move it off the boundary). Each one step, the detail cropped again.
 */
export function calloutGrips(node: NodeDto, view?: GripView, turn?: number): Grip[] {
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  const set = (args: Record<string, unknown>) => ({ command: 'view.updateCallout', args: { id: node.id, ...args } });
  const d = view?.mark?.id === node.id ? view.mark.data as CalloutData | undefined : undefined;
  const t = turn ?? d?.turn ?? (viewId ? turnOf(viewId) : 0);
  const toView = (p: Vec2) => rot(p, -t), toLocal = (p: Vec2) => rot(p, t);
  const grips: Grip[] = [];
  const shape = (node.props.shape ?? 'rect') as CalloutShape;
  if (shape === 'circle') {
    const c = pt(node.props.centre);
    const r = typeof node.props.radius === 'number' ? node.props.radius : null;
    if (!c || !r) return [];
    grips.push(
      { key: 'centre', kind: 'move', at: c, view: viewId, title: 'Drag: move the callout', step: (to) => set({ centre: arr(to) }) },
      { key: 'radius', kind: 'radius', at: { x: c.x + r, y: c.y }, view: viewId, title: 'Drag: its radius', step: (to) => set({ radius: Math.max(0.03, Math.hypot(to.x - c.x, to.y - c.y)) }) },
    );
  } else if (shape === 'polygon') {
    const pts = Array.isArray(node.props.pts) ? (node.props.pts as unknown[]).map(pt).filter((p): p is Vec2 => !!p) : [];
    if (pts.length < 3) return [];
    pts.forEach((p, i) => grips.push({ key: `pt-${i}`, kind: 'vertex', at: p, view: viewId, title: 'Drag: the corner', step: (to) => set({ pts: pts.map((q, k) => arr(k === i ? to : q)) }) }));
    const mid = { x: pts.reduce((s, p) => s + p.x, 0) / pts.length, y: pts.reduce((s, p) => s + p.y, 0) / pts.length };
    grips.push({ key: 'mid', kind: 'move', at: mid, view: viewId, title: 'Drag: move the callout', step: (to) => set({ pts: pts.map((q) => [q.x + to.x - mid.x, q.y + to.y - mid.y]) }) });
  } else {
    const lo = pt(node.props.min), hi = pt(node.props.max);
    if (!lo || !hi) return [];
    const [x0, x1, y0, y1] = [lo.x, hi.x, lo.y, hi.y];
    const rect = (min: Vec2, max: Vec2) => set({ min: [Math.min(min.x, max.x), Math.min(min.y, max.y)], max: [Math.max(min.x, max.x), Math.max(min.y, max.y)] });
    const g = (key: string, kind: Grip['kind'], at: Vec2, title: string, move: (to: Vec2) => [Vec2, Vec2]): Grip =>
      ({ key, kind, at: toView(at), view: viewId, title, step: (to) => rect(...move(toLocal(to))) });
    const mid = { x: (x0 + x1) / 2, y: (y0 + y1) / 2 };
    grips.push(
      g('sw', 'vertex', { x: x0, y: y0 }, 'Drag: the corner', (to) => [to, { x: x1, y: y1 }]),
      g('se', 'vertex', { x: x1, y: y0 }, 'Drag: the corner', (to) => [{ x: x0, y: to.y }, { x: to.x, y: y1 }]),
      g('ne', 'vertex', { x: x1, y: y1 }, 'Drag: the corner', (to) => [{ x: x0, y: y0 }, to]),
      g('nw', 'vertex', { x: x0, y: y1 }, 'Drag: the corner', (to) => [{ x: to.x, y: y0 }, { x: x1, y: to.y }]),
      g('s', 'edgeMid', { x: mid.x, y: y0 }, 'Drag: the edge', (to) => [{ x: x0, y: to.y }, { x: x1, y: y1 }]),
      g('e', 'edgeMid', { x: x1, y: mid.y }, 'Drag: the edge', (to) => [{ x: x0, y: y0 }, { x: to.x, y: y1 }]),
      g('n', 'edgeMid', { x: mid.x, y: y1 }, 'Drag: the edge', (to) => [{ x: x0, y: y0 }, { x: x1, y: to.y }]),
      g('w', 'edgeMid', { x: x0, y: mid.y }, 'Drag: the edge', (to) => [{ x: to.x, y: y0 }, { x: x1, y: y1 }]),
      g('mid', 'move', mid, 'Drag: move the callout', (to) => [{ x: x0 + to.x - mid.x, y: y0 + to.y - mid.y }, { x: x1 + to.x - mid.x, y: y1 + to.y - mid.y }]),
    );
  }
  // The head: where the engine drew it, moved as an offset from the boundary's upper-right corner.
  const head = pt(d?.head), anchor = pt(d?.anchor);
  if (head && anchor) grips.push({ key: 'head', kind: 'move', at: head, view: viewId, title: 'Drag: move the head', step: (to) => set({ head: [to.x - anchor.x, to.y - anchor.y] }) });
  return grips;
}

// Right-click on a selected callout: Open view.
registerDraftMenu((ids, nodes) => {
  const callout = ids.find((id) => nodes.get(id)?.type === 'LapinCallout');
  return callout ? [{ label: 'Open view', icon: <ExternalLink size={13} />, onSelect: () => void openCallout(callout) }] : [];
});

/** A callout's properties: the view it calls (open it), what it refers to, the detail's scale. */
export function CalloutSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const target = nodes.get(String(node.props.target ?? ''));
  const host = typeof node.props.viewId === 'string' ? node.props.viewId : null;
  const made = node.props.made === true;
  const base = useInheritedFrame('callout', !!target);
  return (
    <dl className="fields" data-testid="callout-section">
      <dt title={made ? 'The detail view the callout made' : 'The view the callout refers to'}>{made ? 'Detail view' : 'Refers to'}</dt>
      <dd>
        <span data-no-i18n>{target?.name ?? '?'}</span>{' '}
        <button type="button" className="icon mini" data-testid="callout-open" title="Open view" onClick={() => void openCallout(node.id)}><ExternalLink size={12} /></button>
      </dd>
      {made && target && (
        <>
          <dt title="The detail view's scale">Scale 1:</dt>
          <dd><NumberField testId="callout-detail-scale" value={typeof target.props.scale === 'number' ? target.props.scale : null} min={1} onCommit={(n) => { if (n != null && n >= 1) void execute('element.update', { ids: [target.id], attrs: { scale: n } }); }} /></dd>
        </>
      )}
      <dt title="Point the callout at a view that is there instead (the detail view it made stays)">Refer to</dt>
      <dd>
        <select data-testid="callout-refer" value={made ? '' : String(node.props.target ?? '')} onChange={(e) => { if (e.target.value) void execute('view.updateCallout', { id: node.id, reference: e.target.value }); }}>
          {made && <option value="">Its own detail view</option>}
          {referableViews(nodes, host).map((v) => <option key={v.id} value={v.id} data-no-i18n>{v.name} · {v.kind}</option>)}
        </select>
      </dd>
      {/* Its boundary's pen weight, colour and line type, its own over the drafting standards' (issue #73). */}
      <FrameLineFields field="callout-line" value={ownFrameLine(node.props)} base={base}
        onSet={(f, v) => void execute('view.updateCallout', { id: node.id, ...ownPatch(f, v) })} />
    </dl>
  );
}
