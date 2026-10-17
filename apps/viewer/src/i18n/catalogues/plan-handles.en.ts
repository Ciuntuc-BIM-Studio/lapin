/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

import type { TranslationValue } from '../types';

/**
 * Direct-edit handles in the Model workspace's plan (charter #6232, B3):
 * sliding a door, window or opening along its wall, and moving the selection.
 */
export const planHandlesEn = {
  'planHandles.slide.label': 'Slide along wall',
  'planHandles.slide.hint': 'Drag along the wall · release to place · Esc cancels',
  'planHandles.slide.notHosted': 'Only a door, window or opening in a wall slides along it',
  'planHandles.move.label': 'Move',
  'planHandles.move.hint': 'Drag to the new place · release to move · Esc cancels',
  'planHandles.move.notMovable': "This element's placement can't be moved here",
  'planHandles.dimension.label': 'Wall distance',
  'planHandles.dimension.edit': 'Clear distance to the next wall, face to face (mm): click to type a new one, the wall moves',
  'planHandles.handle.wallEnd': 'Drag the wall end',
  'planHandles.handle.slide': 'Drag along the wall',
  'planHandles.handle.move': 'Drag to move',
  'planHandles.handle.flip': 'Click to flip the wall (its layers to the other face)',
  'planHandles.handle.flipX': 'Click to flip the hand (Flip X: hinges to the other jamb)',
  'planHandles.handle.flipY': 'Click to flip the swing side (Flip Y: to the other face of the wall)',
  'slabOutline.corner': 'Drag to move this corner · double-click to remove it',
  'slabOutline.pushSide': 'Drag to push this side square to itself (a side cut with Knife: only this piece)',
  'slabOutline.knife': 'Click to cut this side here (Knife)',
  'slabOutline.problem': "The slab's outline could not be changed: {reason}",
} as const satisfies Record<string, TranslationValue>;
