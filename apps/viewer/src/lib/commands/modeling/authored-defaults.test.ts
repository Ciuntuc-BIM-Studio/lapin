/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

/**
 * The Model inspector's per-kind defaults (charter #6232, M2.5): a type and
 * a layer set picked in "Defaults · Wall" land on every wall a command
 * authors, inside the command's own undo step, so one Ctrl+Z removes the
 * wall and its assignments together. Defaults are opt-in per commit
 * (`CommitResult.authored`): a split's pieces keep the source's type.
 */

import { afterEach, beforeEach, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { useViewerStore } from '@/store';
import { MODEL_ID, STOREY, seedModelingSession } from '@/test/modeling-session-fixture';
import { applyMaterialLayers, createElementType } from '@/components/viewer/model-inspector/inspector-edits';
import { readWallJoinTarget } from '@ifc-lite/create';
import { wallLayerOffset } from '@/lib/wall-core-axis';
import { layerSetOf, typeOf, type LiveModel } from './authored-kinds.js';
import { runTransaction, setRequestRemesh } from './transaction.js';
import type { CommandContext, ModelingCommand } from './types.js';

const s = () => useViewerStore.getState();
const live = (): LiveModel => ({ dataStore: s().models.get(MODEL_ID)!.ifcDataStore!, view: s().mutationViews.get(MODEL_ID) });
const undoDepth = () => s().undoStacks.get(MODEL_ID)?.length ?? 0;
const ctx: CommandContext = { get: s, modelId: MODEL_ID, storeyId: STOREY, workplane: null };

/** A command that adds one wall, taking the defaults when `authored`. */
function addWall(authored: boolean): { command: ModelingCommand; wall: () => number } {
  let wall = 0;
  const command: ModelingCommand = {
    id: 'test.wall', labelKey: 'modelingCommand.closeAria', hud: {}, snap: 'modeling',
    init: () => null, pointerMove: (g) => g, pointerDown: (g) => g,
    commit: (_g, tx) => {
      const added = tx.store.addWall(MODEL_ID, STOREY, { Start: [0, 0, 0], End: [4, 0, 0], Thickness: 0.2, Height: 3 });
      if (!('expressId' in added)) throw new Error('addWall refused');
      wall = added.expressId;
      return { created: [wall], ...(authored ? { authored: [wall] } : {}), deleted: [], remesh: [wall] };
    },
  };
  return { command, wall: () => wall };
}

let type = 0;
let layerSet = 0;
let restoreRemesh: () => void;
beforeEach(async () => {
  await seedModelingSession();
  restoreRemesh = setRequestRemesh(() => {});
  type = createElementType(MODEL_ID, 'wall', 'WT-300')!;
  layerSet = applyMaterialLayers(MODEL_ID, { kind: 'wall', target: 'element', typeId: null, layers: [{ thickness: 0.3, material: { name: 'Concrete' } }] })!;
  assert.ok(type > 0 && layerSet > 0);
  s().setAuthoringDefaults({
    typeIds: { wall: { modelId: MODEL_ID, expressId: type } },
    layerSetIds: { wall: { modelId: MODEL_ID, expressId: layerSet } },
  });
});
afterEach(() => {
  restoreRemesh();
  s().setAuthoringDefaults({ typeIds: {}, layerSetIds: {} });
});

describe('authored defaults (#6232 M2.5)', () => {
  it('a new wall takes the picked type and layer set in the same undo step', () => {
    const depth = undoDepth();
    const { command, wall } = addWall(true);
    assert.equal(runTransaction(useViewerStore, command, null, ctx).ok, true);
    assert.equal(typeOf(live(), wall()), type);
    const layers = layerSetOf(live(), wall());
    assert.equal(layers?.layerSetId, layerSet);
    assert.equal(layers?.via, 'element', 'through its own IfcMaterialLayerSetUsage');

    s().undo(MODEL_ID);
    assert.equal(undoDepth(), depth, 'one undo removes the wall and its assignments');
    assert.equal(s().readWallEndpoints(MODEL_ID, wall()), null);
    assert.equal(typeOf(live(), wall()), null);
  });

  it('a commit that does not name its elements as authored keeps them untouched', () => {
    const { command, wall } = addWall(false);
    assert.equal(runTransaction(useViewerStore, command, null, ctx).ok, true);
    assert.equal(typeOf(live(), wall()), null);
    assert.equal(layerSetOf(live(), wall()), null);
  });

  it('a pick whose entity was undone away is skipped', () => {
    s().undo(MODEL_ID); // the layer set
    s().undo(MODEL_ID); // the type
    const { command, wall } = addWall(true);
    assert.equal(runTransaction(useViewerStore, command, null, ctx).ok, true);
    assert.equal(typeOf(live(), wall()), null);
    assert.equal(layerSetOf(live(), wall()), null);
  });

  it("a layered wall's axis runs through its structural layer, not the middle of its finishes", () => {
    // Plaster 20 mm, brick 250 mm (the thickest: the core), insulation 100 mm, from the right face.
    const set = applyMaterialLayers(MODEL_ID, { kind: 'wall', target: 'element', typeId: null, layers: [0.02, 0.25, 0.1].map((thickness) => ({ thickness, material: null })) })!;
    s().setAuthoringDefaults({ typeIds: {}, layerSetIds: { wall: { modelId: MODEL_ID, expressId: set } } });
    const { command, wall } = addWall(true);
    assert.equal(runTransaction(useViewerStore, command, null, ctx).ok, true);
    const body = readWallJoinTarget(live().dataStore, live().view, wall(), 1)!.wall;
    assert.equal(+body.thickness.toFixed(6), 0.37, 'the wall is as thick as its layers');
    // Its faces: 20 + 125 mm right of the axis, 125 + 100 mm left of it.
    assert.equal(+(body.offset ?? 0).toFixed(6), 0.04, 'the body shifts 40 mm to the left of the axis');
    assert.equal(+wallLayerOffset([{ thickness: 0.02 }, { thickness: 0.25 }, { thickness: 0.1 }]).toFixed(6), -0.145, 'the layers start 145 mm right of the axis');
    assert.equal(wallLayerOffset([{ thickness: 0.02 }, { thickness: 0.1, core: true }, { thickness: 0.25 }]), -0.07, 'a marked core wins over the thickest');
  });
});
