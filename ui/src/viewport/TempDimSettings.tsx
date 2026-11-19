/**
 * The Temporary Dimension Properties' two groups (decision 738), as Revit lays them out: Walls (centrelines, faces, centre of
 * core, faces of core, and Lapin's reference line below them) and Doors and windows (centrelines, openings). Shown in the dialog
 * (Annotate → Temporary dimensions…) and on Preferences → Grid and snaps; the caller keeps the values.
 */

import { DIM_REFS, DIM_REF_LABELS, OPENING_DIM_REFS, OPENING_DIM_REF_LABELS } from './lineDims';
import { DIM_REF_NOTES, type TempDimChoice } from './tempDimProps';

export function TempDimSettings({ value, onChange, name = 'temp-dims' }: { value: TempDimChoice; onChange: (v: TempDimChoice) => void; name?: string }) {
  return (
    <div className="temp-dim-settings">
      <fieldset data-testid="temp-dim-walls" role="radiogroup" aria-label="Walls">
        <legend>Walls, columns and beams</legend>
        {DIM_REFS.map((r) => (
          <label key={r} className="row small" title={DIM_REF_NOTES[r]} style={r === 'reference' ? { marginTop: 4 } : undefined}>
            <input type="radio" name={`${name}-walls`} value={r} data-field={`temp-dim-walls-${r}`} checked={value.walls === r} onChange={() => onChange({ ...value, walls: r })} /> {DIM_REF_LABELS[r]}
          </label>
        ))}
      </fieldset>
      <fieldset data-testid="temp-dim-openings" role="radiogroup" aria-label="Doors and windows">
        <legend>Doors and windows</legend>
        {OPENING_DIM_REFS.map((r) => (
          <label key={r} className="row small">
            <input type="radio" name={`${name}-openings`} value={r} data-field={`temp-dim-openings-${r}`} checked={value.openings === r} onChange={() => onChange({ ...value, openings: r })} /> {OPENING_DIM_REF_LABELS[r]}
          </label>
        ))}
      </fieldset>
    </div>
  );
}
