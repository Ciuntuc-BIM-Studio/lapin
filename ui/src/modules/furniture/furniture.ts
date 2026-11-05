/**
 * Furniture (docs/plan-furniture.md): the kinds the engine builds (its catalog `furniture.kinds`), the types of a kind,
 * what each tool puts down (its type per kind, its turn, whether it backs onto walls), and a piece's grips.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, Vec2 } from '../../platform/module';

export interface KindParam { key: string; label: string; kind: 'length' | 'number' | 'choice' | 'bool'; min?: number | null; max?: number | null; choices?: string[] | null }
export interface FurnitureKind {
  key: string; label: string; group: string; predefined: string; objectType?: string | null;
  width: number; depth: number; height: number; params: KindParam[]; defaults: Record<string, unknown>; roles: string[];
}
export type FurnitureSpec = Record<string, unknown> & { kind?: string; width?: number; depth?: number; height?: number; materials?: Record<string, string> };

export const TYPE_CLASS = 'IfcFurnitureType';
export const CLASS = 'IfcFurniture';

/** The material roles, as the dialog names them. */
export const ROLE_LABELS: Record<string, string> = {
  body: 'Body', upholstery: 'Upholstery', top: 'Top', fronts: 'Fronts', linen: 'Linen', metal: 'Metal', glass: 'Glass',
};

export const specOf = (t: NodeDto | undefined): FurnitureSpec => (t?.props.spec as FurnitureSpec | undefined) ?? {};
export const kindOf = (t: NodeDto | undefined) => String(specOf(t).kind ?? '');

/** The project's types of a kind, by name. */
export function typesOfKind(nodes: Map<string, NodeDto>, kind: string): NodeDto[] {
  return [...nodes.values()].filter((n) => n.type === TYPE_CLASS && kindOf(n) === kind).sort((a, b) => a.name.localeCompare(b.name));
}

/** A type in a few words: its size in mm. */
export function sizeOf(t: NodeDto | undefined, kinds: FurnitureKind[]): string {
  const s = specOf(t);
  const k = kinds.find((x) => x.key === s.kind);
  const mm = (v: unknown, d?: number) => Math.round(Number(v ?? d ?? 0) * 1000);
  if (!k) return '';
  return s.kind === 'kitchen' ? `depth ${mm(s.depth, k.depth)} · worktop ${mm(s.height, k.height)}` : `${mm(s.width, k.width)} × ${mm(s.depth, k.depth)} × ${mm(s.height, k.height)}`;
}

/** What the tools put down: a type per kind (none: the project's first of the kind), the turn, backed onto walls or not. */
interface Prefs { types: Record<string, string>; rotation: number; against: boolean }
export const useFurniturePrefs = create<Prefs>(() => ({ types: {}, rotation: 0, against: true }));
export const chosenType = (kind: string) => useFurniturePrefs.getState().types[kind];
export const chooseType = (kind: string, id: string) => useFurniturePrefs.setState((s) => ({ types: { ...s.types, [kind]: id } }));

/** The types dialog's selection, set from outside (a piece's Edit type…). */
export const useTypesDialog = create<{ typeId: string | null }>(() => ({ typeId: null }));

/** A piece's grips: its middle to move it, a handle in front of it to turn it (its front looks along its local −y). */
export function furnitureGrips(node: NodeDto, depth = 0.8): Grip[] {
  const p = node.props.at;
  if (!Array.isArray(p) || p.length < 2) return [];
  const at: Vec2 = { x: Number(p[0]), y: Number(p[1]) };
  const rotation = Number(node.props.rotation ?? 0);
  const rad = (rotation * Math.PI) / 180;
  const reach = depth / 2 + 0.35;
  const handle = { x: at.x + Math.sin(rad) * reach, y: at.y - Math.cos(rad) * reach };
  return [
    { key: 'at', kind: 'move', at, title: 'Move', step: (to) => ({ command: 'element.transform', args: { ids: [node.id], op: 'move', by: [to.x - at.x, to.y - at.y] } }) },
    {
      key: 'rotate', kind: 'rotate', at: handle, title: 'Turn',
      step: (to) => {
        // The front towards the cursor: its local −y along (to − at).
        const facing = (Math.atan2(to.x - at.x, -(to.y - at.y)) * 180) / Math.PI;
        const angle = Math.round((((facing - rotation) % 360) + 540) % 360 - 180);
        return angle ? { command: 'element.transform', args: { ids: [node.id], op: 'rotate', center: [at.x, at.y], angle } } : null;
      },
    },
  ];
}

/** The preview's plan items and front pieces as SVG: the plan seen from above (y up), the front with z up. */
export interface PreviewDto {
  plan: { kind: string; closed: boolean; points: number[] }[];
  front: { role: string; color: string; x0: number; x1: number; z0: number; z1: number }[];
  width: number; depth: number; height: number;
}

export function planPaths(p: PreviewDto): { d: string; kind: string }[] {
  return p.plan.map((i) => {
    const pts: string[] = [];
    for (let k = 0; k + 1 < i.points.length; k += 2) pts.push(`${k ? 'L' : 'M'}${i.points[k].toFixed(4)} ${(-i.points[k + 1]).toFixed(4)}`);
    return { d: pts.join(' ') + (i.closed ? ' Z' : ''), kind: i.kind };
  });
}

/** The plan's box (x0, y0, w, h in SVG terms, y down) with a margin. */
export function planBox(p: PreviewDto, margin = 0.08): [number, number, number, number] {
  let [x0, y0, x1, y1] = [Infinity, Infinity, -Infinity, -Infinity];
  for (const i of p.plan) for (let k = 0; k + 1 < i.points.length; k += 2) {
    x0 = Math.min(x0, i.points[k]); x1 = Math.max(x1, i.points[k]);
    y0 = Math.min(y0, -i.points[k + 1]); y1 = Math.max(y1, -i.points[k + 1]);
  }
  if (!Number.isFinite(x0)) return [-1, -1, 2, 2];
  return [x0 - margin, y0 - margin, x1 - x0 + 2 * margin, y1 - y0 + 2 * margin];
}
