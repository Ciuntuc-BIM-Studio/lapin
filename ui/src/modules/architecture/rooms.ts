/**
 * Rooms and their colour schemes, as the interface reads them
 * (docs/rooms.md): a room's outline and stamp point from its built geometry,
 * and the project's colour schemes with the values its rooms have.
 */

import { useCallback, useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import type { Grip, RefLine, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { outlineGrips, outlineRef, pt, toPairs } from '../../viewport/gripKit';
import { fromPairs } from '../../viewport/contour';

export interface SchemeEntry { value: string; color?: string; hatch?: string; hatchScale?: number; hatchColor?: string }
export interface ColorScheme { id: string; name: string; by: string; entries: SchemeEntry[] }
export interface SchemeValue { value: string; count: number; area: number }
export interface Schemes {
  schemes: ColorScheme[];
  fields: { key: string; label: string }[];
  values: Record<string, SchemeValue[]>;
  palette: string[];
  functions: string[];
  viewScheme: string | null;
  rooms: number;
}

/** The project's colour schemes, read again whenever the model changes. */
export function useSchemes(viewId?: string | null): [Schemes | null, () => void] {
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<Schemes | null>(null);
  const read = useCallback(() => void query<Schemes>('architecture.colorSchemes', viewId ? { viewId } : {}).then((r) => r && setS(r)), [viewId]);
  useEffect(read, [read, nodes]);
  return [s, read];
}

const pairs = (flat: number[]): Vec2[] => Array.from({ length: Math.floor(flat.length / 2) }, (_, i) => ({ x: flat[2 * i], y: flat[2 * i + 1] }));

/** A room's outline and stamp point as its geometry has them (they follow its walls), else as it stores them. */
export function roomShape(node: NodeDto): { outline: Vec2[]; stamp: Vec2 | null } {
  const item = useSession.getState().render.get(node.id);
  const outer = item?.plan.find((p) => p.kind === 'room');
  const stamp = item?.plan.find((p) => p.kind === 'roomStamp');
  return {
    outline: outer ? pairs(outer.points) : fromPairs(node.props.contour),
    stamp: stamp && stamp.points.length >= 2 ? { x: stamp.points[0], y: stamp.points[1] } : pt(node.props.stampAt),
  };
}

/**
 * A room's grips: its stamp, to drag where it reads best; a room found from
 * its walls, its point (dragged into another space, it finds that one); a
 * drawn room, its outline's corners and edges.
 */
export function roomGrips(node: NodeDto): Grip[] {
  const { outline, stamp } = roomShape(node);
  const grips: Grip[] = [];
  if (stamp) grips.push({ key: 'stamp', kind: 'move', at: stamp, title: 'Move the stamp', step: (to) => ({ command: 'architecture.updateRoom', args: { id: node.id, stampX: to.x, stampY: to.y } }) });
  const seed = pt(node.props.seed);
  if (node.props.auto && seed) grips.push({ key: 'seed', kind: 'centre', at: seed, title: 'The room\'s point: its outline is found round it', step: (to) => ({ command: 'architecture.updateRoom', args: { id: node.id, x: to.x, y: to.y } }) });
  if (!node.props.auto && outline.length >= 3) grips.push(...outlineGrips({ pts: outline }, (o) => ({ command: 'architecture.updateRoom', args: { id: node.id, points: toPairs(o.pts) } })));
  return grips;
}

export const roomRefs = (node: NodeDto): RefLine[] => {
  const { outline } = roomShape(node);
  return outline.length >= 3 ? [outlineRef(outline)] : [];
};

/** The value a room has for a scheme's field. */
export const valueOf = (node: NodeDto, by: string): string => (by === 'name' ? node.name : typeof node.props[by] === 'string' || typeof node.props[by] === 'number' ? String(node.props[by]).trim() : '');
