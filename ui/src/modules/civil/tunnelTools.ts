/**
 * The tunnel lining's tools (docs/plan-wing-walls.md, S4): opening its configurator, placing what it holds on an axis picked (a
 * road, a path, a wall, a track or a drafted line) between two stations, or along an axis drawn; a portal at one of its ends.
 */

import { Route, Spline } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute } from '../../engine/session';
import type { ToolDef } from '../../platform/module';
import { activate } from '../../platform/modules';
import { specOut } from './portal';
import { useTunnelDialog } from './tunnel';

/** The axes a lining can be taken from. */
export const TUNNEL_AXES = ['IfcPavement', 'LapinRetainingWall', 'LapinTrack', 'IfcEarthworksCut', 'IfcAnnotation'];

/** An axis picked where the lining starts, then a point where it ends (both stations on it, the nearest to each click). */
export const placeTunnelOnAxis: ToolDef = {
  id: 'civil.placeTunnelOnAxis', label: 'Place the lining on an axis', icon: Route,
  gesture: { kind: 'pickThenPoint', types: TUNNEL_AXES, hint: 'Click the axis where the lining starts, then where it ends' },
  complete: async ({ entity, points }, ctx) => {
    if (!entity || points.length < 2) return;
    const { spec } = useTunnelDialog.getState();
    const [a, b] = [points[0], points[points.length - 1]];
    const r = await ctx.execute('civil.addTunnelLining', { storeyId: ctx.storeyId, spec: specOut(spec), axis: { id: entity.id, from: [a.x, a.y], to: [b.x, b.y] } }) as { id?: string } | null;
    if (r?.id) { useUi.setState({ selection: r.id }); useTunnelDialog.setState({ id: r.id }); }
  },
};

/** An axis drawn for it: a polyline (its corners rounded later in the corridor configurator, by radii and clothoids). */
export const drawTunnelAxis: ToolDef = {
  id: 'civil.drawTunnelAxis', label: 'Draw the lining\'s axis', icon: Spline,
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const { spec } = useTunnelDialog.getState();
    const r = await ctx.execute('civil.addTunnelLining', { storeyId: ctx.storeyId, spec: specOut(spec), points: points.map((p) => [p.x, p.y]) }) as { id?: string } | null;
    if (r?.id) { useUi.setState({ selection: r.id }); useTunnelDialog.setState({ id: r.id }); }
  },
};

/** The lining the next „Link to an axis” pick links. */
let linking: string | null = null;

/** A lining linked to an axis picked: where it starts on it, then where it ends; it follows that element from then on. */
export const linkTunnelToAxis: ToolDef = {
  id: 'civil.linkTunnelToAxis', label: 'Link the lining to an axis', icon: Route,
  gesture: { kind: 'pickThenPoint', types: TUNNEL_AXES, hint: 'Click the axis where the lining starts, then where it ends' },
  complete: async ({ entity, points }, ctx) => {
    if (!entity || points.length < 2 || !linking) return;
    const [a, b] = [points[0], points[points.length - 1]];
    await ctx.execute('civil.updateTunnelLining', { id: linking, axis: { id: entity.id, from: [a.x, a.y], to: [b.x, b.y] } });
    useUi.setState({ selection: linking });
  },
};

/** Picks the axis a lining is linked to (the tool above). */
export function linkToAxis(id: string) {
  linking = id;
  activate(linkTunnelToAxis);
}

/** A lining unlinked from its source (a copy of its axis as it is), or linked again to the same element between the same stations. */
export const unlinkTunnel = (id: string) => execute('civil.unlinkTunnelLining', { id });
export const relinkTunnel = (id: string, source: { id: string; from: number; to: number }) =>
  execute('civil.updateTunnelLining', { id, axis: { id: source.id, from: source.from, to: source.to } });

/** Opens the configurator: on a lining made before (its spec), or on a new one. */
export function openTunnel(node?: { id: string; props: Record<string, unknown> }) {
  const spec = node && node.props.lining && typeof node.props.lining === 'object' ? { ...(node.props.lining as Record<string, unknown>) } : useTunnelDialog.getState().spec;
  useTunnelDialog.setState({ id: node?.id ?? null, spec });
  useUi.setState({ dialog: 'tunnel' });
}

/** A portal at one end of a lining (the portal configurator's flared preset, sized from the lining), selected and opened in its configurator. */
export async function portalAtEnd(id: string, end: 'start' | 'end') {
  const r = await execute('civil.tunnelPortal', { id, end }) as { id?: string } | null;
  if (r?.id) useUi.setState({ selection: r.id });
}
