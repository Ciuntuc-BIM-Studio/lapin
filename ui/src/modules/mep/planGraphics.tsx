/**
 * MEP plans that read as MEP drawings (the engine draws them: colours and line types per service, overhead dashes, the
 * architecture halftone, tags): here what the interface adds — the line types a system's lines may take, Tag system on
 * the plan in front, and the coverage circles of the selected devices, drawn over the plan on the screen only when the
 * view or the system says "selected" (the default; "all" is in the engine's drawing and prints, "none" draws none).
 */

import { Tags, TableProperties } from 'lucide-react';
import { useLang } from '../../i18n';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import type { PlanOverlayProps, ToolDef } from '../../platform/module';
import { front2dView } from '../../viewport/viewTemporary';
import { editedSystem } from './edit';
import { useMep } from './prefs';
import { roles, services } from './network';

/** Lapin's own line types (Drafting standards → Line types adds the project's). */
export const LINE_TYPES = ['solid', 'dashed', 'hidden', 'center', 'dotted', 'dashdot', 'dashdotdot'] as const;

export type CoverageMode = 'all' | 'selected' | 'none';
export const COVERAGE_MODES: [CoverageMode, string][] = [['selected', 'on the selected devices'], ['all', 'every device (printed)'], ['none', 'none']];

const mode = (v: unknown): CoverageMode | null => (v === 'all' || v === 'selected' || v === 'none' ? v : null);

/** How a plan draws a system's coverage circles: the view's own, else the system's, else on the selected devices. */
export function coverageMode(view: NodeDto | undefined, system: NodeDto | undefined): CoverageMode {
  return mode(view?.props.mepCoverage) ?? mode(system?.props.coverage) ?? 'selected';
}

/** What a device covers (metres), as the engine's Nodes.Coverage: its own, a hydrant's hose and jet, else its role's; 0: none. */
export function coverageRadius(node: NodeDto, role: { coverage?: number | null; hoseLength?: number | null; jetLength?: number | null } | undefined): number {
  const n = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? v : null);
  const own = n(node.props.coverage);
  if (own != null) return Math.max(0, own);
  const hose = n(role?.hoseLength) ?? 0;
  if (hose > 0) return Math.max(0, (n(node.props.hoseLength) ?? hose) + (n(node.props.jetLength) ?? n(role?.jetLength) ?? 0));
  return Math.max(0, n(role?.coverage) ?? 0);
}

/** A colour paled towards white, as the engine pales a coverage circle's. */
export function pale(color: string, by = 0.65): string {
  if (!/^#[0-9a-f]{6}$/i.test(color)) return '#c8c8c8';
  const v = parseInt(color.slice(1), 16);
  const mix = (c: number) => Math.round(c + (255 - c) * by).toString(16).padStart(2, '0');
  return `#${mix((v >> 16) & 255)}${mix((v >> 8) & 255)}${mix(v & 255)}`;
}

/** A system's line colour: its own, else its service's default. */
export function lineColor(system: NodeDto | undefined): string {
  const own = (system?.props.style as { line?: { color?: string } } | undefined)?.line?.color;
  if (own) return own;
  const service = services().find((s) => s.id === system?.props.service) as { style?: { line?: { color?: string } } } | undefined;
  return service?.style?.line?.color ?? '#888888';
}

/** The selected devices' coverage circles on a plan whose setting is "selected": pale, thin, dashed, never picked. */
export function CoverageOverlay({ toScreen, scale, view }: PlanOverlayProps) {
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  if (!view.storeyId || selected.length === 0) return null;
  const viewNode = view.id ? nodes.get(view.id) : undefined;
  const circles: { x: number; y: number; r: number; color: string; id: string }[] = [];
  for (const id of selected) {
    const node = nodes.get(id);
    if (!node || typeof node.props.role !== 'string' || node.storeyId !== view.storeyId) continue;
    const system = nodes.get(String(node.props.systemId ?? ''));
    if (coverageMode(viewNode, system) !== 'selected') continue;
    const r = coverageRadius(node, roles().find((x) => x.id === node.props.role) as never);
    if (r <= 0) continue;
    const c = toScreen({ x: Number(node.props.x ?? 0), y: Number(node.props.y ?? 0) });
    circles.push({ x: c.x, y: c.y, r: r * scale, color: pale(lineColor(system)), id });
  }
  if (circles.length === 0) return null;
  return (
    <g className="mep-coverage" data-testid="mep-coverage" pointerEvents="none">
      {circles.map((c) => <circle key={c.id} cx={c.x} cy={c.y} r={c.r} fill="none" stroke={c.color} strokeWidth={1} strokeDasharray="6 4" />)}
    </g>
  );
}

/** Tag system: the plan in front's runs, rises and terminals of the system being edited (else the current one, else every one), in one step. */
export const tagSystem: ToolDef = {
  id: 'mep.tagSystem', label: 'Tag system', icon: Tags,
  run: () => {
    const viewId = front2dView();
    if (!viewId || useUi.getState().view !== 'plan') { notify('Open a plan to tag its systems.'); return; }
    const nodes = useSession.getState().nodes;
    const own = [editedSystem(), useMep.getState().systemId].find((id) => id && nodes.get(id)?.type === 'IfcDistributionSystem');
    void execute('mep.tagSystem', own ? { viewId, systemId: own } : { viewId }).then((r) => {
      const n = (r as { tagged?: number } | null)?.tagged;
      if (n != null) notify(n === 0 ? 'Everything on this plan has its tag already.' : `${n} tag(s) placed.`);
    });
  },
};

/**
 * The MEP legend: where its top-left corner goes, on a plan (listing that plan) or on a sheet (listing its views); the symbols and
 * line types they draw, live, its words in the interface's language (Properties: the view it lists, its title, its language).
 */
export const mepLegend: ToolDef = {
  id: 'mep.legend', label: 'MEP legend', icon: TableProperties,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    if (!ctx.viewId) { notify('Open a plan or a sheet to place the MEP legend.'); return; }
    await ctx.execute('sheet.addLegend', { viewId: ctx.viewId, at: [p.x, p.y], kind: 'mep', lang: useLang.getState().lang });
  },
};
