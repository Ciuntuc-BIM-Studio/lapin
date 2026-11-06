/**
 * Exhaust fans (report #55): the kinds and parameters the engine builds (its catalogs `mep.fanKinds`, `mep.fanParams`,
 * `mep.fanPresets`), the project's types of a kind, what each tool puts down, and a fan's grips.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../../engine/protocol';
import type { Grip, Vec2 } from '../../../platform/module';
import type { ParamDef, Spec } from '../../../shell/typeConfigurator';

export const CLASS = 'LapinExhaustFan';
export const TYPE_CLASS = 'LapinExhaustFanType';

export interface FanKind { key: string; label: string; predefined: string; mountings: string[]; defaults: Record<string, unknown> }
export interface FanPreset { key: string; name: string; kind: string; spec: Spec }
export type { ParamDef };

export const specOf = (t: NodeDto | undefined): Spec => (t?.props.spec as Spec | undefined) ?? {};
export const kindOf = (t: NodeDto | undefined) => String(specOf(t).kind ?? '');

/** The project's types of a kind, by name. */
export function typesOfKind(nodes: Map<string, NodeDto>, kind: string): NodeDto[] {
  return [...nodes.values()].filter((n) => n.type === TYPE_CLASS && kindOf(n) === kind).sort((a, b) => a.name.localeCompare(b.name));
}

/** A type in a few words: its duct, grille and airflow. */
export function fanSummary(t: NodeDto | undefined, kinds: FanKind[]): string {
  const s = specOf(t);
  const k = kinds.find((x) => x.key === s.kind);
  if (!k) return '';
  const v = (key: string) => s[key] ?? k.defaults[key];
  const face = Math.round(Number(v('faceSize') ?? 0) * 1000);
  const grille = v('grille') === 'square' ? `${face} × ${face}` : `Ø${face}`;
  return `Ø${v('diameter')} · ${grille} · ${v('airflow')} m³/h · ${v('mounting') ?? k.mountings[0]}`;
}

/** What the tools put down: a type per kind (a project type's id, or a built-in one's `preset:key`). */
interface Prefs { types: Record<string, string> }
export const useFanPrefs = create<Prefs>(() => ({ types: {} }));
export const chooseFanType = (kind: string, id: string) => useFanPrefs.setState((s) => ({ types: { ...s.types, [kind]: id } }));

/** The command's arguments for what a kind's tool puts down. */
export function typeArgs(kind: string, nodes: Map<string, NodeDto>): Record<string, unknown> {
  const chosen = useFanPrefs.getState().types[kind];
  if (chosen?.startsWith('preset:')) return { preset: chosen.slice(7) };
  return chosen && nodes.has(chosen) ? { typeId: chosen } : { kind };
}

/** Where the Duct to outlet tool's duct goes out: a hood on the wall, or a cowl through the roof. */
export const useFanDuct = create<{ roof: boolean }>(() => ({ roof: false }));

/** The types dialog's selection, set from outside (a fan's Edit type…). */
export const useFanTypesDialog = create<{ typeId: string | null }>(() => ({ typeId: null }));

/** A fan's grips: its point to move it, a handle in front of it to turn it (its front looks along its local −y). */
export function fanGrips(node: NodeDto): Grip[] {
  const p = node.props.at;
  if (!Array.isArray(p) || p.length < 2) return [];
  const at: Vec2 = { x: Number(p[0]), y: Number(p[1]) };
  const rotation = Number(node.props.rotation ?? 0);
  const rad = (rotation * Math.PI) / 180;
  const handle = { x: at.x + Math.sin(rad) * 0.45, y: at.y - Math.cos(rad) * 0.45 };
  return [
    { key: 'at', kind: 'move', at, title: 'Move', step: (to) => ({ command: 'element.transform', args: { ids: [node.id], op: 'move', by: [to.x - at.x, to.y - at.y] } }) },
    {
      key: 'rotate', kind: 'rotate', at: handle, title: 'Turn',
      step: (to) => {
        const facing = (Math.atan2(to.x - at.x, -(to.y - at.y)) * 180) / Math.PI;
        const angle = Math.round((((facing - rotation) % 360) + 540) % 360 - 180);
        return angle ? { command: 'element.transform', args: { ids: [node.id], op: 'rotate', center: [at.x, at.y], angle } } : null;
      },
    },
  ];
}
