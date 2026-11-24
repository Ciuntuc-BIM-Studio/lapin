/**
 * Manage → Issue… → On the CDE server (docs/ids.md): before Submit for approval, the project's IDS files run over the model and what
 * fails is listed. When the project says a failing specification stops a submission (Manage → IDS…), Submit waits until the user
 * ticks "Submit despite the IDS failures" and writes why; the note goes with the submission (and an issue tag made directly), so the
 * approver reads it.
 */

import { useEffect, useMemo, useState } from 'react';
import { OctagonAlert, RefreshCw } from 'lucide-react';
import { useUi } from '../../../app/store';
import { failing, idsFiles, runIds, submitGate, totals, type IdsFiles, type IdsRun } from './ids';

export type IdsGate = ReturnType<typeof submitGate>;

export function IdsOnSubmit({ onGate }: { onGate: (g: IdsGate) => void }) {
  const [files, setFiles] = useState<IdsFiles | null>(null);
  const [run, setRun] = useState<IdsRun | null>(null);
  const [running, setRunning] = useState(true);
  const [failed, setFailed] = useState<string | null>(null);
  const [on, setOn] = useState(false);
  const [note, setNote] = useState('');
  const check = async () => {
    setRunning(true); setFailed(null);
    try {
      const f = await idsFiles();
      setFiles(f);
      if (f && f.files.length > 0 && f.onSubmit) setRun(await runIds());
    } catch (e) { setFailed(e instanceof Error ? e.message : String(e)); } finally { setRunning(false); }
  };
  useEffect(() => { void check(); }, []);
  // Until the files are read the gate is closed (checking); after, it is submitGate's.
  const gate = useMemo(() => submitGate(run, { files: files ? files.files.length : 1, onSubmit: files?.onSubmit ?? true, blocks: files?.blocks ?? true, running, failed: Boolean(failed) }, { on, note }),
    [run, files, running, failed, on, note]);
  useEffect(() => { onGate(gate); }, [gate.blocked, gate.why, gate.idsOverride, gate.idsFailing]); // eslint-disable-line react-hooks/exhaustive-deps
  if (files && (files.files.length === 0 || !files.onSubmit)) return null;
  const sum = totals(run);
  const bad = failing(run);
  return (
    <div className="ids-on-submit" data-ids-submit>
      <p className="small"><b>IDS</b>{' '}
        {running ? <span className="muted">Checking the IDS files…</span>
          : failed ? <span className="error-text">{failed}</span>
          : sum.fail === 0 ? <span className="cde-ok">Every specification passes</span>
          : <span className="cde-block"><OctagonAlert size={12} /> <span data-no-i18n>{sum.fail}</span> <span>failing</span></span>}
        {!running && <button type="button" className="icon" title="Check again" onClick={() => void check()}><RefreshCw size={12} /></button>}
      </p>
      {bad.length > 0 && (
        <ul className="small ids-failures">
          {bad.slice(0, 8).map(({ file, spec }) => <li key={`${file.id}-${spec.index}`} data-no-i18n>{spec.name} · {spec.failed}/{spec.applicable}</li>)}
          {bad.length > 8 && <li className="muted" data-no-i18n>… +{bad.length - 8}</li>}
        </ul>
      )}
      {bad.length > 0 && <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'ids' })}>Open Manage → IDS… for the elements</button>}
      {bad.length > 0 && files?.blocks && (
        <>
          <label className="row small"><input type="checkbox" checked={on} onChange={(e) => setOn(e.target.checked)} data-ids="override" /> <span>Submit despite the IDS failures</span></label>
          {on && <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Why it goes all the same (kept with the submission)" aria-label="Why it goes despite the IDS failures" data-ids="override-note" />}
        </>
      )}
      {bad.length > 0 && files && !files.blocks && <p className="muted small">The project lets a submission go with IDS failures.</p>}
      {gate.blocked && gate.why && !running && <p className="cde-block small">{gate.why}</p>}
    </div>
  );
}
