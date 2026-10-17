/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

/**
 * A layered wall's axis runs through the middle of its structural layer (its
 * core: the layer marked so, else the thickest), not through the middle of
 * the build-up: the finishes on either face do not move it. The body sits
 * off the axis by `bodyOffset` and the first layer starts `layerOffset` from
 * it (`stemSection`, the rule a strip foundation's stem follows). A wall with
 * no layers keeps its body centred.
 */

import { stemSection, type CoreLayer } from '@ifc-lite/create';
import type { ModellingStore } from '@/store/slices/mutation-modelling-records';
import { setWallSection } from '@/store/slices/mutation-wall-section';

/** `OffsetFromReferenceLine` of a wall's layer set usage: the first layer's start, from the core's middle. */
export function wallLayerOffset(layers: readonly CoreLayer[]): number {
  return layers.length ? stemSection(layers).layerOffset : 0;
}

/** `layers` (thicknesses, first to last) with layer `core` marked as the structural one. */
export function markCore(thicknesses: readonly number[], core: number | null | undefined): CoreLayer[] {
  return thicknesses.map((thickness, i) => (i === core ? { thickness, core: true } : { thickness }));
}

/**
 * Put wall `wallId`'s axis on its core: its body takes the layers' thickness
 * and shifts across the axis (its openings follow). The ids to re-mesh;
 * throws when the wall's body cannot be rewritten.
 */
export function alignWallOnCore(store: ModellingStore, modelId: string, wallId: number, layers: readonly CoreLayer[]): number[] {
  if (!layers.length) return [];
  const { thickness, bodyOffset } = stemSection(layers);
  if (!(thickness > 0)) return [];
  const outcome = setWallSection(store, modelId, wallId, { thickness, offset: bodyOffset });
  if (!outcome.ok) throw new Error(outcome.reason);
  return outcome.remesh;
}
