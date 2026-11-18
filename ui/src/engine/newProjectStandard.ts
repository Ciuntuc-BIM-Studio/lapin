/**
 * The material standard a new project starts with (docs/plan-material-graphics.md, stage 3): chosen by the user (Preferences, the
 * start screen), default Lapin, never picked from the language or the country. Applied after the project is made
 * (`standards.applyMaterials`: one undo step on the new project); Lapin's own needs nothing, a new project is already on it.
 */

import { usePrefs } from '../app/prefs';
import { notify } from '../app/store';
import { execute } from './session';

/** The standards Lapin carries, for the choice before any project is open (their ids and names as the engine has them). */
export const BUILTIN_MATERIAL_STANDARDS: { id: string; name: string }[] = [
  { id: 'lapin', name: 'Lapin' }, { id: 'iso-128-50', name: 'ISO 128-50' }, { id: 'din-1356', name: 'DIN 1356' },
  { id: 'ro-sr-iso-128-50', name: 'România (SR ISO 128-50)' }, { id: 'bs-8888', name: 'BS 8888 / BS 1192' },
];

/** The id to apply, or null when none is (Lapin's own, or nothing chosen). */
export const newProjectStandardId = (pref: string | null | undefined): string | null => (pref && pref !== 'lapin' ? pref : null);

/** What the apply asks of the engine. */
export interface ApplyDeps { apply: (id: string) => Promise<unknown>; say: (text: string) => void }

/** Applies the chosen standard to the project just made; true when one was applied. */
export async function applyNewProjectStandard(pref: string | null | undefined = usePrefs.getState().newProjectMaterialStandard, deps: ApplyDeps = {
  apply: (id) => execute('standards.applyMaterials', { id }), say: notify,
}): Promise<boolean> {
  const id = newProjectStandardId(pref);
  if (!id) return false;
  try {
    const r = await deps.apply(id);
    if (r == null) { deps.say(`The material standard „${id}” could not be applied: the new project keeps Lapin's.`); return false; }
    return true;
  } catch { deps.say(`The material standard „${id}” could not be applied: the new project keeps Lapin's.`); return false; }
}
