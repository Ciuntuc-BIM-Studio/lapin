/**
 * The Structural usage of an element (docs/plan-structural-usage.md, decision 866): the engine's resolver
 * (`element.structural`) says what the element or its type says (its own tick `loadBearing`, a Pset_…Common LoadBearing,
 * its IFC file's) and, when nothing is said, what the rule makes of it; `element.setStructural` sets or clears the tick.
 */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';

/** The classes the tick applies to (their types too), as the engine's StructuralUsage.Applies. */
export const STRUCTURAL_CLASSES = ['IfcWall', 'IfcWallStandardCase', 'IfcCurtainWall', 'IfcSlab', 'IfcRoof', 'IfcColumn', 'IfcBeam', 'IfcFooting', 'IfcPile', 'IfcMember', 'IfcPlate'];
export const STRUCTURAL_TYPES = STRUCTURAL_CLASSES.map((c) => `${c}Type`);

export const isStructuralClass = (cls: string) => STRUCTURAL_CLASSES.includes(cls) || STRUCTURAL_TYPES.includes(cls);

export interface StructuralRow {
  id: string;
  applies: boolean;
  said?: boolean | null;
  effective?: boolean;
  source?: string;
  conflict?: boolean;
  analysis?: { said?: boolean | null; effective: boolean; source: string };
  typeSaid?: boolean;
}

/** Where the value comes from, as the properties say it under the tick. */
export function sourceText(row: StructuralRow): string {
  switch (row.source) {
    case 'own': return 'set on this element';
    case 'ownPset': return 'from its LoadBearing property';
    case 'file': return 'from its IFC file';
    case 'type': return 'from its type';
    case 'typePset': return "from its type's LoadBearing property";
    case 'typeFile': return "from its type's IFC file";
    case 'rule:partition': return 'by rule: a partition, not structural';
    case 'rule:thickness': return 'by rule: structural in views; the analysis takes it when of masonry or concrete with a core of 10 cm or more';
    default: return row.effective ? 'by rule: its kind is structural' : 'by rule: its kind is not structural';
  }
}

/** Whether the value is the rule's (shown greyed): nothing on the element or its type says it. */
export const byRule = (row: StructuralRow) => row.said == null;

/** The tick's next value from a click: said true or false; `null` goes back to the rule. */
export const setArgs = (ids: string[], value: boolean | null, onType = false) => ({ ids, value, ...(onType ? { onType: true } : {}) });

/** Modify → Structural and Not structural: the tick on the selection's elements that may bear load. */
export async function markSelection(value: boolean): Promise<void> {
  const nodes = useSession.getState().nodes;
  const ids = useUi.getState().selected.filter((id) => { const n = nodes.get(id); return !!n && isStructuralClass(n.type); });
  if (!ids.length) { notify('Select walls, floors, roofs, columns or beams first.'); return; }
  await execute('element.setStructural', setArgs(ids, value));
}
