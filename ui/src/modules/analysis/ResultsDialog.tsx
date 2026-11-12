/** The analysis' results (docs/plan-fem.md §9.6): what the run did (unknowns, time, each case's loads against its reactions), what it refused, a mechanism, and the extremes of the case, combination or envelope picked. Indicative: the engineer's own to check. */

import { Play, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { PendingDialog } from '../core/PendingDialog';
import { hideDiagrams, runAnalysis, setPick, showDiagrams, useResults } from './useResults';
import {
  balanceText, formatMm, formatValue, formatVec, FORCE_QUANTITIES, pickChoices, pickValue, spanRatio,
  type Extreme, type ResultsDto, type RunDto,
} from './results';

/** Selects the elements a mechanism involves, and closes the dialog so they can be seen. */
export function selectMechanism(ids: string[]): void {
  if (ids.length === 0) return;
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null, dialog: null });
}

export function ResultsDialog() {
  const { run, error, running, pick, results, stale } = useResults();
  const close = () => useUi.setState({ dialog: null });
  if (!run) return <PendingDialog title="Analysis results" error={error ?? (running ? null : 'Nothing has been analysed yet: choose Run analysis.')} />;
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Analysis results">
      <div className="dialog" style={{ minWidth: 820 }}>
        <header className="dialog-head">
          <span className="panel-title">Analysis results</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body" data-testid="analysis-results">
          {stale && <p className="warn-text" role="status">The model has changed since this run: the results are out of date.</p>}
          <Summary run={run} />
          {run.mechanism && <Mechanism run={run} />}
          {run.refused && run.refused.length > 0 && (
            <>
              <span className="eyebrow">Left out of the analysis</span>
              <table className="standards-table" data-testid="analysis-refused">
                <thead><tr><th>Element</th><th>Reason</th></tr></thead>
                <tbody>{run.refused.map((x) => <tr key={x.id}><td data-no-i18n>{x.name || x.sourceId}</td><td>{x.reason}</td></tr>)}</tbody>
              </table>
            </>
          )}
          {run.ok && pick && (
            <>
              <div className="row">
                <span className="eyebrow">Largest values of</span>
                <select aria-label="Load case, combination or envelope" data-testid="results-pick" value={pickValue(pick)} onChange={(e) => setPick(e.target.value)}>
                  {(['Load cases', 'Combinations', 'Envelopes'] as const).map((g) => (
                    <optgroup key={g} label={g}>{pickChoices(run).filter((c) => c.group === g).map((c) => <option key={pickValue(c.pick)} value={pickValue(c.pick)}>{c.label}</option>)}</optgroup>
                  ))}
                </select>
              </div>
              {results ? <Maxima results={results} /> : <p className="muted">Reading the results…</p>}
            </>
          )}
          {run.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
          <p className="muted small">The results are indicative: loads, combinations and the analysis are yours to check before you rely on them.</p>
          <div className="row">
            <button type="button" className="btn" disabled={running} data-testid="results-rerun" onClick={() => void runAnalysis()}><Play size={14} />Run again</button>
            {run.ok && !stale && <button type="button" className="btn primary" data-testid="results-show" onClick={() => { showDiagrams(); close(); }}>Show the diagrams</button>}
            {run.ok && !stale && <button type="button" className="btn" onClick={() => { hideDiagrams(); close(); }}>Close without diagrams</button>}
          </div>
        </div>
      </div>
    </div>
  );
}

function Summary({ run }: { run: RunDto }) {
  const bad = run.cases.filter((c) => c.balanced === false);
  return (
    <>
      <p className="row" data-testid="analysis-summary">
        <span>Bars</span><b data-no-i18n>{run.elements}</b><span className="muted">·</span>
        <span>Nodes</span><b data-no-i18n>{run.nodes}</b><span className="muted">·</span>
        <span>Unknowns</span><b data-no-i18n>{run.dofs}</b><span className="muted">·</span>
        <span>Solve time</span><b data-no-i18n>{formatValue(run.solveMs)} ms ({formatValue(run.totalMs)} ms)</b>
      </p>
      <table className="standards-table" data-testid="analysis-cases">
        <thead><tr><th>Case</th><th title="The sum of the loads on the bars, Fx · Fy · Fz (kN)">Loads (kN)</th><th title="The sum of the supports' reactions, Fx · Fy · Fz (kN)">Reactions (kN)</th><th>Balance</th><th title="Load that found nothing under it and was left out (kN)">Unmapped (kN)</th></tr></thead>
        <tbody>
          {run.cases.map((c) => (
            <tr key={c.id} className={c.balanced === false ? 'warn-text' : undefined}>
              <td data-no-i18n>{c.code}</td>
              <td data-no-i18n>{formatVec(c.sumLoads)}</td>
              <td data-no-i18n>{formatVec(c.sumReactions)}</td>
              <td title={balanceText(c)}>{c.balanced == null ? '–' : c.balanced ? '✓' : '✗'}</td>
              <td data-no-i18n>{c.unmapped > 1e-6 ? formatValue(c.unmapped) : '–'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {bad.length > 0 && <p className="warn-text"><span>The reactions do not balance the loads in:</span> <b data-no-i18n>{bad.map((c) => c.code).join(', ')}</b>. <span>Do not rely on these results.</span></p>}
      {run.cases.some((c) => c.unmapped > 1e-6) && <p className="warn-text">Some loads find nothing under them (no bar, slab or wall within reach) and are left out: see the notes below.</p>}
    </>
  );
}

function Mechanism({ run }: { run: RunDto }) {
  const m = run.mechanism!;
  // The elements the engine names (its sources are what the user sees in the model).
  const ids = [...new Set([...(m.sources ?? []), ...(m.movingElements ?? [])].filter(Boolean))];
  return (
    <div className="warn-text" role="alert" data-testid="analysis-mechanism">
      <strong>The structure is a mechanism: it can move without resistance.</strong>
      <p>{m.message}</p>
      {m.names?.length > 0 && <p className="small" data-no-i18n>{[...new Set([...(m.names ?? []), ...(m.movingNames ?? [])])].join(', ')}</p>}
      {ids.length > 0 && <button type="button" className="btn" data-testid="analysis-select-mechanism" onClick={() => selectMechanism(ids)}>Select the elements involved</button>}
    </div>
  );
}

const at = (e: Extreme | undefined, byId: Map<string, string>) => (e ? `${byId.get(e.frame) ?? e.frame} · ${formatValue(e.x)} m` : '–');

function Maxima({ results }: { results: ResultsDto }) {
  const byId = new Map(results.frames.map((f) => [f.id, f.name]));
  const lengths = new Map(results.frames.map((f) => [f.id, f.length]));
  const mx = results.maxima as unknown as Record<string, { max: Extreme; min: Extreme } | undefined>;
  const defl = results.maxima.deflection;
  const disp = results.maxima.displacement;
  return (
    <table className="standards-table" data-testid="analysis-maxima">
      <thead><tr><th>Quantity</th><th>Largest</th><th>Where</th><th>Smallest</th><th>Where</th></tr></thead>
      <tbody>
        {FORCE_QUANTITIES.map((q) => {
          const m = mx[q];
          if (!m) return null;
          return <tr key={q}><td>{q} <span className="muted small" data-no-i18n>{q === 'N' || q[0] === 'V' ? 'kN' : 'kN·m'}</span></td><td data-no-i18n>{formatValue(m.max.value)}</td><td data-no-i18n>{at(m.max, byId)}</td><td data-no-i18n>{formatValue(m.min.value)}</td><td data-no-i18n>{at(m.min, byId)}</td></tr>;
        })}
        {disp && <tr><td>Largest displacement</td><td data-no-i18n>{formatMm(disp.value)}</td><td colSpan={3} className="muted small" data-no-i18n>{disp.node}</td></tr>}
        {defl && <tr><td>Largest deflection</td><td data-no-i18n>{formatMm(defl.value)}</td><td colSpan={3} data-no-i18n>{at(defl, byId)}{spanRatio(lengths.get(defl.frame) ?? 0, Math.abs(defl.value)) ? ` · ${spanRatio(lengths.get(defl.frame) ?? 0, Math.abs(defl.value))}` : ''}</td></tr>}
      </tbody>
    </table>
  );
}
