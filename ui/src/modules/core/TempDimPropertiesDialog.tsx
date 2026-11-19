/**
 * Annotate (or Manage) → Temporary dimensions… (decision 738; a user: "select and remember the preference as we have in the
 * Revit workflow"): Revit's Temporary Dimension Properties. Where temporary dimensions measure from, for walls (and columns,
 * beams) and for doors and windows, kept for the user on this computer for every project (the same as Preferences → Grid and
 * snaps, the tools' bars and a dimension dot's right click). OK keeps the choice, Cancel leaves it as it was.
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { useUi } from '../../app/store';
import { setPrefs, usePrefs } from '../../app/prefs';
import { TempDimSettings } from '../../viewport/TempDimSettings';
import type { TempDimChoice } from '../../viewport/tempDimProps';

export function TempDimPropertiesDialog() {
  const walls = usePrefs((s) => s.tempDimRef);
  const openings = usePrefs((s) => s.tempDimOpeningRef);
  const [choice, setChoice] = useState<TempDimChoice>({ walls, openings });
  const close = () => useUi.setState({ dialog: null });
  const ok = () => { setPrefs({ tempDimRef: choice.walls, tempDimOpeningRef: choice.openings }); close(); };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog temp-dim-dialog" role="dialog" aria-label="Temporary dimension properties" data-testid="temp-dim-dialog" onClick={(e) => e.stopPropagation()}
        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); ok(); } else if (e.key === 'Escape') { e.preventDefault(); close(); } }}>
        <header className="dialog-head"><span className="panel-title">Temporary dimension properties</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Temporary dimensions measure from:</p>
          <TempDimSettings value={choice} onChange={setChoice} name="temp-dim-dialog" />
          <p className="muted small">Kept for you on this computer, for every project. A click on a dimension&rsquo;s dot still changes that one dimension; a right click on it makes its end the default.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" data-testid="temp-dim-cancel" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-testid="temp-dim-ok" onClick={ok}>OK</button>
        </footer>
      </div>
    </div>
  );
}
