/**
 * Manage → Check: the pre-issue rules run over the whole project
 * (docs/plan-cde.md), their findings grouped by rule, blocking first; a
 * click takes the user to the sheet, the view or the element. The same list
 * shows in Manage → Issue… before an issue goes.
 */

import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, OctagonAlert, RefreshCw, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { cdeCheck, goTo, groupFindings, type CheckResult } from './cde';

export function CheckDialog() {
  const nodes = useSession((s) => s.nodes);
  const [r, setR] = useState<CheckResult | null>(null);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const run = useCallback(async () => { setBusy(true); try { setR(await cdeCheck()); } finally { setBusy(false); } }, []);
  useEffect(() => { void run(); }, [run, nodes]);
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Check" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Check</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde">
          <p className="muted small">What a drawing office checks before an issue: title blocks, legends, scales, views on the paper and on sheets, revisions after a change, file names, and the model's basics. A blocking finding stops an issue; a warning may be overridden.</p>
          {r && <FindingsList result={r} />}
          {r === null && <p className="muted">Checking…</p>}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" disabled={busy} onClick={() => void run()}><RefreshCw size={13} /> Check again</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}

/** The findings by rule; each one a link to what it is about. */
export function FindingsList({ result }: { result: CheckResult }) {
  const groups = groupFindings(result.findings, result.rules);
  if (groups.length === 0) return <p className="cde-ok">Nothing to fix.</p>;
  return (
    <div className="cde-findings" data-testid="cde-findings">
      <p className="muted small">{result.blocking} <span>blocking</span> · {result.warnings} <span>warnings</span></p>
      {groups.map((g) => (
        <div key={g.id} className="version-group">
          <span className={g.blocking ? 'cde-block' : 'cde-warn'}>{g.blocking ? <OctagonAlert size={12} /> : <AlertTriangle size={12} />} <span>{g.label}</span> · {g.findings.length}</span>
          <ul>
            {g.findings.map((f, i) => (
              <li key={i} className={f.severity === 'block' ? 'cde-block' : undefined}>
                <button type="button" className="link" onClick={() => goTo(f)}>{f.message}</button>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
