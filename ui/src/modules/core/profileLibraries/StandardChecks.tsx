/**
 * The profile standards as a list of boxes (decision 457): those a project offers (Manage → Profile libraries), those
 * a new project offers (Preferences → Profiles), those the pickers show for this user. `value` null: every standard,
 * one added later too; a box unticked lists the others.
 */

import { toggled } from './model';

export function StandardChecks({ all, value, onChange, testId }: { all: string[]; value: string[] | null; onChange: (v: string[] | null) => void; testId: string }) {
  if (!all.length) return <p className="muted small">No library yet.</p>;
  return (
    <div data-testid={testId}>
      <div className="pref-checks">
        {all.map((g) => (
          <label key={g} className="row small">
            <input type="checkbox" data-standard={g} checked={!value || value.includes(g)} onChange={(e) => onChange(toggled(value, all, g, e.target.checked))} /> <span data-no-i18n>{g}</span>
          </label>
        ))}
      </div>
      <p className="row small">
        <button type="button" className="btn" disabled={!value} onClick={() => onChange(null)} title="Every standard, those added later too">All</button>
      </p>
    </div>
  );
}
