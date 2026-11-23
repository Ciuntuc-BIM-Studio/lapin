/**
 * The portal configurator's tools (docs/plan-wing-walls.md, S3): opening it, placing what it holds by two points or on a road,
 * drawing its path. Kept apart from the dialog, which loads when it opens.
 */

import { Landmark, Route, Spline } from 'lucide-react';
import { useUi } from '../../app/store';
import type { ToolDef } from '../../platform/module';
import { specOut, usePortalDialog, type PortalSpec } from './portal';

/** The configurator's placing tools: two points (the face wall's middle, then the way the wings go), or a road picked. */
export const placePortal: ToolDef = {
  id: 'civil.placePortal', label: 'Place portal', icon: Landmark,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => {
    const { spec } = usePortalDialog.getState();
    const r = await ctx.execute('civil.addPortal', { storeyId: ctx.storeyId, spec: specOut(spec), origin: [a.x, a.y], towards: [b.x, b.y] }) as { id?: string } | null;
    if (r?.id) { useUi.setState({ selection: r.id }); usePortalDialog.setState({ id: r.id }); }
  },
};

export const placePortalOnRoad: ToolDef = {
  id: 'civil.placePortalOnRoad', label: 'Place portal on a road', icon: Route,
  gesture: { kind: 'pick', types: ['IfcPavement', 'LapinRetainingWall', 'IfcAnnotation'], reach: 12 },
  complete: async ({ entity, points }, ctx) => {
    if (!entity || !points.length) return;
    const { spec } = usePortalDialog.getState();
    const at = points[points.length - 1];
    const r = await ctx.execute('civil.addPortal', { storeyId: ctx.storeyId, spec: specOut(spec), axis: { id: entity.id, at: [at.x, at.y], reverse: spec.reverse === true } }) as { id?: string } | null;
    if (r?.id) { useUi.setState({ selection: r.id }); usePortalDialog.setState({ id: r.id }); }
  },
};

/** A path drawn for it: the polyline's points become the spec's path (the first and last segments its wings), then the configurator again. */
export const drawPortalPath: ToolDef = {
  id: 'civil.drawPortalPath', label: 'Draw the portal\'s path', icon: Spline,
  gesture: { kind: 'polyline' },
  complete: async ({ points }) => {
    if (points.length < 2) return;
    const s = usePortalDialog.getState();
    usePortalDialog.setState({ spec: { ...s.spec, path: points.map((p) => [p.x, p.y]) } });
    useUi.setState({ dialog: 'portal', tool: null });
  },
};

/** Opens the configurator: on a wall it made (its spec), or on a new one. */
export function openPortal(node?: { id: string; props: Record<string, unknown> }) {
  const spec = node && node.props.portal && typeof node.props.portal === 'object' ? { ...(node.props.portal as PortalSpec) } : usePortalDialog.getState().spec;
  usePortalDialog.setState({ id: node?.id ?? null, spec });
  useUi.setState({ dialog: 'portal' });
}
