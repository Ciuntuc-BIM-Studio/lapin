/**
 * Cable carriers (report #147): trays, ladders, trunking and conduit drawn as runs are, legs at 45° steps. What the
 * tools share: the engine's catalogs (`mep.carrierKinds`, `mep.carrierPresets`, indicative), the project's types, the
 * type the tool puts down and its elevation, where a new carrier's end lands on another, the fill as Properties says it.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../../engine/protocol';
import { useSession } from '../../../engine/session';
import type { Vec2 } from '../../../platform/module';

export const CLASS = 'IfcCableCarrierSegment';
export const TYPE_CLASS = 'IfcCableCarrierSegmentType';

export interface CarrierKind { key: string; label: string; predefined: string; fillLimit: number; bendRadius: number }
export interface CarrierPreset { key: string; name: string; kind: string; width: number; height: number; diameter: number; indicative: boolean }

const catalog = <T,>(name: string): T[] => (useSession.getState().info?.catalogs[`mep.${name}`] as unknown as T[] | undefined) ?? [];
export const carrierKinds = (): CarrierKind[] => catalog<CarrierKind>('carrierKinds');
export const carrierPresets = (): CarrierPreset[] => catalog<CarrierPreset>('carrierPresets');

export const isCarrier = (n: NodeDto): boolean => n.type === CLASS && Array.isArray(n.props.points);

/** The project's carrier types, by name. */
export const carrierTypes = (nodes: Map<string, NodeDto>): NodeDto[] =>
  [...nodes.values()].filter((n) => n.type === TYPE_CLASS).sort((a, b) => a.name.localeCompare(b.name));

/** What the carrier tool puts down (a project type's id, or a built-in one's `preset:key`) and at what elevation (m). */
interface Prefs { type: string; elevation: number }
export const useCarrierPrefs = create<Prefs>(() => ({ type: 'preset:tray200x60', elevation: 2.4 }));

/** The draw command's arguments for the chosen type. */
export function typeArg(choice: string): Record<string, string> {
  if (choice.startsWith('preset:')) return { preset: choice.slice(7) };
  if (choice.startsWith('kind:')) return { kind: choice.slice(5) };
  return choice ? { typeId: choice } : { preset: 'tray200x60' };
}

/** The next point of a carrier: its leg at the nearest 45° step from the last point, as long as the aimed leg is along it. */
export function step45(placed: Vec2[], aimed: Vec2): Vec2 {
  const last = placed[placed.length - 1];
  if (!last) return aimed;
  const dx = aimed.x - last.x, dy = aimed.y - last.y;
  if (Math.hypot(dx, dy) < 1e-12) return aimed;
  const a = Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
  const ux = Math.cos(a), uy = Math.sin(a);
  const l = dx * ux + dy * uy;
  const r = (v: number) => Math.round(v * 1e6) / 1e6;
  return { x: r(last.x + ux * l), y: r(last.y + uy * l) };
}

export const carrierPoints = (n: NodeDto): Vec2[] =>
  ((n.props.points as unknown as number[][] | undefined) ?? []).filter((p) => Array.isArray(p) && p.length >= 2).map((p) => ({ x: Number(p[0]), y: Number(p[1]) }));

function distToSegment(p: Vec2, a: Vec2, b: Vec2): number {
  const dx = b.x - a.x, dy = b.y - a.y;
  const l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
  return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy));
}

/** What a new carrier's end at a point joins: the carrier of the storey passing nearest within `tol` (a tee inside it, end to end at its end). */
export function carrierEnd(carriers: NodeDto[], p: Vec2, tol = 0.3, storeyId?: string | null): { carrierId: string; at: [number, number] } | undefined {
  let best: NodeDto | null = null, bd = tol;
  for (const c of carriers) {
    if (storeyId && c.storeyId && c.storeyId !== storeyId) continue;
    const pts = carrierPoints(c);
    for (let i = 0; i + 1 < pts.length; i++) {
      const d = distToSegment(p, pts[i], pts[i + 1]);
      if (d < bd) { bd = d; best = c; }
    }
  }
  return best ? { carrierId: best.id, at: [p.x, p.y] } : undefined;
}

/** A carrier's size as its type writes it: 200×60, or Ø20 for conduit. */
export function sizeLabel(kind: string, width: number, height: number, diameter: number): string {
  const mm = (v: number) => Math.round(v * 1000);
  return kind === 'conduit' ? `Ø${mm(diameter)}` : `${mm(width)}×${mm(height)}`;
}

export interface CarrierFill { percent: number; limit: number; over: boolean; count: number; cableIds: string[]; size: string; kind: string }

/** The fill as Properties shows it: "38.2 % of 45 % · 3 cables", or none while no cable lies in it. */
export function fillText(f: CarrierFill | null): string {
  if (!f || f.count === 0) return 'no cables';
  return `${f.percent.toFixed(1)} % of ${f.limit} % · ${f.count} cables`;
}
