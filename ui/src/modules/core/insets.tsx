/**
 * Inset views (decision 574; rebar's, decision 211, made general): any plan, section, elevation or reference plane's view drawn
 * inside another 2D view, where it was clicked (View → Inset view, IV), live: the engine draws the source as it is now
 * (`view.inset`, Insets.cs), at the source's scale or one of its own, cut to a crop of its own, framed with its name and scale.
 * What is drawn over it goes on the source (viewport/insetMap.ts), and what the source has of its own is edited through it.
 * Its grips: its point (drag to move it) and, when it is cropped, its crop's corners.
 */

import { PictureInPicture2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import type { Grip, GripView, ToolDef, Vec2 } from '../../platform/module';
import { NumberField } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { mapper } from '../../viewport/insetMap';

/** Another view drawn in this one: click where it goes, then choose it. */
export const insetTool: ToolDef = {
  id: 'view.inset', label: 'Inset view', icon: PictureInPicture2, shortcut: 'IV',
  tooltip: 'Another view drawn in this one, live, at its own scale: what you draw over it goes on that view',
  gesture: { kind: 'point' },
  complete: ({ points: [p] }, ctx) => { useUi.setState({ dialog: 'insetPick', insetAt: { viewId: ctx.viewId, x: p.x, y: p.y } }); },
};

/** What the engine drew an inset with: its point, its mapping, its scale and its crop. */
interface InsetData { at?: number[]; centre?: number[]; factor?: number; scale?: number; sourceScale?: number; crop?: { min?: number[]; max?: number[] } }

/** The views an inset may show on a host: every plan, section, elevation and reference plane's view, and the levels' own plans, the host left out. */
export function insetSources(nodes: Map<string, NodeDto>, hostId: string): { id: string; name: string; kind: string }[] {
  const views = [...nodes.values()].filter((n) => n.type === 'LapinView' && ['section', 'elevation', 'plan', 'plane'].includes(String(n.props.kind ?? 'plan')) && n.id !== hostId)
    .map((v) => ({ id: v.id, name: v.name ?? '', kind: String(v.props.kind ?? 'plan') }));
  // A level whose plan is not a view of its own yet: its default plan.
  const planned = new Set([...nodes.values()].filter((n) => n.type === 'LapinView' && (n.props.kind ?? 'plan') === 'plan').map((n) => String(n.props.storeyId ?? '')));
  const storeys = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey' && !planned.has(n.id) && `plan:${n.id}` !== hostId)
    .map((s) => ({ id: `plan:${s.id}`, name: s.name ?? '', kind: 'plan' }));
  return [...views, ...storeys];
}

/** An inset's point (drag: move it) and, cropped, its crop's corners (drag: crop it more or less). */
export function insetGrips(node: NodeDto, view?: GripView): Grip[] {
  const at = pt(node.props.at);
  if (!at) return [];
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  const grips: Grip[] = [{ key: 'at', kind: 'move', at, view: viewId, title: 'Drag: move the inset', step: (to: Vec2) => ({ command: 'view.updateInset', args: { id: node.id, at: [to.x, to.y] } }) }];
  const d = view?.mark?.id === node.id ? view.mark.data as InsetData | undefined : undefined;
  const lo = pt(d?.crop?.min), hi = pt(d?.crop?.max);
  if (d?.at && d.centre && d.factor && lo && hi) {
    const { show, back } = mapper({ at: d.at, centre: d.centre, factor: d.factor });
    const set = (a: Vec2, b: Vec2) => ({ command: 'view.updateInset', args: { id: node.id, crop: { min: [a.x, a.y], max: [b.x, b.y] } } });
    const corner = (key: string, p: Vec2, move: (q: Vec2) => [Vec2, Vec2]): Grip =>
      ({ key: `crop-${key}`, kind: 'vertex', at: show(p), view: viewId, title: "Drag: the crop's corner", step: (to) => set(...move(back(to))) });
    grips.push(
      corner('sw', lo, (q) => [q, hi]),
      corner('se', { x: hi.x, y: lo.y }, (q) => [{ x: lo.x, y: q.y }, { x: q.x, y: hi.y }]),
      corner('ne', hi, (q) => [lo, q]),
      corner('nw', { x: lo.x, y: hi.y }, (q) => [{ x: q.x, y: lo.y }, { x: hi.x, y: q.y }]),
    );
  }
  return grips;
}

/** An inset's properties: the view it shows, its scale (the source's unless one is typed), its frame and its crop. */
export function InsetSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const hostId = String(node.props.viewId ?? '');
  const source = String(node.props.source ?? '');
  const choices = insetSources(nodes, hostId);
  const set = (patch: Record<string, unknown>) => void execute('view.updateInset', { id: node.id, ...patch });
  const scale = typeof node.props.scale === 'number' ? node.props.scale : null;
  const sourceScale = source.startsWith('plan:') ? null : nodes.get(source)?.props.scale;
  const factor = typeof node.props.factor === 'number' ? node.props.factor : null;
  return (
    <dl className="fields" data-testid="inset-section">
      <dt>View shown</dt>
      <dd>
        <select data-testid="inset-source" value={source} onChange={(e) => set({ source: e.target.value })}>
          {!choices.some((c) => c.id === source) && <option value={source} data-no-i18n>{source}</option>}
          {choices.map((c) => <option key={c.id} value={c.id} data-no-i18n>{c.name} · {c.kind}</option>)}
        </select>
      </dd>
      <dt title="The inset's own scale on paper; empty: the shown view's own, so it is the size it has on its sheet">Scale 1:</dt>
      <dd>
        <NumberField testId="inset-scale" value={scale} min={1} placeholder={typeof sourceScale === 'number' ? String(sourceScale) : ''}
          onCommit={(n) => set({ scale: n != null && n >= 1 ? n : null, factor: null })} />
        {factor != null && <span className="muted small"> × {Math.round(factor * 1000) / 1000}</span>}
      </dd>
      <dt title="A thin frame round it, with the shown view's name and scale under it">Framed</dt>
      <dd><input type="checkbox" data-testid="inset-frame" checked={node.props.frame !== false} onChange={(e) => set({ frame: e.target.checked })} /></dd>
      <dt title="Only a part of the shown view: drag the crop's corners in the view; off, the whole view">Crop</dt>
      <dd><input type="checkbox" data-testid="inset-crop" checked={!!node.props.crop} onChange={(e) => set({ crop: e.target.checked })} /></dd>
    </dl>
  );
}
