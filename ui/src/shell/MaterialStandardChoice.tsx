/** The material standard a new project starts with, beside the units and the template: Lapin's by default, never chosen for the user by language or country. */

import { setPrefs, usePrefs } from '../app/prefs';
import { BUILTIN_MATERIAL_STANDARDS } from '../engine/newProjectStandard';

export function MaterialStandardChoice() {
  const value = usePrefs((p) => p.newProjectMaterialStandard) ?? 'lapin';
  const known = BUILTIN_MATERIAL_STANDARDS.some((s) => s.id === value);
  return (
    <label className="row small" data-testid="new-project-standard">
      <span>Material standard</span>
      <select data-field="new-project-material-standard" value={value} title="How materials draw in cuts, plans and facades in a new project; Manage → Material standards changes it later"
        onChange={(e) => setPrefs({ newProjectMaterialStandard: e.target.value === 'lapin' ? null : e.target.value })}>
        {BUILTIN_MATERIAL_STANDARDS.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
        {!known && <option value={value} data-no-i18n>{value}</option>}
      </select>
    </label>
  );
}
