/**
 * What other modules add to the structure module's interface: extra fields in the
 * Structure types dialog for a column or beam type being edited (the rebar
 * module's stirrups). Filled in at import time; empty without them.
 */

import type { ComponentType } from 'react';
import type { NodeDto } from '../../engine/protocol';

export interface StructureHooks {
  /** Rendered under a column's or beam's fields while that type is edited. */
  typeExtras: ComponentType<{ type: NodeDto; kind: string }>[];
}

export const structureHooks: StructureHooks = { typeExtras: [] };
