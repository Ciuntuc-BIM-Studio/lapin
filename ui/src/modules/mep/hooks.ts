/**
 * What another module adds over the open MEP (mep-pro, when the product has
 * it), set as it loads: the laying rules of materials for the Run tool, a
 * word on a material's rule, and what Done does at the end of a system's
 * edit. Without them a run goes where it is drawn and Done leaves the mode.
 */

import type { ComponentType } from 'react';
import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';

export interface MepHooks {
  /** Buttons for a run in its MEP section (its profile); null: none. */
  runActions: ComponentType<{ node: NodeDto }> | null;
  /** Where the next point of a run may go, given the points placed, for the material (the engine's laying rules); the aimed point when it has none. */
  nextPoint: (service: string, material: string | null, placed: Vec2[], aimed: Vec2) => Vec2;
  /** The material's laying rule in a few words, shown after its name; null when it has none. */
  ruleText: (service: string, material: string | null) => string | null;
  /** The end of a system's edit (Done): resolves it. Null: nothing beyond leaving the mode. */
  resolve: ((systemId: string) => Promise<void>) | null;
}

export const mepHooks: MepHooks = {
  runActions: null,
  nextPoint: (_service, _material, _placed, aimed) => aimed,
  ruleText: () => null,
  resolve: null,
};
