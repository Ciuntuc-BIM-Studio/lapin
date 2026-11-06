/**
 * Elevators (docs/plan-elevators.md): the catalogue's styles (the engine's `transport.styles`), the project's own, what
 * the tool puts down, which elevator the configurator edits, an elevator's grips and its preview drawn as SVG.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, Vec2 } from '../../platform/module';

export const CLASS = 'IfcTransportElement';
export const TYPE_CLASS = 'IfcTransportElementType';
export const OWN_PREFIX = 'own:';

export interface ElevatorStyle { id: string; label: string; group: string; description: string; values: Record<string, unknown> }
/** A project style, kept on the project (`elevatorStyles`). */
export interface OwnStyle { id: string; label: string; kind: string; attrs: Record<string, unknown> }

export interface PreviewItem { kind: string; closed: boolean; points: number[] }
export interface SectionBox { role: string; y0: number; y1: number; z0: number; z1: number }
export interface Preview {
  problem?: string | null;
  attrs?: Record<string, unknown>;
  plan?: PreviewItem[];
  section?: { items: SectionBox[]; levels: { name: string; z: number; served: boolean; id: string }[]; top: number; bottom: number; width: number };
  summary?: [string, string][];
  checks?: string[];
  suggested?: { load: number; persons: number };
}

/** What the tool puts down: a style (the catalogue's or the project's) and a turn. */
interface Prefs { style: string; rotation: number }
export const useElevatorPrefs = create<Prefs>(() => ({ style: 'passenger-medium', rotation: 0 }));

/** The elevator the configurator edits. */
export const useElevatorDialog = create<{ id: string | null }>(() => ({ id: null }));

/** The styles in groups, in the catalogue's order. */
export function groupStyles(styles: ElevatorStyle[]): [string, ElevatorStyle[]][] {
  const groups = new Map<string, ElevatorStyle[]>();
  for (const s of styles) groups.set(s.group, [...(groups.get(s.group) ?? []), s]);
  return [...groups];
}

/** A style in a few words: persons, load, car in mm. */
export function styleAbout(values: Record<string, unknown>): string {
  const mm = (v: unknown) => Math.round(Number(v ?? 0) * 1000);
  return `${values.persons ?? '?'} p · ${values.load ?? '?'} kg · ${mm(values.cabinWidth)} × ${mm(values.cabinDepth)}`;
}

/** The storeys an elevator may stop on between two levels, lowest first. */
export function stopsBetween(storeys: NodeDto[], bottomId: string | undefined, topId: string | undefined): NodeDto[] {
  const level = (n?: NodeDto) => Number(n?.props.elevation ?? 0);
  const sorted = [...storeys].sort((a, b) => level(a) - level(b));
  const lo = level(sorted.find((s) => s.id === bottomId));
  const hi = level(sorted.find((s) => s.id === topId) ?? sorted.find((s) => s.id === bottomId));
  const [a, b] = lo <= hi ? [lo, hi] : [hi, lo];
  return sorted.filter((s) => level(s) >= a - 1e-6 && level(s) <= b + 1e-6);
}

/** The stops it skips, with one ticked or unticked (the lowest and highest are always served). */
export function toggleStop(unserved: string[], id: string, served: boolean): string[] {
  const rest = unserved.filter((x) => x !== id);
  return served ? rest : [...rest, id];
}

/** An elevator's grips: its middle to move it, a handle in front of its doors to turn it (its front looks along its local −y). */
export function elevatorGrips(node: NodeDto, reach = 1.5): Grip[] {
  const p = node.props.at;
  if (!Array.isArray(p) || p.length < 2) return [];
  const at: Vec2 = { x: Number(p[0]), y: Number(p[1]) };
  const rotation = Number(node.props.rotation ?? 0);
  const rad = (rotation * Math.PI) / 180;
  const handle = { x: at.x + Math.sin(rad) * reach, y: at.y - Math.cos(rad) * reach };
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

/** A plan item as an SVG path, y up. */
export function itemPath(i: PreviewItem): string {
  const pts: string[] = [];
  for (let k = 0; k + 1 < i.points.length; k += 2) pts.push(`${k ? 'L' : 'M'}${i.points[k].toFixed(4)} ${(-i.points[k + 1]).toFixed(4)}`);
  return pts.join(' ') + (i.closed ? ' Z' : '');
}

/** The plan's box (x, y, w, h in SVG terms, y down) with a margin. */
export function planBox(items: PreviewItem[], margin = 0.25): [number, number, number, number] {
  let [x0, y0, x1, y1] = [Infinity, Infinity, -Infinity, -Infinity];
  for (const i of items) for (let k = 0; k + 1 < i.points.length; k += 2) {
    x0 = Math.min(x0, i.points[k]); x1 = Math.max(x1, i.points[k]);
    y0 = Math.min(y0, -i.points[k + 1]); y1 = Math.max(y1, -i.points[k + 1]);
  }
  if (!Number.isFinite(x0)) return [-1, -1, 2, 2];
  return [x0 - margin, y0 - margin, x1 - x0 + 2 * margin, y1 - y0 + 2 * margin];
}
