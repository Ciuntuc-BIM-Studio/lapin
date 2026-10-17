/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

/**
 * The selected wall's temporary dimensions in the plan: on each side of it,
 * the clear distance from its face to the facing face of the nearest wall
 * parallel to it that runs beside it (their lengths overlap), measured square
 * to the wall a third of the way along that overlap (clear of the handles at
 * the wall's middle). Face to face, never axis to
 * axis: a layered wall's axis is on its core (`wall-core-axis.ts`), not
 * between its faces.
 *
 * A dimension typed in moves the selected wall square to itself by the
 * difference (`moveWallAcross`): one undo step, the walls joined to it follow
 * (the selection transform's join carrier), its openings move with it.
 *
 * Shown with the plan's handles: one selected wall of the session model on
 * the plan's storey, in Select, on a model the user may edit.
 */

import { readWallJoinTarget, wallBodyLateralRange } from '@ifc-lite/create';
import type { ViewerState } from '@/store';
import { useViewerStore } from '@/store';
import { resolveEntityRef } from '@/store/resolveEntityRef';
import { canMutate } from '@/store/mutation-permission';
import { getModelLengthUnitScale } from '@/lib/length-unit-scale';
import { storeyWallAxes } from '@/lib/snap/sources/semantic-walls';
import type { Vec2 } from '@/lib/snap/types';
import type { Workplane } from '@/lib/commands/modeling/types';
import { buildStoreyWorkplane, elementStoreyId, isWorkplane } from '@/lib/commands/modeling/workplane';
import { runTransaction } from '@/lib/commands/modeling/transaction';
import type { ModelingCommand } from '@/lib/commands/modeling/types';
import { commitElementTransform } from '@/lib/element-transform/commit';
import { ensureEditMode } from '@/project/edit-mode';
import { toast } from '@/components/ui/toast';

export interface WallDimension {
  /** `left` or `right` of the wall's Start → End. */
  readonly side: 'left' | 'right';
  /** On the selected wall's face and on the neighbour's, plan-local metres. */
  readonly from: Vec2;
  readonly to: Vec2;
  /** The clear distance, metres. */
  readonly value: number;
  /** What a typed value moves: the wall, the way that widens the gap (storey plan, unit). */
  readonly modelId: string;
  readonly storeyId: number;
  readonly wall: number;
  readonly away: Vec2;
}

/** Two walls this close to parallel (sine of the angle between them) face each other. */
const PARALLEL = Math.sin((0.5 * Math.PI) / 180);
/** Lengths that overlap by less than this (metres) do not run beside each other. */
const MIN_OVERLAP = 0.01;
/** A gap this small or less is a touch (metres): no dimension. */
const MIN_GAP = 1e-4;

interface PlanWall { id: number; start: Vec2; end: Vec2; lateral: [number, number] }

function readWall(s: ViewerState, modelId: string, id: number): PlanWall | null {
  const dataStore = s.models.get(modelId)?.ifcDataStore;
  if (!dataStore) return null;
  const read = readWallJoinTarget(dataStore, s.mutationViews.get(modelId) ?? null, id, getModelLengthUnitScale(dataStore));
  if (!read) return null;
  const { start, end } = read.wall;
  return { id, start: [start[0], start[1]], end: [end[0], end[1]], lateral: wallBodyLateralRange(read.wall) };
}

/**
 * `wall`'s dimensions to its neighbours on `storeyId`, storey-local metres
 * (`from`/`to`): the nearest parallel wall beside it on each side, if any.
 */
export function wallNeighbourGaps(s: ViewerState, modelId: string, storeyId: number, wallId: number): Array<Omit<WallDimension, 'modelId' | 'storeyId' | 'wall'>> {
  const dataStore = s.models.get(modelId)?.ifcDataStore;
  const view = s.mutationViews.get(modelId);
  const self = readWall(s, modelId, wallId);
  if (!dataStore || !view || !self) return [];
  const length = Math.hypot(self.end[0] - self.start[0], self.end[1] - self.start[1]);
  if (length < MIN_OVERLAP) return [];
  const u: Vec2 = [(self.end[0] - self.start[0]) / length, (self.end[1] - self.start[1]) / length];
  const n: Vec2 = [-u[1], u[0]];
  const along = (p: Vec2) => (p[0] - self.start[0]) * u[0] + (p[1] - self.start[1]) * u[1];
  const across = (p: Vec2) => (p[0] - self.start[0]) * n[0] + (p[1] - self.start[1]) * n[1];
  const [lo, hi] = self.lateral;
  const best: Record<'left' | 'right', { gap: number; face: number; at: number } | null> = { left: null, right: null };
  const seen = new Set<number>([wallId]);
  for (const axis of storeyWallAxes(dataStore, view, storeyId)) {
    if (seen.has(axis.expressId)) continue;
    seen.add(axis.expressId);
    const other = readWall(s, modelId, axis.expressId);
    if (!other) continue;
    const len = Math.hypot(other.end[0] - other.start[0], other.end[1] - other.start[1]);
    if (len < MIN_OVERLAP) continue;
    const v: Vec2 = [(other.end[0] - other.start[0]) / len, (other.end[1] - other.start[1]) / len];
    if (Math.abs(u[0] * v[1] - u[1] * v[0]) > PARALLEL) continue;
    const [a, b] = [along(other.start), along(other.end)].sort((x, y) => x - y);
    const from = Math.max(0, a), to = Math.min(length, b);
    if (to - from < MIN_OVERLAP) continue;
    // Its body across the selected wall: its own lateral range, turned when it runs the other way.
    const t = across(other.start);
    const sense = u[0] * v[0] + u[1] * v[1] > 0 ? 1 : -1;
    const [wlo, whi] = [t + sense * other.lateral[0], t + sense * other.lateral[1]].sort((x, y) => x - y);
    const at = from + (to - from) / 3;
    if (wlo - hi > MIN_GAP && (!best.left || wlo - hi < best.left.gap)) best.left = { gap: wlo - hi, face: wlo, at };
    if (lo - whi > MIN_GAP && (!best.right || lo - whi < best.right.gap)) best.right = { gap: lo - whi, face: whi, at };
  }
  const point = (at: number, off: number): Vec2 => [self.start[0] + u[0] * at + n[0] * off, self.start[1] + u[1] * at + n[1] * off];
  const out: Array<Omit<WallDimension, 'modelId' | 'storeyId' | 'wall'>> = [];
  if (best.left) out.push({ side: 'left', from: point(best.left.at, hi), to: point(best.left.at, best.left.face), value: best.left.gap, away: [-n[0], -n[1]] });
  if (best.right) out.push({ side: 'right', from: point(best.right.at, lo), to: point(best.right.at, best.right.face), value: best.right.gap, away: n });
  return out;
}

/**
 * The selected wall's dimensions in the plan of `plane` (plan-local), or none:
 * one selected wall of the session model, on the session's storey, in Select,
 * on an editable model.
 */
export function selectedWallDimensions(s: ViewerState, plane: Workplane | null): WallDimension[] {
  const session = s.session;
  if (!plane || !session || session.storeyId === null || s.activeTool !== 'select' || s.selectedEntityId === null) return [];
  if (s.selectedEntityIds.size > 1 || (s.selectedEntityIds.size === 1 && !s.selectedEntityIds.has(s.selectedEntityId))) return [];
  const { modelId, expressId } = resolveEntityRef(s.selectedEntityId);
  if (modelId !== session.modelId || !s.models.has(modelId) || !canMutate(s, modelId)) return [];
  const storeyId = session.storeyId;
  if (elementStoreyId(s, modelId, expressId) !== storeyId || !s.readWallEndpoints(modelId, expressId)) return [];
  const storey = buildStoreyWorkplane(s, modelId, storeyId, 0);
  if (!isWorkplane(storey)) return [];
  const toPlan = (p: Vec2): Vec2 => {
    const local = plane.renderToLocal(storey.localToRender([p[0], p[1], 0]));
    return [local[0], local[1]];
  };
  return wallNeighbourGaps(s, modelId, storeyId, expressId).map((d) => ({ ...d, from: toPlan(d.from), to: toPlan(d.to), modelId, storeyId, wall: expressId }));
}

/** Move `dim`'s wall square to itself so that the gap becomes `value` metres: one undo step. False when refused. */
export function setWallDimension(dim: WallDimension, value: number): boolean {
  if (!(Number.isFinite(value) && value >= 0) || Math.abs(value - dim.value) < 1e-6) return false;
  ensureEditMode();
  const shift = value - dim.value;
  const get = useViewerStore.getState;
  const command: ModelingCommand = {
    id: 'wall.dimension',
    labelKey: 'planHandles.dimension.label',
    hud: {},
    snap: 'modeling',
    init: () => null,
    pointerMove: (g) => g,
    pointerDown: (g) => g,
    commit: (_g, tx) => {
      const storey = buildStoreyWorkplane(tx.store, dim.modelId, dim.storeyId, 0);
      if (!isWorkplane(storey)) throw new Error(storey.refused);
      return commitElementTransform(tx, dim.modelId, [dim.wall], {
        kind: 'move', from: storey.localToRender([0, 0, 0]), to: storey.localToRender([dim.away[0] * shift, dim.away[1] * shift, 0]),
      });
    },
  };
  const outcome = runTransaction(useViewerStore, command, null, { get, modelId: dim.modelId, storeyId: dim.storeyId, workplane: null });
  if (!outcome.ok) toast.error(outcome.reason);
  return outcome.ok;
}
