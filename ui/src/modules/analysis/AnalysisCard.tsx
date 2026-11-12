/** Over the model view while the analysis is shown: what is drawn (case, combination or envelope; the quantity; its scale), and Run again once the model has changed. */

import { Play, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { hideDiagrams, runAnalysis, setMult, setPick, setQuantity, setReactions, useResults } from './useResults';
import { formatValue, pickChoices, pickValue, QUANTITIES, type Quantity } from './results';
import { ShellPicker } from './ShellContours';

const SHORT: Record<Quantity, string> = { N: 'N', Vy: 'Vy', Vz: 'Vz', T: 'T', My: 'My', Mz: 'Mz', deformed: 'Deformed' };
const TITLE: Record<Quantity, string> = {
  N: 'Axial force (tension positive)', Vy: 'Shear force along local axis 2', Vz: 'Shear force along local axis 3', T: 'Torsion',
  My: 'Bending moment about local axis 2', Mz: 'Bending moment about local axis 3 (a level beam\'s strong axis, sagging positive)', deformed: 'The deformed shape',
};

export function AnalysisCard() {
  const view = useUi((s) => s.view);
  const { run, show, stale, pick, quantity, mult, reactions, diagram, running } = useResults();
  if (!run || !show || view === 'sheet' || view === 'schedule') return null;
  const choices = pickChoices(run);
  const groups = ['Load cases', 'Combinations', 'Envelopes'] as const;
  return (
    <div className="sel-card analysis-card" role="region" aria-label="Analysis results" data-testid="analysis-card">
      <div className="row between">
        <b>Analysis results</b>
        <button type="button" className="icon mini" title="Take the diagrams off the view" aria-label="Hide the diagrams" onClick={hideDiagrams}><X size={12} /></button>
      </div>
      {stale ? (
        <>
          <p className="warn-text small">The model has changed: these results are out of date.</p>
          <button type="button" className="btn primary" disabled={running} data-testid="analysis-rerun" onClick={() => void runAnalysis(false).then(() => useResults.setState({ show: true }))}><Play size={14} />Run again</button>
        </>
      ) : (
        <>
          <select aria-label="Load case, combination or envelope" data-testid="analysis-pick" value={pick ? pickValue(pick) : ''} onChange={(e) => setPick(e.target.value)}>
            {groups.map((g) => (
              <optgroup key={g} label={g}>
                {choices.filter((c) => c.group === g).map((c) => <option key={pickValue(c.pick)} value={pickValue(c.pick)}>{c.label}</option>)}
              </optgroup>
            ))}
          </select>
          <div className="row quantities" role="group" aria-label="Quantity">
            {QUANTITIES.map((q) => (
              <button key={q} type="button" className={`btn mini${quantity === q ? ' primary' : ''}`} aria-pressed={quantity === q} title={TITLE[q]} data-testid={`analysis-q-${q}`} onClick={() => setQuantity(q)}>{SHORT[q]}</button>
            ))}
          </div>
          <ShellPicker />
          <label className="row small" title="The diagram's size on the view; the model is not changed">
            <span>Scale</span>
            <input type="range" min={-2} max={2} step={0.1} value={Math.log2(mult)} aria-label="Diagram scale" data-testid="analysis-scale" onChange={(e) => setMult(2 ** Number(e.target.value))} />
            <span data-no-i18n>×{formatValue(mult)}</span>
          </label>
          <label className="row small"><input type="checkbox" checked={reactions} onChange={(e) => setReactions(e.target.checked)} />Reactions</label>
          {diagram && <p className="muted small">Largest value <span data-no-i18n>{formatValue(diagram.max * (diagram.unit === 'm' ? 1000 : 1))} {diagram.unit === 'm' ? 'mm' : diagram.unit}</span></p>}
          <div className="row">
            <button type="button" className="btn mini" onClick={() => useUi.setState({ dialog: 'analysisResults' })}>Results…</button>
            <button type="button" className="btn mini" disabled={running} onClick={() => void runAnalysis(false)}>Run again</button>
          </div>
        </>
      )}
    </div>
  );
}
