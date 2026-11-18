/** Save as hatch pattern…: the name, description and units of the pattern made from the linework chosen; then what it became. */

import { useState } from 'react';
import { Dialog } from '../../shell/Dialog';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { phrase } from '../../shell/prompts';
import { forgetHatchPreviews } from './hatchCache';
import { openHatchPatterns } from './hatchEditorOpen';
import { UNITS_HELP, UNITS_LABEL, type HatchUnits } from './hatchUnits';
import { resultNote, toPatternArgs, validName } from './hatchPatternModel';
import { useSaveHatch } from './hatchPatternTools';

export function SaveHatchDialog() {
  const request = useSaveHatch((s) => s.request);
  const [name, setName] = useState('MY-PATTERN');
  const [description, setDescription] = useState('');
  const [units, setUnits] = useState<HatchUnits>('paper');
  const [busy, setBusy] = useState(false);
  const [made, setMade] = useState<{ name: string; kind: 'lines' | 'tile'; families: number } | null>(null);
  const close = () => { useSaveHatch.setState({ request: null }); useUi.setState({ dialog: null }); };
  if (!request) return null;

  const go = async () => {
    if (!validName(name) || busy) return;
    setBusy(true);
    const r = await execute('drafting.toPattern', toPatternArgs(request.viewId, request.ids, request.cell, name, description, units)) as { name: string; kind: 'lines' | 'tile'; families?: unknown[] | number } | null;
    setBusy(false);
    if (!r) { notify(useSession.getState().error ?? 'The pattern could not be saved.'); return; }
    forgetHatchPreviews();
    setMade({ name: r.name, kind: r.kind, families: Array.isArray(r.families) ? r.families.length : (r.families ?? 0) });
  };

  const note = made && resultNote({ kind: made.kind, families: made.families });
  return (
    <Dialog title="Save as hatch pattern" onClose={close} className="tpl-save" dataDialog="save-hatch"
      footer={made ? (
        <>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Done</button>
          <button type="button" className="btn primary" data-field="hp-open-editor" onClick={() => { useSaveHatch.setState({ request: null }); openHatchPatterns({ select: made.name }); }}>Open in the editor</button>
        </>
      ) : (
        <>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!validName(name) || busy} data-field="hp-save-go" onClick={() => void go()}>Save</button>
        </>
      )}>
      {made && note ? (
        <>
          <p data-testid="hp-result">{phrase('The pattern {name} is in the project.', { name: made.name })}</p>
          <p className="muted small" data-testid="hp-result-kind">{phrase(note.text, note.vars)}</p>
        </>
      ) : (
        <>
          <label className="doc-info-row"><span className="muted small">Name</span>
            <input autoFocus value={name} data-field="hp-save-name" data-no-i18n onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') void go(); }} /></label>
          <label className="doc-info-row"><span className="muted small">Description</span>
            <input value={description} onChange={(e) => setDescription(e.target.value)} /></label>
          <label className="doc-info-row"><span className="muted small">Units</span>
            <select value={units} onChange={(e) => setUnits(e.target.value as HatchUnits)} data-field="hp-save-units" title={UNITS_HELP[units]}>
              <option value="paper">{UNITS_LABEL.paper}</option>
              <option value="model">{UNITS_LABEL.model}</option>
            </select></label>
          <p className="muted small">{request.cell ? phrase('The cell is {w} × {h} m of the view.', { w: +request.cell.width.toFixed(4), h: +request.cell.height.toFixed(4) }) : 'No cell was clicked: the lines\' own bounds are the cell.'}</p>
          {!validName(name) && <p className="muted small">A pattern&apos;s name is one word of letters, digits, _ and -.</p>}
        </>
      )}
    </Dialog>
  );
}
