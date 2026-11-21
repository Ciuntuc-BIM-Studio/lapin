/** The results dialog's stability section (docs/plan-fem.md §14.6): what the nonlinear solve did on each combination (second order, one-way members, supports that lift) and the critical load factors αcr with their class (EN 1993-1-1 5.2.1(3)). Indicative: the engineer's own to check. */

import { Play } from 'lucide-react';
import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { useUi } from '../../app/store';
import { query } from '../../engine/session';
import { NotesList } from './NotesList';
import { useResults } from './useResults';
import { animateBucklingMode, useMode } from './useMode';
import type { RunDto } from './results';
import {
  CLASS_LABEL, CLASS_TITLE, classTone, formatAmplification, formatAmplifier, globalText, modeCells, modeChoices, needsLook, slackNames, TONE_COLOUR,
  type BucklingDto, type NonlinearComboDto,
} from './stability';

interface BucklingState { at: RunDto | null; data: BucklingDto | null; loading: boolean; failed: boolean }
const useBuckling = create<BucklingState>(() => ({ at: null, data: null, loading: false, failed: false }));

/** Asks for the critical load factors of the run's ultimate combinations: seconds on a big model, so only on a click. */
async function findBuckling(run: RunDto): Promise<void> {
  useBuckling.setState({ at: run, data: null, loading: true, failed: false });
  const data = await query<BucklingDto>('analysis.buckling', {});
  if (useBuckling.getState().at !== run) return;
  useBuckling.setState({ data, loading: false, failed: !data });
}

export function StabilitySection() {
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const { at, data, loading, failed } = useBuckling();
  // What was found belongs to the run it was asked on.
  useEffect(() => { if (run && at && at !== run) useBuckling.setState({ at: null, data: null, loading: false, failed: false }); }, [run, at]);
  if (!run?.ok) return null;
  const mine = at === run ? data : null;
  const nl = run.nonlinear;
  return (
    <div className="stability" data-testid="stability">
      <p><span className="eyebrow">Stability</span></p>
      {nl && <Nonlinear nl={nl.combinations} secondOrder={nl.secondOrder} />}
      {run.supports && (
        <p className="muted small" data-testid="stability-supports">
          <span>Supports on springs</span> <span data-no-i18n>{run.supports.supports}</span>{run.supports.slabs > 0 && <> · <span>slabs on Winkler&apos;s bed</span> <span data-no-i18n>{run.supports.slabs}</span></>}
        </p>
      )}
      <div className="row">
        <button type="button" className="btn" disabled={loading || stale} data-testid="stability-find" title="The linear buckling of the bars under each ultimate combination: αcr, the factor that brings the loads to the critical ones" onClick={() => void findBuckling(run)}>
          <Play size={12} />{mine ? 'Find again' : 'Find the critical load factors'}
        </button>
        {loading && <span className="muted small">Finding the critical load factors…</span>}
        {failed && <span className="warn-text small">The critical load factors could not be found.</span>}
      </div>
      {mine && <Buckling data={mine} />}
      <p className="muted small">Indicative: αcr is of the bars alone, at uncracked stiffness; the engineer judges whether second order is needed.</p>
    </div>
  );
}

function Nonlinear({ nl, secondOrder }: { nl: NonlinearComboDto[]; secondOrder: boolean }) {
  return (
    <>
      <p className="muted small">{secondOrder ? 'Each combination solved on its own, second order (P-Δ)' : 'Each combination solved on its own'}</p>
      <div className="seismic-scroll">
        <table className="standards-table" data-testid="stability-nonlinear">
          <thead>
            <tr>
              <th>Combination</th><th>Iterations</th><th>Settled</th>
              {secondOrder && <th title="The largest horizontal displacement of the second-order solution over the first-order one">Amplification</th>}
              <th title="One-way members that went slack">Slack members</th><th title="Supports that lifted off the ground">Lifted supports</th><th />
            </tr>
          </thead>
          <tbody>
            {nl.map((c) => (
              <tr key={c.key} className={needsLook(c) ? 'warn-text' : undefined}>
                <td data-no-i18n>{c.name}</td>
                <td data-no-i18n>{c.iterations}</td>
                <td>{c.converged ? '✓' : '✗'}</td>
                {secondOrder && <td data-no-i18n>{formatAmplification(c.amplification)}</td>}
                <td data-no-i18n>{c.slack.length > 0 ? slackNames(c).join(', ') : '–'}</td>
                <td data-no-i18n>{c.lifted.length > 0 ? c.lifted.length : '–'}</td>
                <td>{c.message ?? ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Buckling({ data }: { data: BucklingDto }) {
  const shown = useMode((s) => s.buckling);
  const shownMode = useMode((s) => s.mode);
  const rows = data.combinations;
  const [key, setKey] = useState(data.lowest?.key ?? rows[0]?.key ?? '');
  const [mode, setMode] = useState(1);
  const combo = rows.find((c) => c.key === key) ?? rows[0];
  const choices = modeChoices(combo);
  const m = Math.min(mode, Math.max(1, choices.length));
  return (
    <>
      {rows.length > 0 && (
        <div className="seismic-scroll">
          <table className="standards-table" data-testid="stability-buckling">
            <thead>
              <tr>
                <th>Combination</th><th>α1</th><th>α2</th><th>α3</th>
                <th title="The first mode no single member holds: the frame's own; a lower bound (≥) when every mode found is one member's">Global αcr</th><th>Class</th>
                <th title="1/(1 − 1/αcr): the sway effects' amplifier, EN 1993-1-1 5.2.2(5B)">Amplifier</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((c) => {
                const tone = classTone(c.class);
                const low = data.lowest?.key === c.key;
                return (
                  <tr key={c.key} className={low ? 'seismic-shown' : undefined} data-lowest={low || undefined}>
                    <td data-no-i18n>{c.name}</td>
                    {modeCells(c).map((x, i) => (
                      <td key={i} className={x.local ? 'muted' : undefined} title={x.local ? `${x.local} (${Math.round((x.share ?? 0) * 100)} %)` : undefined} data-no-i18n>
                        {x.alpha}{x.local ? '*' : ''}
                      </td>
                    ))}
                    <td data-no-i18n><b>{globalText(c)}</b></td>
                    <td title={CLASS_TITLE[c.class] ?? c.message ?? undefined}>
                      <span style={{ color: TONE_COLOUR[tone], fontWeight: 600 }} data-tone={tone}>{CLASS_LABEL[c.class] ?? c.class}</span>
                      {c.message && <span className="muted small"> {c.message}</span>}
                    </td>
                    <td data-no-i18n>{formatAmplifier(c.amplifier)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {rows.some((c) => c.modes?.some((x) => x.local)) && <p className="muted small">* A mode one member holds more than half of: that member&apos;s own buckling (its check&apos;s), not the frame&apos;s.</p>}
      {choices.length > 0 && (
        <div className="row" data-testid="stability-mode-pick">
          <span className="eyebrow">Buckling mode</span>
          <select aria-label="Combination" data-testid="stability-mode-combination" value={combo?.key ?? ''} onChange={(e) => { setKey(e.target.value); setMode(1); }}>
            {rows.filter((c) => c.alpha.length > 0).map((c) => <option key={c.key} value={c.key} data-no-i18n>{c.name}</option>)}
          </select>
          <select aria-label="Mode" data-testid="stability-mode" value={m} onChange={(e) => setMode(Number(e.target.value))}>
            {choices.map((n) => <option key={n} value={n} data-no-i18n>{n} · αcr = {modeCells(combo!, choices.length)[n - 1]?.alpha}</option>)}
          </select>
          <button type="button" className={`btn${shown === combo?.key && shownMode === m ? ' primary' : ''}`} data-testid="stability-show-mode" title="Show this buckling mode in the 3D view, swinging"
            onClick={() => { if (combo) { void animateBucklingMode(combo.key, m); useUi.setState({ dialog: null }); } }}>
            <Play size={12} />Show buckling mode
          </button>
        </div>
      )}
      <NotesList notes={data.notes} test="stability-notes" />
    </>
  );
}
