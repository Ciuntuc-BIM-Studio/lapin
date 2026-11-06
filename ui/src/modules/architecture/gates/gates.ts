/**
 * Gates (report #57): the kinds, parameters and built-in styles the engine builds (its catalogs `architecture.gateKinds`,
 * `architecture.gateParams`, `architecture.gateStyles`), the project's types, what the tools put down, and a gate's grips.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../../engine/protocol';
import type { Grip, Vec2 } from '../../../platform/module';
import type { ParamDef, Spec } from '../../../shell/typeConfigurator';

export const CLASS = 'LapinGate';
export const TYPE_CLASS = 'LapinGateType';

export interface GateKind { key: string; label: string; operation: string; defaults: Record<string, unknown> }
export interface GateStyle { key: string; name: string; kind: string; spec: Spec }
export type { ParamDef };

/** The material roles, as the dialog names them. */
export const GATE_ROLES: Record<string, string> = { frame: 'Frame', infill: 'Infill', pier: 'Piers', cap: 'Caps', hardware: 'Hardware', motor: 'Motor' };

export const specOf = (t: NodeDto | undefined): Spec => (t?.props.spec as Spec | undefined) ?? {};
export const kindOf = (t: NodeDto | undefined) => String(specOf(t).kind ?? '');

/** A type in a few words: its clear size and leaves' infill. */
export function gateSummary(t: NodeDto | undefined, kinds: GateKind[]): string {
  const s = specOf(t);
  const k = kinds.find((x) => x.key === s.kind);
  if (!k) return '';
  const v = (key: string) => s[key] ?? k.defaults[key];
  const mm = (x: unknown) => Math.round(Number(x ?? 0) * 1000);
  const wicket = s.kind === 'swingWicket' ? ` + ${mm(v('wicketWidth'))}` : '';
  return `${mm(v('width'))}${wicket} × ${mm(v('height'))} · ${String(v('infill'))} · ${String(v('piers'))}${v('motor') === true ? ' · motor' : ''}`;
}

/** What the tools put down: a type (a project type's id, or a built-in style's `style:key`), and whether a click finds a line. */
interface Prefs { type: Record<string, string>; onLine: boolean }
export const useGatePrefs = create<Prefs>(() => ({ type: {}, onLine: true }));
export const chooseGateType = (kind: string, id: string) => useGatePrefs.setState((s) => ({ type: { ...s.type, [kind]: id } }));

export function gateTypeArgs(kind: string, nodes: Map<string, NodeDto>): Record<string, unknown> {
  const chosen = useGatePrefs.getState().type[kind];
  if (chosen?.startsWith('style:')) return { style: chosen.slice(6) };
  return chosen && nodes.has(chosen) ? { typeId: chosen } : { kind };
}

export const useGateTypesDialog = create<{ typeId: string | null }>(() => ({ typeId: null }));

const SWINGS = ['swing', 'doubleSwing', 'pedestrian', 'swingWicket', 'folding'];
const HANDED = ['swing', 'pedestrian', 'sliding', 'cantilever', 'swingWicket'];

/**
 * A gate's grips: its middle to move it, a handle beyond its end to turn it along a new line, and flip arrows — the
 * leaves to open to the other side, the hinges (or where it slides to) on the other hand.
 */
export function gateGrips(node: NodeDto, overall = 3): Grip[] {
  const p = node.props.at;
  if (!Array.isArray(p) || p.length < 2) return [];
  const at: Vec2 = { x: Number(p[0]), y: Number(p[1]) };
  const rotation = Number(node.props.rotation ?? 0);
  const rad = (rotation * Math.PI) / 180;
  const along = { x: Math.cos(rad), y: Math.sin(rad) };
  const inward = { x: -Math.sin(rad), y: Math.cos(rad) };
  const reach = overall / 2 + 0.4;
  const kind = String(node.props.gateKind ?? '');
  const swing = node.props.swing === 'out' ? -1 : 1;
  const hand = node.props.hand === 'left' ? -1 : 1;
  const grips: Grip[] = [
    { key: 'at', kind: 'move', at, title: 'Move', step: (to) => ({ command: 'element.transform', args: { ids: [node.id], op: 'move', by: [to.x - at.x, to.y - at.y] } }) },
    {
      key: 'rotate', kind: 'rotate', at: { x: at.x + along.x * reach, y: at.y + along.y * reach }, title: 'Turn',
      step: (to) => {
        const angle = Math.round(((((Math.atan2(to.y - at.y, to.x - at.x) * 180) / Math.PI - rotation) % 360) + 540) % 360 - 180);
        return angle ? { command: 'element.transform', args: { ids: [node.id], op: 'rotate', center: [at.x, at.y], angle } } : null;
      },
    },
  ];
  if (SWINGS.includes(kind) || kind === 'sliding' || kind === 'cantilever')
    grips.push({ key: 'swing', kind: 'flip', at: { x: at.x + inward.x * swing * 0.6, y: at.y + inward.y * swing * 0.6 }, dir: { x: inward.x * swing, y: inward.y * swing }, title: 'Open to the other side',
      click: () => ({ command: 'architecture.flipGate', args: { ids: [node.id], axis: 'swing' } }) });
  if (HANDED.includes(kind))
    grips.push({ key: 'hand', kind: 'flip', at: { x: at.x + along.x * hand * (overall / 2 - 0.2), y: at.y + along.y * hand * (overall / 2 - 0.2) }, dir: { x: along.x * hand, y: along.y * hand }, title: 'Hinges or parking on the other hand',
      click: () => ({ command: 'architecture.flipGate', args: { ids: [node.id], axis: 'hand' } }) });
  return grips;
}

/** A type's overall width (its piers' outer faces), for its grips. */
export function overallOf(t: NodeDto | undefined, kinds: GateKind[]): number {
  const s = specOf(t);
  const k = kinds.find((x) => x.key === s.kind);
  const v = (key: string) => Number(s[key] ?? k?.defaults[key] ?? 0);
  const piers = (s.piers ?? k?.defaults.piers) === 'none' ? 0 : v('pierSize');
  const wicket = s.kind === 'swingWicket' ? v('wicketWidth') + piers : 0;
  return v('width') + wicket + 2 * piers;
}
