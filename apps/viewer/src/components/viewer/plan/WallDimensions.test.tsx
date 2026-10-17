/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

/**
 * A selected wall's temporary dimensions in the plan: the clear distance,
 * face to face, to the nearest parallel wall beside it on each side (a wall
 * across it or past its ends is not one); typing one moves the wall square to
 * itself in one undo step.
 */

import '@/test/setup-dom.js';
import { installLayout } from '@/test/dom-layout.js';
installLayout();
import { afterEach, beforeEach, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { act } from 'react';
import { useViewerStore } from '@/store';
import { toGlobalIdFromModels } from '@/store/globalId';
import { cleanup, render } from '@/test/render.js';
import { MODEL_ID, STOREY, seedModelingSession } from '@/test/modeling-session-fixture';
import '@/lib/commands/modeling/builtin';
import { setRequestRemesh } from '@/lib/commands/modeling/transaction';
import { PLAN_CUT_DEBOUNCE_MS } from './usePlanCut';
import { PlanView } from './PlanView';
import { wallNeighbourGaps } from './wall-dimensions';

const wait = (ms: number) => new Promise((r) => setTimeout(r, ms));
const s = () => useViewerStore.getState();
const round = (v: number) => +v.toFixed(6);

let restoreRemesh: () => void = () => {};
let wall = 0;

function addWall(start: [number, number], end: [number, number], thickness = 0.2, offset?: number): number {
  const added = s().addWall(MODEL_ID, STOREY, { Start: [...start, 0], End: [...end, 0], Thickness: thickness, Height: 3, ...(offset !== undefined ? { Offset: offset } : {}) });
  assert.ok('expressId' in added);
  return added.expressId;
}

beforeEach(async () => {
  await seedModelingSession();
  useViewerStore.setState({ snapEnabled: false, selectedEntityId: null, selectedEntityIds: new Set(), selectedEntity: null });
  assert.ok(s().enterModelWorkspace());
  restoreRemesh = setRequestRemesh(() => {});
  wall = addWall([0, 0], [4, 0]);
});
afterEach(() => {
  restoreRemesh();
  s().exitModelWorkspace();
  cleanup();
});

describe('wall temporary dimensions', () => {
  it('measure face to face to the nearest parallel wall beside it on each side', () => {
    addWall([1, 2], [6, 2]); // left, 1.8 m clear
    addWall([0, 3], [4, 3]); // left too, further
    addWall([4, -1.5], [0, -1.5], 0.3, 0.05); // right, drawn the other way, its body shifted: faces at -1.7 and -1.4 (its left is our right)
    addWall([5, -3], [5, 3]); // across: not parallel
    addWall([6, 1], [9, 1]); // parallel but past the end
    const gaps = wallNeighbourGaps(s(), MODEL_ID, STOREY, wall);
    const left = gaps.find((g) => g.side === 'left')!, right = gaps.find((g) => g.side === 'right')!;
    assert.equal(round(left.value), 1.8);
    assert.deepEqual(left.from.map(round), [2, 0.1], 'a third along the overlap, on our face');
    assert.deepEqual(left.to.map(round), [2, 1.9], "on the neighbour's facing face");
    assert.equal(round(right.value), 1.3, 'to the face of the shifted body, not its axis');
    assert.equal(gaps.length, 2);
  });

  it('typing a distance moves the selected wall to it, in one undo step', async () => {
    addWall([0, 2], [4, 2]);
    const ui = render(<PlanView layout="split" />);
    await wait(PLAN_CUT_DEBOUNCE_MS + 250);
    act(() => s().setSelectedEntityId(toGlobalIdFromModels(s().models, MODEL_ID, wall)));
    const label = ui.querySelector('[data-wall-dimension="left"]') as HTMLButtonElement;
    assert.ok(label, 'the selected wall shows its dimension');
    assert.equal(label.textContent, '1800');
    act(() => label.click());
    const input = ui.querySelector('[data-wall-dimension-input="left"]') as HTMLInputElement;
    assert.ok(input, 'a click edits it');
    const before = s().undoStacks.get(MODEL_ID)?.length ?? 0;
    act(() => {
      input.value = '1500';
      input.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    });
    const moved = s().readWallEndpoints(MODEL_ID, wall)!;
    assert.deepEqual([moved.start[1], moved.end[1]].map(round), [0.3, 0.3], 'the wall moved 300 mm towards its neighbour');
    const added = (s().undoStacks.get(MODEL_ID) ?? []).slice(before);
    assert.equal(new Set(added.map((m) => s().mutationBatchTags.get(m.id))).size, 1, 'one undo step');
    assert.equal(ui.querySelector('[data-wall-dimension="left"]')?.textContent, '1500', 'the dimension reads the new distance');
  });
});
