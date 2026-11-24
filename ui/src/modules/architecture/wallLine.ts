/**
 * The line a wall is drawn on, as the Wall tool shows it before it is built (decision 692): the type's layers laid out across
 * the axis as the engine lays out a new wall's (the first layer, its exterior, to the right; the axis on the core's centre),
 * where the line clicked lies among them (the engine's WallReference.LeftOf for a wall not flipped), what Space makes of it
 * (the same line on the other side: the exterior face and the interior face swap, so do the core's faces, an offset changes
 * sign; the core's centre has no other side), and the ghost of the wall along a path drawn. Pure.
 */

import type { Vec2 } from '../../platform/module';
import { axisLine, type Arc } from '../../viewport/arc';
import { offsetArc } from '../../viewport/wallReference';
import type { WallLine } from './prefs';

/** A layer across the wall: from `from` to `to` metres left of the axis. */
export interface Band { from: number; to: number; core: boolean }

/** The layers as a new wall has them (exterior first, to the right; the core: the one marked, else the thickest); none: `thickness`, centred. */
export function bandsOf(layers: { thickness: number; core?: boolean }[], thickness = 0.25): Band[] {
  if (!layers.length) return [{ from: -thickness / 2, to: thickness / 2, core: true }];
  let core = layers.findIndex((l) => l.core);
  if (core < 0) core = layers.reduce((best, l, i) => (l.thickness > layers[best].thickness ? i : best), 0);
  let at = -(layers.slice(0, core).reduce((s, l) => s + l.thickness, 0) + layers[core].thickness / 2);
  return layers.map((l, i) => { const from = at; at += l.thickness; return { from, to: at, core: i === core }; });
}

/** The layers mirrored across the axis, as a flipped wall has them (the exterior to the left; forum topic 238). */
export const mirrorBands = (bands: Band[]): Band[] => bands.map((b) => ({ ...b, from: -b.to, to: -b.from })).reverse();

/** How far left of the axis the line clicked lies. */
export function lineLeft(bands: Band[], line: WallLine, offset: number): number {
  if (line === 'offset') return offset;
  if (line === 'core' || !bands.length) return 0;
  const core = bands.find((b) => b.core) ?? bands[0];
  const lo = Math.min(...bands.map((b) => b.from)), hi = Math.max(...bands.map((b) => b.to));
  switch (line) {
    case 'coreExterior': return core.from;
    case 'coreInterior': return core.to;
    case 'exterior': return lo;
    case 'interior': return hi;
  }
}

const MIRROR: Partial<Record<WallLine, WallLine>> = { exterior: 'interior', interior: 'exterior', coreExterior: 'coreInterior', coreInterior: 'coreExterior' };

/** Space: the line on the other side of the wall; null for the core's centre, which has none. */
export function mirrorLine(line: WallLine, lineOffset: number): { line: WallLine; lineOffset: number } | null {
  if (line === 'offset') return { line, lineOffset: lineOffset === 0 ? 0 : -lineOffset };
  const other = MIRROR[line];
  return other ? { line: other, lineOffset } : null;
}

/** The wall's ghost along `path` drawn on a line `left` of its axis: its outline (closed), and its core's faces when the core is not the whole wall. */
export function wallGhost(path: Arc, bands: Band[], left: number): Vec2[][] {
  if (!bands.length) return [];
  const lo = Math.min(...bands.map((b) => b.from)), hi = Math.max(...bands.map((b) => b.to));
  const at = (y: number) => axisLine(offsetArc(path, y - left));
  const right = at(lo), leftFace = at(hi);
  const outline = [...right, ...[...leftFace].reverse(), right[0]];
  const core = bands.find((b) => b.core);
  const lines = [outline];
  if (core && (core.from > lo + 1e-6 || core.to < hi - 1e-6)) lines.push(at(core.from), at(core.to));
  return lines;
}
